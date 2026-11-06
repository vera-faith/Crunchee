#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Crunchee.Game;

namespace Crunchee.EditorTools
{
    /// <summary>
    /// The world around the path (reference videos): a tall glass tower
    /// ringed by glowing white pillars, a sunset sky with pink-orange clouds,
    /// and giant glossy candy floating in the air. Plus the shared
    /// rounded-box meshes that replace raw cubes everywhere.
    /// </summary>
    public static partial class ButterPathBuilder
    {
        const string GeneratedMeshDir = "Assets/Models/Generated";
        const float TowerR = 14f, TowerH = 34f;
        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> propMats = new Dictionary<string, Material>();

        static void ResetWorldCaches()
        {
            meshCache.Clear();
            propMats.Clear();
            if (AssetDatabase.IsValidFolder(GeneratedMeshDir)) AssetDatabase.DeleteAsset(GeneratedMeshDir);
            Directory.CreateDirectory(GeneratedMeshDir);
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- sky + light

        static void SetupSunsetSky()
        {
            const string path = "Assets/Materials/SunsetSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Crunchee/SunsetSky"));
                AssetDatabase.CreateAsset(sky, path);
            }
            sky.SetVector("_SunDir", new Vector4(0.55f, 0.1f, 1f, 0f));
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.44f, 0.4f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.56f, 0.44f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.3f, 0.35f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.98f, 0.76f, 0.74f);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 150f;

            // Sky-only realtime probe so glossy candy picks up the sunset.
            var probeGo = new GameObject("SkyReflection");
            probeGo.transform.position = new Vector3(0f, 6f, SpiralCenterZ);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.cullingMask = 0;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
            probe.size = new Vector3(200f, 200f, 200f);
            probe.resolution = 64;
            probe.importance = 0;
        }

        static void AddFillLight()
        {
            var fill = new GameObject("SkyFill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.86f, 0.7f, 1f);
            fill.intensity = 0.22f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 150f, 0f);
        }

        // ---------------------------------------------------------------- tower

        static void BuildSunsetTower(Vector3 center)
        {
            var root = new GameObject("SunsetTower").transform;

            // Glass wall: an open tube, drawn first among transparents so
            // the wax and jelly always sort on top of it.
            var glass = Mat("TowerGlass", new Color(0.96f, 0.88f, 1f), 0.06f, 0.95f);
            glass.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            glass.SetColor("_RimColor", new Color(1f, 0.85f, 0.95f));
            glass.SetFloat("_RimStrength", 0.3f);
            glass.SetFloat("_SpecStrength", 0.6f);
            glass.SetFloat("_RimPower", 3.5f);
            glass.renderQueue = 2950;
            var tube = new GameObject("GlassWall");
            tube.transform.SetParent(root, false);
            tube.transform.position = center + new Vector3(0f, -0.3f, 0f);
            tube.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TubeMesh(TowerR, TowerH, 64), "GlassTube");
            tube.AddComponent<MeshRenderer>().sharedMaterial = glass;

            // Glowing white pillars (bloom turns them into soft light bars).
            var pillarMat = EmissiveMat("Pillar", new Color(1f, 0.97f, 0.98f), 1.6f);
            var trimMat = EmissiveMat("PillarTrim", new Color(1f, 0.8f, 0.9f), 1.1f);
            const int pillars = 18;
            for (int i = 0; i < pillars; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / pillars;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p = Box("Pillar", center + dir * (TowerR - 0.05f) + Vector3.up * (TowerH / 2f - 0.3f),
                            new Vector3(0.38f, TowerH, 0.38f), pillarMat);
                p.transform.SetParent(root, true);
                p.transform.rotation = Quaternion.LookRotation(dir);
                Object.DestroyImmediate(p.GetComponent<Collider>());
            }
            // Horizontal glowing rings at a few heights
            foreach (float ry in new[] { 0.05f, 12f, 24f, TowerH - 0.4f })
            {
                var ring = new GameObject("TowerRing");
                ring.transform.SetParent(root, false);
                ring.transform.position = center + new Vector3(0f, ry - 0.3f, 0f);
                ring.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TubeMesh(TowerR + 0.02f, 0.22f, 64), "TowerRing");
                ring.AddComponent<MeshRenderer>().sharedMaterial = trimMat;
            }

            BuildFloatingCandy(center, root);
            BuildSkyCandy(center, root);
        }

        static Material CheckerFloorMat()
        {
            var m = Mat("RoomFloor", Color.white, 1f, 0.55f);
            m.mainTexture = CheckerTexture();
            m.mainTextureScale = new Vector2(7f, 7f);
            m.SetFloat("_RimStrength", 0.15f);
            return m;
        }

        // ---------------------------------------------------------------- candy

        static Material PropMat(string name, Color c, float gloss, float rim = 0.4f, Texture tex = null, float emit = 0f)
        {
            if (propMats.TryGetValue(name, out var m)) return m;
            m = Mat(name, c, 1f, gloss);
            m.SetFloat("_RimStrength", rim);
            if (tex != null) m.mainTexture = tex;
            if (emit > 0f) m.SetColor("_EmissionColor", c * emit);
            propMats[name] = m;
            return m;
        }

        static GameObject Ball(Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp; g.transform.localScale = ls;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp; g.transform.localScale = ls;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static Transform CandyProp(int kind, Vector3 pos, float scale, Transform parent)
        {
            var g = new GameObject("FloatingCandy").transform;
            g.SetParent(parent, false);
            g.position = pos;
            g.localScale = Vector3.one * scale;
            g.rotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            var eye = PropMat("CandyEye", new Color(0.12f, 0.08f, 0.1f), 0.9f, 0f);
            var blush = PropMat("CandyBlush", new Color(1f, 0.55f, 0.65f), 0.2f, 0f);
            switch (kind)
            {
                case 0: // grape cluster
                {
                    var grape = PropMat("Grape", new Color(0.58f, 0.32f, 0.82f), 0.85f, 0.6f);
                    var rows = new[] { 4, 3, 2, 1 };
                    float y = 0f;
                    foreach (int n in rows)
                    {
                        for (int k = 0; k < n; k++)
                        {
                            float a = k * Mathf.PI * 2f / n + y;
                            float r = n == 1 ? 0f : 0.32f;
                            Ball(g, "Grape", new Vector3(Mathf.Cos(a) * r, -y, Mathf.Sin(a) * r), new Vector3(0.46f, 0.5f, 0.46f), grape);
                        }
                        y += 0.36f;
                    }
                    Cyl(g, "Stem", new Vector3(0f, 0.38f, 0f), new Vector3(0.06f, 0.18f, 0.06f), PropMat("Stem", new Color(0.45f, 0.62f, 0.25f), 0.3f, 0.1f));
                    break;
                }
                case 1: // apple
                {
                    bool green = Random.value < 0.5f;
                    var skin = green ? PropMat("AppleGreen", new Color(0.62f, 0.9f, 0.35f), 0.9f, 0.45f)
                                     : PropMat("AppleRed", new Color(0.95f, 0.25f, 0.32f), 0.9f, 0.45f);
                    Ball(g, "Apple", Vector3.zero, new Vector3(1f, 0.9f, 1f), skin);
                    Cyl(g, "Stem", new Vector3(0f, 0.52f, 0f), new Vector3(0.06f, 0.14f, 0.06f), PropMat("AppleStem", new Color(0.45f, 0.3f, 0.2f), 0.2f, 0f));
                    var leaf = Ball(g, "Leaf", new Vector3(0.16f, 0.6f, 0f), new Vector3(0.32f, 0.05f, 0.16f), PropMat("Leaf", new Color(0.4f, 0.78f, 0.35f), 0.6f, 0.3f));
                    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    break;
                }
                case 2: // marshmallow with a face
                {
                    bool pink = Random.value < 0.5f;
                    var mm = pink ? PropMat("MallowPink", new Color(1f, 0.78f, 0.86f), 0.25f, 0.5f)
                                  : PropMat("MallowWhite", new Color(1f, 0.97f, 0.95f), 0.25f, 0.5f);
                    var body = Box("Marshmallow", Vector3.zero, new Vector3(0.9f, 0.85f, 0.9f), mm);
                    Object.DestroyImmediate(body.GetComponent<Collider>());
                    body.transform.SetParent(g, false);
                    Roundify(body, 0.32f);
                    Ball(g, "Eye", new Vector3(-0.17f, 0.06f, -0.45f), new Vector3(0.1f, 0.13f, 0.05f), eye);
                    Ball(g, "Eye", new Vector3(0.17f, 0.06f, -0.45f), new Vector3(0.1f, 0.13f, 0.05f), eye);
                    Ball(g, "Blush", new Vector3(-0.28f, -0.06f, -0.44f), new Vector3(0.13f, 0.07f, 0.04f), blush);
                    Ball(g, "Blush", new Vector3(0.28f, -0.06f, -0.44f), new Vector3(0.13f, 0.07f, 0.04f), blush);
                    break;
                }
                case 3: // glitter ball
                {
                    var cols = new[] { new Color(1f, 0.62f, 0.82f), new Color(0.75f, 0.68f, 1f), new Color(0.6f, 0.9f, 1f), new Color(1f, 0.85f, 0.5f) };
                    int ci = Random.Range(0, cols.Length);
                    Ball(g, "GlitterBall", Vector3.zero, Vector3.one, PropMat("Glitter" + ci, cols[ci], 0.95f, 0.7f, GlitterTexture(), 0.25f));
                    break;
                }
                case 4: // macaron
                {
                    var cols = new[] { new Color(1f, 0.7f, 0.8f), new Color(0.7f, 0.93f, 0.78f), new Color(0.82f, 0.74f, 1f), new Color(1f, 0.86f, 0.6f) };
                    int ci = Random.Range(0, cols.Length);
                    var shell = PropMat("Macaron" + ci, cols[ci], 0.45f, 0.35f);
                    Ball(g, "Top", new Vector3(0f, 0.2f, 0f), new Vector3(1f, 0.38f, 1f), shell);
                    Ball(g, "Bottom", new Vector3(0f, -0.2f, 0f), new Vector3(1f, 0.38f, 1f), shell);
                    Cyl(g, "Cream", Vector3.zero, new Vector3(0.9f, 0.09f, 0.9f), PropMat("MacCream", new Color(1f, 0.97f, 0.92f), 0.3f, 0.2f));
                    g.localRotation *= Quaternion.Euler(Random.Range(20f, 60f), 0f, 0f);
                    break;
                }
                case 5: // strawberry
                {
                    Ball(g, "Berry", Vector3.zero, new Vector3(0.8f, 1f, 0.8f), PropMat("Strawberry", new Color(0.98f, 0.22f, 0.35f), 0.9f, 0.5f, SeedTexture()));
                    var leaf = PropMat("Leaf", new Color(0.4f, 0.78f, 0.35f), 0.6f, 0.3f);
                    for (int k = 0; k < 5; k++)
                    {
                        var l = Ball(g, "Calyx", Vector3.zero, new Vector3(0.14f, 0.04f, 0.4f), leaf);
                        l.transform.localRotation = Quaternion.Euler(0f, k * 72f, 0f);
                        l.transform.localPosition = l.transform.localRotation * new Vector3(0f, 0.48f, 0.14f);
                    }
                    break;
                }
                default: // gumdrop with sugar
                {
                    var cols = new[] { new Color(1f, 0.45f, 0.55f), new Color(1f, 0.7f, 0.3f), new Color(0.5f, 0.85f, 0.55f) };
                    int ci = Random.Range(0, cols.Length);
                    Ball(g, "Gumdrop", Vector3.zero, new Vector3(0.9f, 1f, 0.9f), PropMat("Gumdrop" + ci, cols[ci], 0.7f, 0.5f, GlitterTexture()));
                    break;
                }
            }
            return g;
        }

        static void BuildFloatingCandy(Vector3 center, Transform root)
        {
            // Inside the tower, between the staircase (r < 7.4) and the glass.
            int n = 26;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.1f, 0.1f);
                float r = Random.Range(8.6f, 12.4f);
                float y = Random.Range(0.8f, 15f);
                var p = CandyProp(i % 7, center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Random.Range(0.75f, 1.35f), root);
                var bob = p.gameObject.AddComponent<FloatBob>();
                bob.amplitude = Random.Range(0.15f, 0.35f); bob.speed = Random.Range(0.4f, 0.8f);
                bob.spin = Random.Range(-14f, 14f); bob.phase = Random.Range(0f, 6.28f);
            }
        }

        static void BuildSkyCandy(Vector3 center, Transform root)
        {
            // Giant candy outside the glass for depth.
            int n = 22;
            for (int i = 0; i < n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
                float r = Random.Range(20f, 34f);
                float y = Random.Range(-6f, 26f);
                var p = CandyProp(i % 7, center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), Random.Range(2.2f, 4.2f), root);
                var bob = p.gameObject.AddComponent<FloatBob>();
                bob.amplitude = Random.Range(0.4f, 0.9f); bob.speed = Random.Range(0.2f, 0.4f);
                bob.spin = Random.Range(-6f, 6f); bob.phase = Random.Range(0f, 6.28f);
            }
        }

        static Material SparkleParticleMat(bool additive = true)
        {
            var m = new Material(Shader.Find("Crunchee/Particle")) { name = additive ? "SparkleAdd" : "SparkleAlpha" };
            m.mainTexture = SparkleTexture();
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        static void NoSheen(Material m)
        {
            m.SetFloat("_RimStrength", 0f);
            m.SetFloat("_SpecStrength", 0f);
        }

        static Texture2D GlitterTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Glitter.png", 128, 128, (x, y, w, h) =>
            {
                float hsh = Mathf.Sin(x * 91.7f + y * 47.3f) * 43758.5453f; hsh -= Mathf.Floor(hsh);
                float v = hsh > 0.93f ? 1.25f : 0.9f + hsh * 0.1f;
                return new Color(v, v, v, 1f);
            });
        }

        static Texture2D SeedTexture()
        {
            return LoadOrCreateTexture("Assets/Textures/Seeds.png", 128, 128, (x, y, w, h) =>
            {
                int cx = (x / 16) * 16 + 8 + ((y / 16) % 2) * 4, cy = (y / 16) * 16 + 8;
                float d = Mathf.Sqrt((x - cx) * (x - cx) * 2.2f + (y - cy) * (y - cy));
                return d < 2.6f ? new Color(1f, 0.92f, 0.55f) : Color.white;
            });
        }

        // ---------------------------------------------------------------- meshes

        /// <summary>Swap a unit-cube primitive for a rounded box of the same
        /// world size. Rounding is built in world units, then un-scaled, so
        /// non-uniform parents don't stretch the bevels.</summary>
        static void Roundify(GameObject go, float radius = -1f)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null) return;
            var s = go.transform.lossyScale;
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            if (min < 1e-4f) return;
            float r = radius > 0f ? Mathf.Min(radius, min * 0.49f) : Mathf.Min(0.12f, min * 0.3f);
            string key = $"RBox_{s.x:0.###}_{s.y:0.###}_{s.z:0.###}_{r:0.###}";
            if (!meshCache.TryGetValue(key, out var mesh))
            {
                mesh = SaveMesh(RoundedBoxMesh(s, r, 4, s), key);
                meshCache[key] = mesh;
            }
            mf.sharedMesh = mesh;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            m.name = name;
            string path = $"{GeneratedMeshDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) return AssetDatabase.LoadAssetAtPath<Mesh>(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh RoundedBoxMesh(Vector3 size, float r, int seg, Vector3 unscale)
        {
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvs = new List<Vector2>(); var tris = new List<int>();
            float[] Axis(float half)
            {
                var c = new List<float>();
                for (int k = 0; k <= seg; k++) c.Add(-half + r * (1f - Mathf.Cos(k * Mathf.PI * 0.5f / seg)));
                for (int k = seg; k >= 0; k--) c.Add(half - r * (1f - Mathf.Cos(k * Mathf.PI * 0.5f / seg)));
                return c.ToArray();
            }
            float[] ax = Axis(h.x), ay = Axis(h.y), az = Axis(h.z);
            // face: normal axis n, in-plane axes u,v (right-handed so u x v = n)
            var faces = new (int n, float sgn, int u, int v)[]
            {
                (0, 1, 2, 1), (0, -1, 1, 2), (1, 1, 0, 2), (1, -1, 2, 0), (2, 1, 1, 0), (2, -1, 0, 1)
            };
            float[][] axes = { ax, ay, az };
            foreach (var f in faces)
            {
                float[] au = axes[f.u], av = axes[f.v];
                int baseIdx = verts.Count;
                for (int j = 0; j < av.Length; j++)
                    for (int i = 0; i < au.Length; i++)
                    {
                        var p = Vector3.zero;
                        p[f.n] = h[f.n] * f.sgn; p[f.u] = au[i]; p[f.v] = av[j];
                        var inner = new Vector3(Mathf.Clamp(p.x, -h.x + r, h.x - r), Mathf.Clamp(p.y, -h.y + r, h.y - r), Mathf.Clamp(p.z, -h.z + r, h.z - r));
                        var d = p - inner;
                        var nrm = d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.zero;
                        if (nrm == Vector3.zero) nrm[f.n] = f.sgn;
                        var pos = inner + nrm * r;
                        verts.Add(new Vector3(pos.x / unscale.x, pos.y / unscale.y, pos.z / unscale.z));
                        norms.Add(new Vector3(nrm.x * unscale.x, nrm.y * unscale.y, nrm.z * unscale.z).normalized);
                        uvs.Add(new Vector2((au[i] + h[f.u]) / (2f * h[f.u]), (av[j] + h[f.v]) / (2f * h[f.v])));
                    }
                int w = au.Length;
                for (int j = 0; j < av.Length - 1; j++)
                    for (int i = 0; i < w - 1; i++)
                    {
                        int a = baseIdx + j * w + i, b = a + 1, c = a + w, d2 = c + 1;
                        if (f.sgn > 0) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d2); }
                        else { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d2); tris.Add(c); }
                    }
            }
            var mesh = new Mesh();
            mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        static Mesh TubeMesh(float radius, float height, int segs)
        {
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int i = 0; i <= segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                verts.Add(n * radius); verts.Add(n * radius + Vector3.up * height);
                norms.Add(n); norms.Add(n);
                uvs.Add(new Vector2(i / (float)segs, 0f)); uvs.Add(new Vector2(i / (float)segs, 1f));
                if (i < segs)
                {
                    int b = i * 2;
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3);
                }
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }
}
#endif
