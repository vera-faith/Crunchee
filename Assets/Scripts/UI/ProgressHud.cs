using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Crunchee.Core;
using Crunchee.Game;

namespace Crunchee.UI
{
    /// <summary>
    /// Path progress (big build, Nov 3): ten pips across the top that fill gold
    /// as each butter cracks, plus the finish celebration - banner, jingle and
    /// a pastel confetti burst over the finish platform.
    /// </summary>
    public class ProgressHud : MonoBehaviour
    {
        public Image[] pips;
        public GameObject banner;

        Squishy[] squishies;
        Transform finishAt;
        bool finished;

        static readonly Color Filled = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color Empty = new Color(1f, 1f, 1f, 0.35f);

        void Start()
        {
            var list = new List<Squishy>(FindObjectsOfType<Squishy>());
            list.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));
            squishies = list.ToArray();
            var trig = FindObjectOfType<FinishTrigger>();
            if (trig != null)
            {
                finishAt = trig.transform;
                trig.OnFinish += Celebrate;
            }
            if (banner != null) banner.SetActive(false);
            Refresh();
        }

        void Update() { Refresh(); }

        void Refresh()
        {
            if (squishies == null || pips == null) return;
            for (int i = 0; i < pips.Length; i++)
                if (pips[i] != null)
                    pips[i].color = (i < squishies.Length && squishies[i].Cracked) ? Filled : Empty;
        }

        void Celebrate()
        {
            if (finished) return;
            finished = true;
            if (banner != null) banner.SetActive(true);
            Crunchee.Audio.AsmrAudio.PlayFinish();
            StartCoroutine(Confetti());
        }

        IEnumerator Confetti()
        {
            var pastel = new[]
            {
                new Color(1f, 0.65f, 0.75f), new Color(0.65f, 0.9f, 0.78f),
                new Color(0.8f, 0.75f, 0.98f), new Color(1f, 0.88f, 0.55f),
                new Color(1f, 0.8f, 0.6f),
            };
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Vector3 c = finishAt != null ? finishAt.position + Vector3.up * 0.6f : Vector3.up * 2f;
            var bits = new List<(GameObject go, Vector3 vel, float spin)>();
            for (int i = 0; i < 40; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                var m = new Material(sh) { color = pastel[i % pastel.Length] };
                go.GetComponent<MeshRenderer>().material = m;
                float sz = Random.Range(0.07f, 0.15f);
                go.transform.localScale = new Vector3(sz, sz * 0.4f, sz);
                go.transform.position = c + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0f, 0.5f), Random.Range(-1.2f, 1.2f));
                bits.Add((go, new Vector3(Random.Range(-1.6f, 1.6f), Random.Range(2.6f, 4.6f), Random.Range(-1.6f, 1.6f)), Random.Range(-9f, 9f)));
            }
            float t = 0f;
            while (t < 1.8f)
            {
                float dt = Time.deltaTime; t += dt;
                for (int i = 0; i < bits.Count; i++)
                {
                    var b = bits[i];
                    if (b.go == null) continue;
                    b.vel.y -= 5.5f * dt;
                    bits[i] = b;
                    b.go.transform.position += b.vel * dt;
                    b.go.transform.Rotate(b.spin * dt * 2f, b.spin * dt * 1.3f, b.spin * dt);
                }
                yield return null;
            }
            foreach (var (go, _, _) in bits) if (go != null) Destroy(go);
        }
    }
}
