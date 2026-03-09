using Avalonia.Media;

namespace FlowStep.Renderers.AvaloniaUI.Themes
{
    public class ThemeColors
    {
        public Color Background { get; set; }
        public Color Surface { get; set; }
        public Color SurfaceHover { get; set; }
        public Color SurfacePressed { get; set; }
        public Color Primary { get; set; }
        public Color PrimaryHover { get; set; }
        public Color PrimaryPressed { get; set; }
        public Color PrimaryDisabled { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color TextMuted { get; set; }
        public Color TextDisabled { get; set; }
        public Color Border { get; set; }
        public Color BorderHover { get; set; }
        public Color Error { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }

        // Factory method to create default dark theme
        public static ThemeColors CreateDefaultDark()
        {
            return new ThemeColors
            {
                Background = Color.Parse("#0F0F12"),
                Surface = Color.Parse("#1A1A1F"),
                SurfaceHover = Color.Parse("#232329"),
                SurfacePressed = Color.Parse("#2A2A32"),
                Primary = Color.Parse("#6366F1"),
                PrimaryHover = Color.Parse("#818CF8"),
                PrimaryPressed = Color.Parse("#4F46E5"),
                PrimaryDisabled = Color.Parse("#374151"),
                TextPrimary = Color.Parse("#F9FAFB"),
                TextSecondary = Color.Parse("#9CA3AF"),
                TextMuted = Color.Parse("#6B7280"),
                TextDisabled = Color.Parse("#4B5563"),
                Border = Color.Parse("#27272A"),
                BorderHover = Color.Parse("#3F3F46"),
                Error = Color.Parse("#EF4444"),
                Success = Color.Parse("#22C55E"),
                Warning = Color.Parse("#F59E0B")
            };
        }
    }
}