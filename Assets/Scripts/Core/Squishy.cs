using UnityEngine;
using Crunchee.Audio;

namespace Crunchee.Core
{
    /// <summary>
    /// The heart of Crunchee. Put this on any steppable object.
    /// On landing it runs the pipeline in order:
    /// deform -> coating effect -> ASMR sound (layered) -> particles -> haptic -> bounce.
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
        public GameObject coatingShell;        // wax / nail-polish shell mesh that cracks/flashes, optional

        SquishProfile profile;
        Vector3 restScale;
        float squishVelocity;
        float currentSquish; // 0..1

        void Awake()
        {
            profile = overrideProfile ? profileOverride : SquishProfile.DefaultFor(baseMaterial, coating);
            restScale = transform.localScale;
        }

        void Update()
        {
            // Spring the squish back to 0 with a wobble (damped spring).
            float stiffness = 90f / Mathf.Max(0.05f, profile.springBack);
            float damping = 8f;
            float accel = -stiffness * currentSquish - damping * squishVelocity;
            squishVelocity += accel * Time.deltaTime;
            currentSquish += squishVelocity * Time.deltaTime;
            float y = 1f - profile.squishDepth * Mathf.Clamp(currentSquish, -0.3f, 1f);
            float xz = 1f + (1f - y) * 0.6f; // volume-ish bulge sideways
            transform.localScale = new Vector3(restScale.x * xz, restScale.y * y, restScale.z * xz);
        }

        /// <summary>Called by the player when it lands on / presses this squishy.</summary>
        public float OnStepped(float impactSpeed)
        {
            // 1. Deform
            currentSquish = Mathf.Clamp01(0.4f + impactSpeed * 0.08f);
            squishVelocity = 0f;

            // 2. Coating effect hook (crack shell, gloss flash) - visuals land in M2/M3.
            if (coatingShell != null && (coating == Coating.Wax || coating == Coating.NailPolish))
                coatingShell.SetActive(false); // shell "breaks"/flashes off on first step; reset by checkpoint if needed

            // 3. ASMR sound: coating layer + base layer, micro-varied.
            //    Wax passes its layer count so the crack can stack/scale audibly.
            AsmrAudio.PlaySquish(baseMaterial, coating, transform.position,
                                 coating == Coating.Wax ? waxLayers : 1);

            // 4. Particles (wax: flake count scales with layers)
            if (stepParticles != null)
            {
                var main = stepParticles.main;
                if (coating == Coating.Wax) stepParticles.Emit(6 + waxLayers * 4);
                else stepParticles.Emit(14);
            }

            // 5. Haptic (mobile)
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate(); // placeholder: replaced by graded haptics in M2 (strength = profile.hapticStrength)
#endif
            // 6. Bounce handed back to the player
            return profile.bounceForce;
        }

        public bool IsSticky => profile.stickyRelease;
    }
}
