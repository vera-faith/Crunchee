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
        // Wax-plate gap inks: shadowed depth, the butter showing through the
        // split, and the bright broken edge of the wax itself.
        static readonly Color GapShadow = new Color(0.45f, 0.28f, 0.1f, 0.6f);
        static readonly Color GapButter = new Color(0.99f, 0.8f, 0.34f, 0.92f);
        static readonly Color GapEdge = new Color(1f, 1f, 1f, 0.85f);
        readonly List<Vector2> crackNodes = new List<Vector2>();
        readonly HashSet<long> crackEdges = new HashSet<long>();
        int sparkleCounter;
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
                    float grow = 0.95f + 0.5f * sink;
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
            if (coating == Coating.Wax) SpawnStepChips(worldPos); // pieces keep breaking off underfoot
            AsmrAudio.PlayStep(transform.position, baseMaterial, coating,
                               coating == Coating.Wax ? waxLayers : 0);
            if (coating == Coating.Wax && stepParticles != null) stepParticles.Emit(1);
        }

        /// <summary>
        /// Tiny wax chips pop off at her feet as she walks (Faith, Nov 3): the
        /// wax keeps breaking into smaller pieces, not just on the first crack.
        /// </summary>
        void SpawnStepChips(Vector3 at)
        {
            if (shardMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                shardMat = new Material(sh) { color = new Color(1f, 1f, 1f, 0.92f) };
            }
            int count = 2 + (waxLayers >= 5 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.GetComponent<MeshRenderer>().material = shardMat;
                Destroy(go.GetComponent<Collider>());
                float sz = Random.Range(0.03f, 0.065f);
                go.transform.localScale = new Vector3(sz, sz * 0.4f, sz);
                go.transform.position = at + new Vector3(Random.Range(-0.16f, 0.16f), TopY + 0.03f, Random.Range(-0.16f, 0.16f));
                shards.Add(new Shard
                {
                    go = go,
                    vel = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(1.0f, 1.9f), Random.Range(-0.8f, 0.8f)),
                    spin = Random.Range(-10f, 10f),
                    life = Random.Range(0.35f, 0.5f),
                    age = 0f,
                    baseScale = go.transform.localScale
                });
            }
        }

        // --- Crack drawing (Faith, Nov 3 round 3, from the reference videos):
        // the cracks are the WAX LAYER ITSELF splitting into polygonal plates.
        // Nodes land on the surface and connect with straight borders; every
        // border is drawn as a gap in the wax - shadowed depth, golden butter
        // showing through, and a bright rim where the wax edge broke. Walking
        // adds nodes along her real path, so the plates get SMALLER the more
        // she walks. Nothing is placed at random. ---

        Vector2 WorldToCrackPx(Vector3 worldPos)
        {
            var lp = crackDecal.transform.InverseTransformPoint(worldPos);
            return new Vector2((lp.x / 10f + 0.5f) * (CrackW - 1), (lp.z / 10f + 0.5f) * (CrackH - 1));
        }

        bool InCrackRange(Vector2 p) => p.x > -24f && p.x < CrackW + 24f && p.y > -24f && p.y < CrackH + 24f;

        void StampPx(float x, float y, float radius, Color ink)
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
                    float fall = 1f - (d / Mathf.Max(0.001f, radius)) * 0.55f;
                    float a = Mathf.Min(1f, ink.a * fall);
                    var cur = crackTex.GetPixel(X, Y);
                    cur.r = Mathf.Lerp(cur.r, ink.r, a);
                    cur.g = Mathf.Lerp(cur.g, ink.g, a);
                    cur.b = Mathf.Lerp(cur.b, ink.b, a);
                    cur.a = Mathf.Max(cur.a, a);
                    crackTex.SetPixel(X, Y, cur);
                }
        }

        void DrawGapPass(Vector2 a, Vector2 b, Color ink, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                StampPx(p.x, p.y, radius, ink);
            }
        }

        /// <summary>One plate border: a wide shadowed gap, butter inside it, white wax rim.</summary>
        void DrawPlateGap(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            if (len < 2f) return;
            var mid = (a + b) * 0.5f;
            var nrm = new Vector2(-(b.y - a.y), b.x - a.x) / len;
            mid += nrm * Random.Range(-len * 0.045f, len * 0.045f); // nearly straight, slight irregularity
            DrawGapPass(a, mid, GapShadow, 5.2f); DrawGapPass(mid, b, GapShadow, 5.2f);
            DrawGapPass(a, mid, GapButter, 3.0f); DrawGapPass(mid, b, GapButter, 3.0f);
            var off = new Vector2(1.7f, 1.7f);
            DrawGapPass(a + off, mid + off, GapEdge, 1.1f); DrawGapPass(mid + off, b + off, GapEdge, 1.1f);
            // A long border sometimes sprouts one short T-stub - real plates
            // micro-crack off their edges. It grows FROM the border, so it
            // never reads as a random mark.
            if (len > 70f && Random.value < 0.45f)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                var tip = mid + nrm * side * Random.Range(13f, 19f);
                DrawGapPass(mid, tip, GapButter, 1.8f);
                DrawGapPass(mid + off, tip + off, GapEdge, 0.9f);
            }
        }

        void StampSparkle(Vector2 c)
        {
            var star = new Color(1f, 1f, 1f, 0.95f);
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                StampPx(c.x + i * 2f, c.y, 0.9f, star);
                StampPx(c.x, c.y + i * 2f, 0.9f, star);
            }
            StampPx(c.x, c.y, 1.7f, star);
        }

        int AddNode(Vector2 p)
        {
            p.x = Mathf.Clamp(p.x, 8f, CrackW - 9f);
            p.y = Mathf.Clamp(p.y, 8f, CrackH - 9f);
            crackNodes.Add(p);
            return crackNodes.Count - 1;
        }

        static long EdgeKey(int a, int b) => a < b ? (long)a * 4096 + b : (long)b * 4096 + a;

        void ConnectNodes(int a, int b)
        {
            if (a == b || !crackEdges.Add(EdgeKey(a, b))) return;
            DrawPlateGap(crackNodes[a], crackNodes[b]);
        }

        /// <summary>
        /// The landing: the wax under her shatters into a ring of polygonal
        /// plates - spokes from the impact point plus a connected ring, with a
        /// few longer spokes reaching out (the video's cracked-plate look).
        /// </summary>
        public void AddNetworkCrack(Vector3 worldPos, float radiusWorld)
        {
            if (crackTex == null || crackDecal == null) return;
            var c = WorldToCrackPx(worldPos);
            if (!InCrackRange(c)) { hasTrail = false; return; }
            crackNodes.Clear(); crackEdges.Clear();
            float pxPerUnit = Vector2.Distance(c, WorldToCrackPx(worldPos + Vector3.right));
            float rad1 = radiusWorld * pxPerUnit * 0.55f;
            int ci = AddNode(c);
            const int R1 = 7;
            var ring = new int[R1];
            float baseAng = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < R1; i++)
            {
                float ang = baseAng + i * (Mathf.PI * 2f / R1) + Random.Range(-0.16f, 0.16f);
                float rr = rad1 * Random.Range(0.85f, 1.15f);
                ring[i] = AddNode(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr);
            }
            for (int i = 0; i < R1; i++)
            {
                ConnectNodes(ci, ring[i]);                    // spokes
                ConnectNodes(ring[i], ring[(i + 1) % R1]);    // the ring closes the plates
            }
            for (int i = 0; i < R1; i += 2)                   // longer reach on a few spokes
            {
                float ang = baseAng + i * (Mathf.PI * 2f / R1) + 0.45f;
                int outer = AddNode(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad1 * 1.9f);
                ConnectNodes(ring[i], outer);
            }
            StampSparkle(c);
            crackTex.Apply();
            trailPx = c; hasTrail = true;
        }

        /// <summary>
        /// Each step drops a node on her path and ties it to the two nearest
        /// nodes, so the plate network grows along exactly where she walks -
        /// re-walked areas subdivide into smaller and smaller pieces.
        /// </summary>
        public void AddTrailCrack(Vector3 worldPos)
        {
            if (crackTex == null || crackDecal == null) return;
            var p = WorldToCrackPx(worldPos);
            if (!InCrackRange(p)) { hasTrail = false; return; }
            if (hasTrail && Vector2.Distance(p, trailPx) < 15f) { trailPx = p; return; } // tiny shuffle: no new plate
            if (crackNodes.Count > 240) { trailPx = p; hasTrail = true; return; }
            int ni = AddNode(p);
            int n1 = -1, n2 = -1; float d1 = 115f, d2 = 115f;
            for (int i = 0; i < crackNodes.Count - 1; i++)
            {
                float d = Vector2.Distance(p, crackNodes[i]);
                if (d < d1) { d2 = d1; n2 = n1; d1 = d; n1 = i; }
                else if (d < d2) { d2 = d; n2 = i; }
            }
            if (n1 >= 0) ConnectNodes(ni, n1);
            if (n2 >= 0) ConnectNodes(ni, n2);
            if ((sparkleCounter++ % 4) == 0) StampSparkle(p);
            crackTex.Apply();
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
