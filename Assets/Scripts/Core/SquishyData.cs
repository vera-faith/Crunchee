using UnityEngine;

namespace Crunchee.Core
{
    /// <summary>What a squishy is made of. Defines its sound family, squish feel and particles.</summary>
    public enum BaseMaterial
    {
        Butter, Croissant, CakeSponge, PawPad, Melon, Mochi,
        Macaron, Chocolate, Donut, Grape, KeyboardKey, Toast, Jam
    }

    /// <summary>Surface coating. Modifies look, step-on effect AND sound of any base material.</summary>
    public enum Coating
    {
        Plain, Wax, Sugar, Honey, NailPolish
    }

    /// <summary>
    /// Tunable feel for one base x coating combination.
    /// Filled in from the design tables in the spec; defaults are sane starting points.
    /// </summary>
    [System.Serializable]
    public struct SquishProfile
    {
        [Range(0f, 0.7f)] public float squishDepth;    // how far it compresses (0.35 = 35%)
        public float springBack;                       // seconds to wobble-settle
        public float bounceForce;                      // upward push given to the player
        public float hapticStrength;                   // 0..1, matched to material hardness
        public bool stickyRelease;                     // honey: slower release + pull sound

        public static SquishProfile DefaultFor(BaseMaterial b, Coating c)
        {
            var p = new SquishProfile { squishDepth = 0.35f, springBack = 0.45f, bounceForce = 8f, hapticStrength = 0.4f, stickyRelease = false };
            if (b == BaseMaterial.CakeSponge || b == BaseMaterial.Mochi) { p.squishDepth = 0.5f; p.hapticStrength = 0.25f; }
            if (b == BaseMaterial.KeyboardKey) { p.squishDepth = 0.18f; p.springBack = 0.18f; p.bounceForce = 3f; p.hapticStrength = 0.6f; }
            if (b == BaseMaterial.Chocolate || b == BaseMaterial.Macaron) { p.squishDepth = 0.15f; p.hapticStrength = 0.7f; }
            switch (c)
            {
                case Coating.Wax: p.squishDepth *= 0.8f; p.hapticStrength += 0.15f; break;
                case Coating.Sugar: p.squishDepth *= 0.85f; p.hapticStrength += 0.1f; break;
                case Coating.Honey: p.stickyRelease = true; p.springBack *= 1.6f; p.hapticStrength -= 0.1f; break;
                case Coating.NailPolish: p.squishDepth *= 0.6f; p.hapticStrength += 0.3f; break;
            }
            p.hapticStrength = Mathf.Clamp01(p.hapticStrength);
            return p;
        }
    }
}
