
namespace MediaConfigTool.Rendering
{
    /// <summary>
    /// Defines visual styling settings used for slideshow rendering.
    /// Values are configurable and will be passed into the rendering engine in Phase 5.
    /// </summary>
    public class RenderSettings
    {
        public string FontFamily { get; set; } = "Georgia";
        public double FontSize { get; set; } = 26;
        public string TextColor { get; set; } = "#FFFFFF";
        public string AccentColor { get; set; } = "#F2D7A4";
    }
}
