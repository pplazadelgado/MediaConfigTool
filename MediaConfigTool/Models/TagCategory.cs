using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class TagCategory
    {
        [JsonPropertyName("tag_category_id")]
        public string TagCategoryId { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("category_type")]
        public string CategoryType { get; set; } = string.Empty;

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;
    }
}
