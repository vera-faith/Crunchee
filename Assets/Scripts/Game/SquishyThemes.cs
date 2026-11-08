using UnityEngine;
using Crunchee.Core;

namespace Crunchee.Game
{
    public enum SquishyTheme { Bakery, ClassicButter }
    public enum SquishyItem { Butter, FlourBag, VanillaBowl, Croissant }

    [System.Serializable]
    public struct SquishyStepDefinition
    {
        public SquishyItem item;
        public Coating coating;
        [Range(1, 10)] public int waxLayers;
        public bool wrapped;

        public SquishyStepDefinition(SquishyItem item, Coating coating = Coating.Plain, int waxLayers = 1, bool wrapped = true)
        {
            this.item = item;
            this.coating = coating;
            this.waxLayers = waxLayers;
            this.wrapped = wrapped;
        }
    }

    public sealed class SquishyThemeDefinition
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string sceneName;
        public readonly Color skyTop;
        public readonly Color horizon;
        public readonly Color accent;
        public readonly SquishyStepDefinition[] steps;

        public SquishyThemeDefinition(string id, string displayName, string sceneName, Color skyTop, Color horizon, Color accent, SquishyStepDefinition[] steps)
        {
            this.id = id;
            this.displayName = displayName;
            this.sceneName = sceneName;
            this.skyTop = skyTop;
            this.horizon = horizon;
            this.accent = accent;
            this.steps = steps;
        }
    }

    public static class ThemePath
    {
        public const int StepsPerTurn = 20;

        static readonly SquishyStepDefinition[] bakerySteps =
        {
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Plain),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 1),
            new SquishyStepDefinition(SquishyItem.Croissant),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 2),
            new SquishyStepDefinition(SquishyItem.FlourBag),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Plain, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 3),
            new SquishyStepDefinition(SquishyItem.VanillaBowl),
            new SquishyStepDefinition(SquishyItem.Croissant),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 5),
            new SquishyStepDefinition(SquishyItem.FlourBag),
            new SquishyStepDefinition(SquishyItem.Butter),
            new SquishyStepDefinition(SquishyItem.Croissant),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 4),
            new SquishyStepDefinition(SquishyItem.VanillaBowl),
        };

        static readonly SquishyStepDefinition[] classicSteps =
        {
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Plain, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 1),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Sugar, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 2),
            new SquishyStepDefinition(SquishyItem.Butter),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 3),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Sugar, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 5),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Plain, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 1),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Sugar, 1, false),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 4),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 2),
            new SquishyStepDefinition(SquishyItem.Butter),
            new SquishyStepDefinition(SquishyItem.Butter, Coating.Wax, 5),
        };

        public static readonly SquishyThemeDefinition Bakery = new SquishyThemeDefinition(
            "bakery", "Bakery Spiral", "ButterPath",
            new Color(0.48f, 0.38f, 0.52f), new Color(0.96f, 0.72f, 0.52f), new Color(1f, 0.67f, 0.76f), bakerySteps);

        public static readonly SquishyThemeDefinition Classic = new SquishyThemeDefinition(
            "classicbutter", "Classic Butter", "ClassicButter",
            new Color(0.45f, 0.57f, 0.72f), new Color(1f, 0.85f, 0.54f), new Color(1f, 0.84f, 0.3f), classicSteps);

        public static SquishyThemeDefinition Get(SquishyTheme theme) => theme == SquishyTheme.ClassicButter ? Classic : Bakery;
    }
}
