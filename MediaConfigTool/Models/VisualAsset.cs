using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class VisualAsset
    {
        [JsonPropertyName("visual_asset_id")]
        public string VisualAssetId { get; set; } = string.Empty;

        [JsonPropertyName("asset_uri")]
        public string AssetUri { get; set; } = string.Empty;

        [JsonPropertyName("asset_type")]
        public string AssetType { get; set; } = string.Empty;

        [JsonPropertyName("mime_type")]
        public string MimeType { get; set; } = string.Empty;

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        // Display helper
        public string FileName => System.IO.Path.GetFileName(AssetUri);
    }
}
