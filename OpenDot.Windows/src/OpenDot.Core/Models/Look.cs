namespace OpenDot.Core.Models;

public class Look
{
    public string Shape { get; set; } = "round"; // round, chubby, tall
    public string Color { get; set; } = "#9fcbff"; // body pastel hex
    public string Accent { get; set; } = "#2a6fdb"; // feet / accessory trim
    public string EyeColor { get; set; } = "#3b3842";
    public string Material { get; set; } = "soft"; // soft, glossy, velvet, toon
    public string Eyes { get; set; } = "classic"; // classic, happy, wide, wink
    public string Accessory { get; set; } = "none"; // none, bow, cap, antenna, halo, sprout, headphones

    public static readonly string[] Shapes = ["round", "chubby", "tall"];
    public static readonly string[] Materials = ["soft", "glossy", "velvet", "toon"];
    public static readonly string[] EyesList = ["classic", "happy", "wide", "wink"];
    public static readonly string[] Accessories = ["none", "bow", "cap", "antenna", "halo", "sprout", "headphones"];

    public static readonly string[] Colors =
    [
        "#ffa7c4", "#ffb8a1", "#ffd98a", "#c8ec8f", "#97e0b8", "#8fdcd6",
        "#9fcbff", "#b9b3ff", "#dcb4f5", "#f4efe6", "#c9c3bb", "#6e6a78"
    ];

    public static readonly string[] FeetColors =
    [
        "#d8195f", "#e0492d", "#d98a00", "#4f9a2f", "#1f8f5f", "#138a8a",
        "#2a6fdb", "#5b46d6", "#9b3cc4", "#3b3842", "#8a5a3c", "#f4f4f4"
    ];

    public static readonly string[] EyeColors = ["#2b5fd9", "#1f8f5f", "#8a4bd6", "#c2410c", "#3b3842"];
}
