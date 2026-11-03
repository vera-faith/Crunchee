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
        public MeshRenderer dentDecal;         // soft shadow disc that follows her - the visible dent she sinks into

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
        const int CrackW = 512, CrackH = 256;
        static readonly Color CrackInk = new Color(0.15f, 0.09f, 0.07f, 0.9f);
        Vector2 trailPx; bool hasTrail;
        Material dentMat; Vector3 dentBaseScale; float dentShown;
        Vector3 trackPos; float trackTime = -10f;

        public bool Cracked => cracked;
        public float TopY => col != null ? col.bounds.max.y : transform.position.y;

        void Awake()
        {
            profile = overrideProfile ? profileOverride : SquishProfile.DefaultFor(baseMaterial, coating);
            restScale = transform.localScale;
            col = GetComponent<Collider>();
            if (crackDecal != null)
            {
                crackTex = new Texture2D(CrackW, CrackH, TextureFormat.RGBA32, false);
                crackTex.SetPixels32(new Color32[CrackW * CrackH]); // fully transparent
                crackTex.Apply();
                crackDecal.material.mainTexture = crackTex;
            }
            if (dentDecal != null)
            {
                dentMat = dentDecal.material;
                dentBaseScale = dentDecal.transform.localScale;
                dentDecal.enabled = false;
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

            // The dent: a soft pressed-in shadow that FOLLOWS her across the
            // squishy and deepens with the sink, so you can see her sinking
            // into the surface wherever she stands or walks (Faith, Nov 3).
            if (dentDecal != null && dentMat != null)
            {
                bool show = occupied && Time.time - trackTime < 0.25f;
                dentShown = Mathf.MoveTowards(dentShown, show ? 1f : 0f, Time.deltaTime * 5f);
                if (dentShown > 0.01f)
                {
                    dentDecal.enabled = true;
                    var lp = transform.InverseTransformPoint(trackPos);
                    lp.x = Mathf.Clamp(lp.x, -0.46f, 0.46f);
                    lp.z = Mathf.Clamp(lp.z, -0.46f, 0.46f);
                    var dp = dentDecal.transform.localPosition;
                    dentDecal.transform.localPosition = new Vector3(lp.x, dp.y, lp.z);
                    float grow = 0.95f + 0.4f * sink;
                    dentDecal.transform.localScale = new Vector3(dentBaseScale.x * grow, dentBaseScale.y, dentBaseScale.z * grow);
                    var dc = dentMat.color;
                    dc.a = 0.9f * dentShown * (0.3f + 0.7f * sink);
                    dentMat.color = dc;
                }
                else dentDecal.enabled = false;
            }

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

        /// <summary>Player reports her position every frame while on this squishy (drives the dent).</summary>
        public void TrackPlayer(Vector3 worldPos) { trackPos = worldPos; trackTime = Time.time; }

        /// <summary>First touch: crack + sound + particles, then the sink takes over.</summary>
        public void OnLanded(float impactSpeed, Vector3 worldPos)
        {
            if (!cracked)
            {
                cracked = true;
                occupied = true;
                sink = Mathf.Max(sink, 0.3f); // the initial give as weight lands
                CrackShell();
                AddNetworkCrack(worldPos, 1.05f); // the big bold crack network under the landing
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
        /// One footstep while walking around on this squishy (Faith, Nov 3): the
        /// crack TRAIL connects from her last step to this one, so ALL of her
        /// movement shows on the wax; the sound is the soft squish of the
        /// squishy itself with a wax crackle layered over it (more crackle on
        /// thicker wax).
        /// </summary>
        public void OnStep(Vector3 worldPos)
        {
            if (!cracked) { OnLanded(0f, worldPos); return; }
            AddTrailCrack(worldPos);
            AsmrAudio.PlayStep(transform.position, baseMaterial, coating,
                               coating == Coating.Wax ? waxLayers : 0);
            if (coating == Coating.Wax && stepParticles != null) stepParticles.Emit(1);
        }

        // --- Crack drawing (Faith, Nov 3, from the reference videos): cracks are
        // BOLD dark jagged strokes forming a connected network - a big branching
        // burst on landing, then a continuous trail connecting every step, so
        // all of her movement shows on the wax. Not faint random marks. ---

        Vector2 WorldToCrackPx(Vector3 worldPos)
        {
            var lp = crackDecal.transform.InverseTransformPoint(worldPos);
            return new Vector2((lp.x / 10f + 0.5f) * (CrackW - 1), (lp.z / 10f + 0.5f) * (CrackH - 1));
        }

        bool InCrackRange(Vector2 p) => p.x > -24f && p.x < CrackW + 24f && p.y > -24f && p.y < CrackH + 24f;

        void StampPx(float x, float y, float radius, float alphaScale)
        {
            int r = Mathf.CeilToInt(radius);
            int cx = Mathf.RoundToInt(x), cy = Mathf.RoundToInt(y);
            for (int ox = -r; ox <= r; ox++)
                for (int oy = -r; oy <= r; oy++)
                {
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    if (d > radius) continue;
                    int X = cx + ox, Y = cy + oy;
                    if (X < 0 || X >= CrackW || Y < 0 || Y >= CrackH) continue;
                    float fall = 1f - d / Mathf.Max(0.001f, radius);
                    float a = CrackInk.a * alphaScale * (0.5f + 0.5f * fall);
                    var cur = crackTex.GetPixel(X, Y);
                    crackTex.SetPixel(X, Y, new Color(CrackInk.r, CrackInk.g, CrackInk.b, Mathf.Max(cur.a, a)));
                }
        }

        void StampLine(Vector2 a, Vector2 b, float coreRadius)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) / 2f);
            for (int pass = 0; pass < 2; pass++)
            {
                float r = pass == 0 ? coreRadius * 2.0f : coreRadius;   // soft dark halo, then firm core
                float aScale = pass == 0 ? 0.30f : 1f;
                for (int i = 0; i <= steps; i++)
                {
                    var p = Vector2.Lerp(a, b, steps == 0 ? 0f : i / (float)steps);
                    StampPx(p.x, p.y, r, aScale);
                }
            }
        }

        void DrawCrackSeg(Vector2 a, Vector2 b, float coreRadius)
        {
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 10f));
            var dir = len > 0.01f ? (b - a) / len : Vector2.right;
            var perp = new Vector2(-dir.y, dir.x);
            var prev = a;
            for (int i = 1; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)n);
                if (i < n) p += perp * Random.Range(-4f, 4f); // jagged wander
                StampLine(prev, p, coreRadius);
                prev = p;
            }
        }

        /// <summary>The landing burst: 9 long jagged branches + sub-branches, a real crack network.</summary>
        public void AddNetworkCrack(Vector3 worldPos, float radiusWorld)
        {
            if (crackTex == null || crackDecal == null) return;
            var c = WorldToCrackPx(worldPos);
            if (!InCrackRange(c)) { hasTrail = false; return; }
            for (int i = 0; i < 9; i++)
            {
                float ang = i / 9f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float len = radiusWorld * Random.Range(0.65f, 1.1f);
                var dirW = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                DrawCrackSeg(c, WorldToCrackPx(worldPos + dirW * len), 3.1f);
                float bang = ang + Random.Range(0.5f, 1.0f) * (Random.value < 0.5f ? -1f : 1f);
                var midW = worldPos + dirW * len * 0.55f;
                var bendW = midW + new Vector3(Mathf.Cos(bang), 0f, Mathf.Sin(bang)) * len * 0.4f;
                DrawCrackSeg(WorldToCrackPx(midW), WorldToCrackPx(bendW), 2.1f);
            }
            StampPx(c.x, c.y, 7f, 1f); // chipped center
            crackTex.Apply();
            trailPx = c; hasTrail = true;
        }

        /// <summary>Connect this step to the last one with a bold jagged crack line.</summary>
        public void AddTrailCrack(Vector3 worldPos)
        {
            if (crackTex == null || crackDecal == null) return;
            var p = WorldToCrackPx(worldPos);
            if (!InCrackRange(p)) { hasTrail = false; return; }
            if (hasTrail && Vector2.Distance(p, trailPx) < 140f)
            {
                DrawCrackSeg(trailPx, p, 2.6f);
                if (Random.value < 0.6f) // short branchlet off the trail
                {
                    float ang = Random.Range(0f, Mathf.PI * 2f);
                    DrawCrackSeg(p, p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(10f, 22f), 1.7f);
                }
                crackTex.Apply();
            }
            trailPx = p; hasTrail = true;
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
