namespace MediaConfigTool.Models
{
    public class MediaRenderData
    {
        public string MediaAssetId { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public DateTime? CaptureTimestamp { get; set; }

        public string? LocationName { get; set; }
        public string? MapAssetUri { get; set; }

        public List<string> PersonNames { get; set; } = new();
        public List<string> EventNames { get; set; } = new();
        public List<string> TagNames { get; set; } = new();
    }
}
