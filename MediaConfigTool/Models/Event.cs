using System.Text.Json.Serialization;

namespace MediaConfigTool.Models
{
    public class Event
    {
        [JsonPropertyName("event_id")]
        public string EventId { get; set; } = string.Empty;

        [JsonPropertyName("event_name")]
        public string EventName { get; set; } = string.Empty;

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("start_timestamp")]
        public DateTimeOffset? StartTimestamp { get; set; }

        [JsonPropertyName("end_timestamp")]
        public DateTimeOffset? EndTimestamp { get; set; }
    }
}