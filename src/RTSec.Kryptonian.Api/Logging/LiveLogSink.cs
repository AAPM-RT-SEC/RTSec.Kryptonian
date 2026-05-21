using RTSec.Kryptonian.Application.DTOs;
using Serilog.Core;
using Serilog.Events;

namespace RTSec.Kryptonian.Api.Logging;

/// <summary>
/// Serilog sink that pushes every emitted log event into <see cref="LiveLogBroadcaster"/>
/// so the admin UI can stream gateway activity in near real time.
/// </summary>
public sealed class LiveLogSink : ILogEventSink
{
    private readonly LiveLogBroadcaster _broadcaster;
    private readonly IFormatProvider? _formatProvider;

    public LiveLogSink(LiveLogBroadcaster broadcaster, IFormatProvider? formatProvider = null)
    {
        _broadcaster = broadcaster;
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = new LiveLogEntryDto
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(_formatProvider),
            Exception = logEvent.Exception?.ToString(),
            CorrelationId = ReadString(logEvent, "CorrelationId"),
            RequestId = ReadString(logEvent, "RequestId"),
            ClientIp = ReadString(logEvent, "ClientIp"),
        };

        _broadcaster.Add(entry);
    }

    private static string? ReadString(LogEvent logEvent, string propertyName)
    {
        if (!logEvent.Properties.TryGetValue(propertyName, out var prop))
        {
            return null;
        }
        return prop is ScalarValue scalar ? scalar.Value?.ToString() : prop.ToString();
    }
}
