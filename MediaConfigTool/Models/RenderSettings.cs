using System.Windows.Media;

namespace MediaConfigTool.Models;

public class RenderSettings
{
    public string FontFamily { get; set; } = "Georgia";
    public double FontSize { get; set; } = 26.0;
    public Color TextColor { get; set; } = Colors.White;
    public Color AccentColor { get; set; } = (Color)ColorConverter.ConvertFromString("#F2D7A4");

    public static RenderSettings Default { get; } = new RenderSettings();
}
