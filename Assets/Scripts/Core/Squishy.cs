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

        public bool Cracked => cracked;
        public float TopY => col != null ? col.bounds.max.y : transform.position.y;

        void Awake()
        {
            profile = overrideProfile ? profileOverride : SquishProfile.DefaultFor(baseMaterial, coating);
            restScale = transform.localScale;
            col = GetComponent<Collider>();
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
        public void OnLanded(float impactSpeed)
        {
            if (!cracked)
            {
                cracked = true;
                occupied = true;
                sink = Mathf.Max(sink, 0.3f); // the initial give as weight lands
                CrackShell();
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
