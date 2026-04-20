using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Person
    {
        [JsonPropertyName("person_id")]
        public string PersonId {  get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("relationship_type")]
        public string? RelationshipType { get; set; }

        [JsonPropertyName("tenant_id")]
        public string TenantId {  get; set; } = string.Empty;

    }
}
