

using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Tenant
    {
        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("tenant_name")]
        public string TenantName { get; set;} = string.Empty;
    }
}
