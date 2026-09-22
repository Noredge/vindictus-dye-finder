using DyeFinder.Core;

namespace DyeFinder.App;

public static class ColorLabels
{
    public static string? Name(Rgb color, string? customName = null)
    {
        if (!string.IsNullOrWhiteSpace(customName)) return customName.Trim();
        return color switch
        {
            { R:230, G:230, B:230 } => "White", { R:25, G:25, B:25 } => "Black",
            { R:200, G:130, B:35 } => "Gold", { R:140, G:36, B:35 } => "Deep Red",
            { R:184, G:125, B:46 } => "Amber", { R:171, G:40, B:38 } => "Red", _ => null
        };
    }
    public static string Display(Rgb color, string? customName = null) => Name(color, customName) ?? $"RGB {color}";
}
