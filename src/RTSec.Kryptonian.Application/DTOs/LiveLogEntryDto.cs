using System.Text.Json.Serialization;

namespace RTSec.Kryptonian.Application.DTOs;

/// <summary>
/// A single log entry surfaced from the gateway's in-memory live-log ring buffer.
/// </summary>
public class LiveLogEntryDto
{
    [JsonPropertyName("seq")]
    public long Seq { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("requestId")]
    public string? RequestId { get; set; }

    [JsonPropertyName("clientIp")]
    public string? ClientIp { get; set; }
}

/// <summary>
/// Page of live-log entries plus the broadcaster's current head sequence, so
/// clients can poll incrementally with sinceSeq = page.CurrentSeq.
/// </summary>
public class LiveLogPageDto
{
    [JsonPropertyName("entries")]
    public List<LiveLogEntryDto> Entries { get; set; } = new();

    [JsonPropertyName("currentSeq")]
    public long CurrentSeq { get; set; }
}
