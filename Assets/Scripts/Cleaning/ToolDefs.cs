using UnityEngine;

namespace AfterHours
{
    /// <summary>Tuning for every cleaning tool in one place.</summary>
    public static class ToolDefs
    {
        public static readonly Brush Cloth = new()
        {
            Shape = BrushShape.Round, Radius = 0.13f, Rate = 6.5f, ScrubRef = 0.5f, MinScrub = 0.35f,
            DryFactor = 0.8f,
        };

        public static readonly Brush Vacuum = new()
        {
            Shape = BrushShape.Round, Radius = 0.2f, Rate = 7f, Nap = true,
        };

        public static readonly Brush Squeegee = new()
        {
            Shape = BrushShape.Blade, Radius = 0.19f, Rate = 60f, DryFactor = 0.15f, ClearsFoam = true,
        };

        public static readonly Brush Mop = new()
        {
            Shape = BrushShape.Round, Radius = 0.26f, Rate = 5f, ScrubRef = 0.7f, MinScrub = 0.4f, Wet = 1f,
        };

        public static Brush For(ToolKind k) => k switch
        {
            ToolKind.Cloth => Cloth,
            ToolKind.Vacuum => Vacuum,
            ToolKind.Squeegee => Squeegee,
            ToolKind.Mop => Mop,
            _ => Cloth,
        };

        /// <summary>Max distance from the eye to the contact point.</summary>
        public static float Reach(ToolKind k) => k switch
        {
            ToolKind.Cloth => 2.1f,
            ToolKind.Squeegee => 2.7f,
            ToolKind.Vacuum => 3.3f,
            ToolKind.Mop => 3.3f,
            _ => 2.2f,
        };

        public static string Name(ToolKind k) => k switch
        {
            ToolKind.Cloth => "Cloth & spray",
            ToolKind.Vacuum => "Vacuum",
            ToolKind.Squeegee => "Squeegee",
            ToolKind.Mop => "Mop",
            ToolKind.Hands => "Hands",
            _ => "",
        };

        public static Color Accent(ToolKind k) => k switch
        {
            ToolKind.Cloth => new Color(0.55f, 0.85f, 1f),
            ToolKind.Vacuum => new Color(1f, 0.72f, 0.35f),
            ToolKind.Squeegee => new Color(0.6f, 1f, 0.9f),
            ToolKind.Mop => new Color(0.7f, 0.8f, 1f),
            _ => Color.white,
        };
    }
}
