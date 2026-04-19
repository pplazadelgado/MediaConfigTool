using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Tag
    {
        [JsonPropertyName("tag_id")]
        public string TagId { get; set; } = string.Empty;

        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;
    }
}
