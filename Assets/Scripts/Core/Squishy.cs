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
        [Tooltip("Smooth pre-crack glaze sheet; hidden the moment the plates appear so translucent seams never show.")]
        public GameObject seamlessShell;
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
        [Tooltip("Position along the course; drives the progress pips.")]
        public int pathOrder;
        [Tooltip("Course stage (1 = Butter Path, 2 = Candy Path); the pips show one stage at a time.")]
        public int stage = 1;
        [Tooltip("Shared material for flying wax chips (assigned by the builder so it ships in the build).")]
        public Material shardMaterial;
        [Tooltip("Frosted/stress-whitened wax variant the plates switch to on crack.")]
        public Material crackedShellMaterial;
        Material dentMat; Vector3 dentBaseScale; float dentShown;
        Vector3 trackPos; float trackTime = -10f;

        public bool Cracked => cracked;
        /// <summary>Depth of the soft bowl under her feet right now (drives the shader dent).</summary>
        public float DentDepth => (0.05f + profile.squishDepth * 0.28f) * Mathf.Clamp01(sink);
        public float TopY => col != null ? col.bounds.max.y : transform.position.y;

        void Awake()
        {
            profile = overrideProfile ? profileOverride : SquishProfile.DefaultFor(baseMaterial, coating);
            restScale = transform.localScale;
            col = GetComponent<Collider>();
            if (dentDecal != null)
            {
                dentMat = dentDecal.material;
                dentBaseScale = dentDecal.transform.localScale;
                dentDecal.enabled = false;
            }
            InitShellPieces();
        }

        /// <summary>Snapshot the shell's crackable plates and their home poses.
        /// Called from Awake in play mode; PreviewCrack also calls it lazily
        /// because Awake never runs in edit mode.</summary>
        void InitShellPieces()
        {
            if (shellPieces != null) return;
            if (coatingShell != null && (coating == Coating.Wax || coating == Coating.NailPolish || coating == Coating.Sugar))
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

        /// <summary>Edit-mode crack preview (CaptureTool review shots): runs the
        /// same CrackShell target math, then snaps the plates straight to their
        /// target pose - no animation, shards, particles, audio or haptics.</summary>
        public void PreviewCrack(Vector3 worldPos)
        {
            InitShellPieces();
            CrackShell(worldPos);
            cracked = true;
            crackAnim = 1f;
            if (shellPieces == null) return;
            for (int i = 0; i < shellPieces.Length; i++)
            {
                shellPieces[i].localPosition = pieceTargetPos[i];
                shellPieces[i].localRotation = pieceTargetRot[i];
            }
        }

        void Update()
        {
            // Soft jelly spring (Nov 6, "make them squish better"): she
            // presses it down with a little give-and-settle wobble, and it
            // springs back up with an overshoot when she leaves.
            {
                float target = occupied ? 1f : 0f;
                float stiff = occupied ? 14f : 22f, damp = occupied ? 4.2f : 6f;
                sinkVel += (stiff * (target - sink) - damp * sinkVel) * Time.deltaTime;
                sink += sinkVel * Time.deltaTime;
                if (!occupied && Mathf.Abs(sink) < 0.004f && Mathf.Abs(sinkVel) < 0.008f) { sink = 0f; sinkVel = 0f; }
            }
            float s = Mathf.Clamp(sink, -0.15f, 1.15f);
            float y = 1f - profile.squishDepth * 0.5f * s;
            float xz = 1f + (1f - y) * 0.6f; // volume-ish bulge sideways as it sinks
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
                sink = Mathf.Max(sink, 0.12f);
                sinkVel += 2.6f; // the quick initial give as her weight lands
                CrackShell(worldPos); // the wax shell breaks into plates, widest where she hit
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
        /// sound is the soft squish of the squishy itself with a wax crackle
        /// layered over it (more crackle on thicker wax) and tiny chips keep
        /// popping off underfoot. The surface itself stays clean - Faith,
        /// Nov 6: "no crack web marks on the butters".
        /// </summary>
        public void OnStep(Vector3 worldPos)
        {
            if (!cracked) { OnLanded(0f, worldPos); return; }
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
                shardMat = shardMaterial != null ? shardMaterial
                    : new Material(Shader.Find("Crunchee/Candy")) { color = new Color(1f, 1f, 1f, 0.92f) };
            }
            int count = 3 + (waxLayers >= 5 ? 1 : 0);
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

        /// <summary>
        /// The shell splits into its plates on first landing. Pieces nearest
        /// the impact slide out widest and tip hardest, so the crack visibly
        /// BLOOMS where she landed instead of shifting uniformly (Nov 6: this
        /// physical split is now the whole crack visual - no drawn marks).
        /// Piece locals live under the scaled step, so offsets are converted
        /// back through the shell's lossy scale.
        /// </summary>
        void CrackShell(Vector3 impact)
        {
            if (coatingShell == null) return;
            if (shellPieces != null && shellPieces.Length > 0)
            {
                // Swap the smooth sheet for the real plates at the split,
                // stress-whitened so they read as broken glassy wax.
                if (seamlessShell != null)
                {
                    seamlessShell.SetActive(false);
                    for (int i = 0; i < shellPieces.Length; i++)
                    {
                        shellPieces[i].gameObject.SetActive(true);
                        if (crackedShellMaterial != null)
                            shellPieces[i].GetComponent<MeshRenderer>().sharedMaterial = crackedShellMaterial;
                    }
                }
                var ls = coatingShell.transform.lossyScale;
                Vector3 imp = coatingShell.transform.InverseTransformPoint(impact);
                for (int i = 0; i < shellPieces.Length; i++)
                {
                    // Every piece slides radially OUT from the shell centre
                    // (works for the many-piece grid, not just 4 quadrants)
                    // and tips away as it goes. home/imp are in scaled units;
                    // multiply by ls to compare real distances and directions.
                    var home = pieceHomePos[i];
                    float hx = home.x * ls.x, hz = home.z * ls.z;
                    float ix = imp.x * ls.x, iz = imp.z * ls.z;
                    var dir = new Vector3(hx, 0f, hz);
                    if (dir.sqrMagnitude < 0.0004f) dir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
                    dir.Normalize();
                    float d = Vector2.Distance(new Vector2(hx, hz), new Vector2(ix, iz));
                    float near = Mathf.Clamp01(1.5f - d / 1.7f);       // 1 at impact, ~0 past ~2.5 units
                    // ~0.03 m gap minimum everywhere, blooming wider/deeper/
                    // harder-tilted near the landing - and chunkier the
                    // thicker the wax layer is.
                    float sep = Mathf.Lerp(0.03f, 0.14f + waxLayers * 0.006f, near);
                    float drop = Mathf.Lerp(0.02f, 0.05f + waxLayers * 0.005f, near);
                    float tilt = Mathf.Lerp(4f, 12f + waxLayers, near);
                    pieceTargetPos[i] = home + new Vector3(dir.x * sep / Mathf.Max(0.01f, Mathf.Abs(ls.x)),
                                                            -drop / Mathf.Max(0.01f, Mathf.Abs(ls.y)),
                                                            dir.z * sep / Mathf.Max(0.01f, Mathf.Abs(ls.z)));
                    pieceTargetRot[i] = pieceHomeRot[i] * Quaternion.Euler(dir.z * tilt, 0f, -dir.x * tilt);
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
            if (!Application.isPlaying) return; // edit-mode crack previews show plates only
            if (shardMat == null)
            {
                shardMat = shardMaterial != null ? shardMaterial
                    : new Material(Shader.Find("Crunchee/Candy")) { color = new Color(1f, 1f, 1f, 0.92f) };
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
