#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crunchee.Game;

namespace Crunchee.EditorTools
{
    public static partial class ButterPathBuilder
    {
        static Transform BuildThemeEnvironmentChunk(SquishyThemeDefinition theme, int turn)
        {
            var root = new GameObject($"Environment_{theme.id}_{turn}").transform;
            root.position = new Vector3(EndlessSquishyPath.Radius, EndlessSquishyPath.BaseY + turn * EndlessSquishyPath.TurnHeight, EndlessSquishyPath.StartZ);
            bool bakery = theme.id == ThemePath.Bakery.id;
            var column = PropMat(theme.id + "Column", bakery ? new Color(0.86f, 0.72f, 0.58f) : new Color(0.98f, 0.91f, 0.7f), 0.35f, 0.06f);
            var trim = PropMat(theme.id + "Trim", bakery ? new Color(0.55f, 0.34f, 0.2f) : new Color(0.95f, 0.72f, 0.25f), 0.5f, 0.08f);
            Cyl(root, "DisplayColumn", new Vector3(0f, EndlessSquishyPath.TurnHeight / 2f, 0f), new Vector3(1.65f, EndlessSquishyPath.TurnHeight / 2f + 0.5f, 1.65f), column);
            for (int i = 0; i < 4; i++)
                Cyl(root, "DisplayShelf", new Vector3(0f, 0.6f + i * 2.25f, 0f), new Vector3(1.95f, 0.07f, 1.95f), trim);

            var cloud = PropMat(theme.id + "Cloud", new Color(0.99f, 0.97f, 1f), 0.2f, 0.08f);
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + turn * 19f) * Mathf.Deg2Rad;
                var at = root.position + new Vector3(Mathf.Cos(a) * 12.2f, 1.5f + (i % 3) * 2.7f, Mathf.Sin(a) * 12.2f);
                var cloudGo = BuildCloud(at, 1.15f + (i % 2) * 0.35f, cloud);
                cloudGo.transform.SetParent(root, true);
            }

            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                var at = root.position + new Vector3(Mathf.Cos(a) * 10.8f, 1f + (i % 3) * 2.35f, Mathf.Sin(a) * 10.8f);
                GameObject prop;
                if (bakery)
                {
                    prop = i % 2 == 0
                        ? BuildCroissant(at)
                        : BuildCupcake(at, BakeryCakeMats()[i % 3], PropMat("Cherry", new Color(0.9f, 0.15f, 0.25f), 0.9f, 0.05f));
                    prop.transform.SetParent(root, true);
                    prop.transform.localScale *= 2.1f;
                    foreach (var t in prop.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                }
                else
                {
                    var butter = ButterBodyMat("FloatingClassicButter", new Color(1f, 0.84f, 0.36f), 0.58f);
                    prop = Box("FloatingButter", at, new Vector3(0.75f, 0.48f, 1.75f), butter);
                    prop.transform.SetParent(root, true);
                    if (i % 2 == 0)
                    {
                        var sugar = PropMat("FloatingSugar", new Color(1f, 0.98f, 0.95f), 0.85f, 0.2f, GlitterTexture());
                        Ball(prop.transform, "SugarDust", new Vector3(0f, 0.65f, 0f), new Vector3(0.8f, 0.2f, 0.8f), sugar);
                    }
                }
                foreach (var c in prop.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var bob = prop.AddComponent<FloatBob>();
                bob.amplitude = 0.18f; bob.speed = 0.45f + i * 0.05f; bob.spin = i % 2 == 0 ? 8f : -7f; bob.phase = i * 0.8f;
            }

            var dust = new GameObject("ThemeDust").AddComponent<ParticleSystem>();
            dust.transform.SetParent(root, false);
            dust.transform.localPosition = new Vector3(0f, EndlessSquishyPath.TurnHeight / 2f, 0f);
            var main = dust.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = 90;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0.08f; main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new Color(1f, 0.94f, 0.82f, 0.42f);
            var emission = dust.emission; emission.rateOverTime = 12f;
            var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(23f, EndlessSquishyPath.TurnHeight, 23f);
            dust.GetComponent<ParticleSystemRenderer>().material = SparkleParticleMat();
            return root;
        }
    }
}
#endif
