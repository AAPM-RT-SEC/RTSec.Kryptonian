using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service interface for enrollment event operations.
/// </summary>
public interface IEnrollmentEventService
{
    /// <summary>
    /// Gets recent enrollment events.
    /// </summary>
    /// <param name="profileId">Optional filter by profile ID.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IEnumerable<EnrollmentEventDto>> GetRecentAsync(Guid? profileId = null, int limit = 50, CancellationToken ct = default);
}
