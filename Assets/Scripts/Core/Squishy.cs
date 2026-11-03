using System.Collections.Generic;
using UnityEngine;
using Crunchee.Audio;

namespace Crunchee.Core
{
    /// <summary>
    /// The heart of Crunchee. Faith's gameplay (from the reference videos):
    /// ONE landing per squishy. On that first landing the coating CRACKS
    /// (wax / nail-polish shells split into pieces, shards fly, sound plays),
    /// then the character slowly SINKS into the squishy while standing on it.
    /// Stepping off lets it recover slowly with a soft wobble. Re-landing on an
    /// already-cracked squishy is just a soft thud - no repeat crack, no bounce.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Squishy : MonoBehaviour
    {
        public BaseMaterial baseMaterial = BaseMaterial.Butter;
        public Coating coating = Coating.Plain;

        [Tooltip("Wax layers (only used when coating = Wax). Butter Path uses 1..10 along the path: more layers = thicker/louder/longer crack, more flakes.")]
        [Range(1, 10)] public int waxLayers = 1;

        [Header("Optional overrides (leave off to use design-table defaults)")]
        public bool overrideProfile = false;
        public SquishProfile profileOverride;

        public ParticleSystem stepParticles;   // burst unique to material x coating
        public GameObject coatingShell;        // shell root; wax/polish hold crackable pieces as children
        public MeshRenderer crackDecal;        // plane on the shell top; cracks are drawn into its texture underfoot

        SquishProfile profile;
        Vector3 restScale;
        Collider col;
        bool cracked, occupied;
        float sink;          // 0..1 how deep the character has sunk in
        float sinkVel;       // for the springy recovery wobble
        float crackAnim;     // 0..1 shell-split animation
        Transform[] shellPieces;
        Vector3[] pieceHomePos, pieceTargetPos;
        Quaternion[] pieceHomeRot, pieceTargetRot;
        readonly List<Shard> shards = new List<Shard>();
        static Material shardMat;
        Texture2D crackTex;

        public bool Cracked => cracked;
        public float TopY => col != null ? col.bounds.max.y : transform.position.y;

        void Awake()
        {
            profile = overrideProfile ? profileOverride : SquishProfile.DefaultFor(baseMaterial, coating);
            restScale = transform.localScale;
            col = GetComponent<Collider>();
            if (crackDecal != null)
            {
                crackTex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                crackTex.SetPixels32(new Color32[256 * 256]); // fully transparent
                crackTex.Apply();
                crackDecal.material.mainTexture = crackTex;
            }
            if (coatingShell != null && (coating == Coating.Wax || coating == Coating.NailPolish))
            {
                int n = coatingShell.transform.childCount;
                if (n > 0)
                {
                    shellPieces = new Transform[n];
                    pieceHomePos = new Vector3[n]; pieceTargetPos = new Vector3[n];
                    pieceHomeRot = new Quaternion[n]; pieceTargetRot = new Quaternion[n];
                    for (int i = 0; i < n; i++)
                    {
                        var t = coatingShell.transform.GetChild(i);
                        shellPieces[i] = t;
                        pieceHomePos[i] = t.localPosition; pieceHomeRot[i] = t.localRotation;
                        pieceTargetPos[i] = t.localPosition; pieceTargetRot[i] = t.localRotation;
                    }
                }
            }
        }

        void Update()
        {
            if (occupied)
            {
                // Slow sink-in: full depth over ~0.9 s, no bounce.
                sink = Mathf.MoveTowards(sink, 1f, Time.deltaTime / 0.9f);
                sinkVel = 0f;
            }
            else if (sink > 0f || Mathf.Abs(sinkVel) > 0.001f)
            {
                // Released: spring back slowly with a soft overshoot wobble.
                float stiff = 22f, damp = 6.0f;
                sinkVel += (-stiff * sink - damp * sinkVel) * Time.deltaTime;
                sink += sinkVel * Time.deltaTime;
                if (sink < 0.004f && Mathf.Abs(sinkVel) < 0.008f) { sink = 0f; sinkVel = 0f; }
            }
            float s = Mathf.Clamp(sink, -0.12f, 1f);
            float y = 1f - profile.squishDepth * s;
            float xz = 1f + (1f - y) * 0.55f; // volume-ish bulge sideways as it sinks
            transform.localScale = new Vector3(restScale.x * xz, restScale.y * y, restScale.z * xz);

            // Shell-split animation
            if (shellPieces != null && crackAnim < 1f && cracked)
            {
                crackAnim = Mathf.Min(1f, crackAnim + Time.deltaTime / 0.3f);
                float e = 1f - (1f - crackAnim) * (1f - crackAnim);
                for (int i = 0; i < shellPieces.Length; i++)
                {
                    shellPieces[i].localPosition = Vector3.Lerp(pieceHomePos[i], pieceTargetPos[i], e);
                    shellPieces[i].localRotation = Quaternion.Slerp(pieceHomeRot[i], pieceTargetRot[i], e);
                }
            }
            for (int i = shards.Count - 1; i >= 0; i--)
                if (shards[i].Tick(Time.deltaTime)) { Destroy(shards[i].go); shards.RemoveAt(i); }
        }

        /// <summary>Called by the player while standing on this squishy (drives the sink).</summary>
        public void SetOccupied(bool value) => occupied = value;

        /// <summary>First touch: crack + sound + particles, then the sink takes over.</summary>
        public void OnLanded(float impactSpeed, Vector3 worldPos)
        {
            if (!cracked)
            {
                cracked = true;
                occupied = true;
                sink = Mathf.Max(sink, 0.3f); // the initial give as weight lands
                CrackShell();
                AddFootCrack(worldPos, 0.85f); // the big first crack star under the landing
                AsmrAudio.PlaySquish(baseMaterial, coating, transform.position,
                                     coating == Coating.Wax ? waxLayers : 1);
                if (stepParticles != null)
                {
                    if (coating == Coating.Wax) stepParticles.Emit(6 + waxLayers * 4);
                    else stepParticles.Emit(14);
                }
#if UNITY_IOS || UNITY_ANDROID
                Handheld.Vibrate(); // placeholder: graded haptics later (strength = profile.hapticStrength)
#endif
            }
            else
            {
                // Already cracked: just a soft re-land thud, nothing re-triggers.
                AsmrAudio.PlaySoftBase(transform.position);
            }
        }

        /// <summary>
        /// One footstep while walking around on this squishy (Faith, Nov 2): the
        /// wax keeps cracking - a crack star appears under the foot and a short
        /// crackle plays, so moving around a butter audibly and visibly cracks
        /// more and more of its surface.
        /// </summary>
        public void OnStep(Vector3 worldPos)
        {
            if (!cracked) { OnLanded(0f, worldPos); return; }
            if (coating == Coating.Wax)
            {
                AddFootCrack(worldPos, 0.3f + waxLayers * 0.014f);
                AsmrAudio.PlayStepCrack(transform.position, waxLayers);
                if (stepParticles != null) stepParticles.Emit(2);
            }
            else AsmrAudio.PlaySoftBase(transform.position);
        }

        /// <summary>Draw a jagged crack star into the decal texture at a world position.</summary>
        public void AddFootCrack(Vector3 worldPos, float radiusWorld)
        {
            if (crackTex == null || crackDecal == null) return;
            var lp = crackDecal.transform.InverseTransformPoint(worldPos);
            float u = lp.x / 10f + 0.5f, v = lp.z / 10f + 0.5f;
            if (u < -0.1f || u > 1.1f || v < -0.1f || v > 1.1f) return;
            int cx = Mathf.RoundToInt(u * 255f), cy = Mathf.RoundToInt(v * 255f);
            float radPx = radiusWorld / Mathf.Max(0.1f, crackDecal.bounds.size.x) * 256f;
            var ink = new Color(0.23f, 0.2f, 0.28f, 0.62f);
            for (int r = 0; r < 6; r++)
            {
                float a = r / 6f * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                float len = radPx * Random.Range(0.55f, 1.05f);
                float x = cx, y = cy;
                int segs = Mathf.Max(2, Mathf.RoundToInt(len / 7f));
                for (int sgi = 0; sgi < segs; sgi++)
                {
                    a += Random.Range(-0.45f, 0.45f);
                    float nx = x + Mathf.Cos(a) * 7f, ny = y + Mathf.Sin(a) * 7f;
                    DrawSeg(x, y, nx, ny, ink);
                    x = nx; y = ny;
                }
            }
            DrawSeg(cx - 2, cy, cx + 2, cy, ink);
            DrawSeg(cx, cy - 2, cx, cy + 2, ink);
            crackTex.Apply();
        }

        void DrawSeg(float x0, float y0, float x1, float y1, Color ink)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps == 0 ? 0f : i / (float)steps;
                int px = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int py = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        if (Mathf.Abs(ox) + Mathf.Abs(oy) == 2) continue; // diamond brush
                        int X = px + ox, Y = py + oy;
                        if (X < 0 || X > 255 || Y < 0 || Y > 255) continue;
                        var cur = crackTex.GetPixel(X, Y);
                        crackTex.SetPixel(X, Y, new Color(ink.r, ink.g, ink.b, Mathf.Max(cur.a, ink.a)));
                    }
            }
        }

        void CrackShell()
        {
            if (coatingShell == null) return;
            if (shellPieces != null && shellPieces.Length > 0)
            {
                // Piece locals live under the (scaled) step, so convert the world-space
                // crack offset into shell-local space.
                var ls = coatingShell.transform.lossyScale;
                Vector3 off = new Vector3(0.1f / Mathf.Max(0.01f, Mathf.Abs(ls.x)), -0.05f / Mathf.Max(0.01f, Mathf.Abs(ls.y)), 0.1f / Mathf.Max(0.01f, Mathf.Abs(ls.z)));
                for (int i = 0; i < shellPieces.Length; i++)
                {
                    var home = pieceHomePos[i];
                    float sx = home.x >= 0f ? 1f : -1f;
                    float sz = home.z >= 0f ? 1f : -1f;
                    pieceTargetPos[i] = home + new Vector3(sx * off.x, off.y, sz * off.z);
                    pieceTargetRot[i] = pieceHomeRot[i] * Quaternion.Euler(sz * 17f, 0f, -sx * 17f);
                }
                crackAnim = 0f;
                SpawnShards();
            }
            else if (coating == Coating.NailPolish)
            {
                coatingShell.SetActive(false); // fallback: single-piece polish shell flashes off
                SpawnShards();
            }
        }

        void SpawnShards()
        {
            if (shardMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                shardMat = new Material(sh) { color = new Color(1f, 1f, 1f, 0.92f) };
            }
            int count = coating == Coating.Wax ? 5 + waxLayers : 8;
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.GetComponent<MeshRenderer>().material = shardMat;
                Destroy(go.GetComponent<Collider>());
                float sz = Random.Range(0.05f, 0.11f) * (coating == Coating.Wax ? (0.7f + waxLayers * 0.06f) : 1f);
                go.transform.localScale = new Vector3(sz, sz * 0.4f, sz);
                go.transform.position = transform.position + new Vector3(Random.Range(-0.9f, 0.9f), TopY + 0.05f, Random.Range(-0.6f, 0.6f));
                shards.Add(new Shard
                {
                    go = go,
                    vel = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(1.6f, 3.0f), Random.Range(-1.2f, 1.2f)),
                    spin = Random.Range(-9f, 9f),
                    life = Random.Range(0.55f, 0.85f),
                    age = 0f,
                    baseScale = go.transform.localScale
                });
            }
        }

        class Shard
        {
            public GameObject go; public Vector3 vel; public float spin, life, age; public Vector3 baseScale;
            public bool Tick(float dt)
            {
                age += dt;
                vel.y -= 7.5f * dt;
                go.transform.position += vel * dt;
                go.transform.Rotate(spin * dt * 57f, spin * 0.7f * dt * 57f, 0f);
                float left = life - age;
                if (left < 0.2f) go.transform.localScale = baseScale * Mathf.Clamp01(left / 0.2f);
                return age >= life;
            }
        }
    }
}
