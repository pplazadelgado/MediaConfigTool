using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Tag
    {
        [JsonPropertyName("tag_id")]
        public string TagId { get; set; } = string.Empty;

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("tag_category_id")]
        public string TagCategoryId { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("color_hex")]
        public string? ColorHex { get; set; }

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;
    }
}
