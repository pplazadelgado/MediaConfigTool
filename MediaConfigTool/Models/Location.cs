using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Location
    {
        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("location_name")]
        public string LocationName { get; set; } = string.Empty;

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("location_type")]
        public string LocationType { get; set; } = string.Empty;
    }
}
