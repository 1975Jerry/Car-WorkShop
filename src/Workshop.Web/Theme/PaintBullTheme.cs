using MudBlazor;

namespace Workshop.Web.Theme;

public static class PaintBullTheme
{
    public static MudTheme Light { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1F3A5F",       // Deep professional navy
            Secondary = "#D4AF37",     // Paint Bull gold accent
            Tertiary = "#3A6B8C",
            Info = "#0288D1",
            Success = "#2E7D32",
            Warning = "#F57C00",
            Error = "#C62828",
            Background = "#F7F8FA",
            Surface = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#1F3A5F",
            AppbarBackground = "#1F3A5F",
            AppbarText = "#FFFFFF",
            TextPrimary = "#1A1A1A",
            TextSecondary = "rgba(0,0,0,0.66)",
            LinesDefault = "rgba(0,0,0,0.10)",
            TableLines = "rgba(0,0,0,0.08)"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#4A7BA7",
            Secondary = "#D4AF37",
            Tertiary = "#5A8BB0",
            Background = "#121826",
            Surface = "#1A2235",
            DrawerBackground = "#1A2235",
            AppbarBackground = "#0F1622",
            TextPrimary = "#E6E8EC",
            TextSecondary = "rgba(255,255,255,0.7)"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Inter", "Noto Sans", "system-ui", "sans-serif" },
                FontSize = ".9rem",
                FontWeight = "400",
                LineHeight = "1.5"
            },
            H1 = new H1Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontSize = "2rem",   FontWeight = "600" },
            H2 = new H2Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontSize = "1.5rem", FontWeight = "600" },
            H3 = new H3Typography { FontFamily = new[] { "Inter", "sans-serif" }, FontSize = "1.25rem", FontWeight = "600" },
            Button = new ButtonTypography { FontWeight = "500", LetterSpacing = "0" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
            DrawerWidthLeft = "260px",
            AppbarHeight = "60px"
        }
    };
}
