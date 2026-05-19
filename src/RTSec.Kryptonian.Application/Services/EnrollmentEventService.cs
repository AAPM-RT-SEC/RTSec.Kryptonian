using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service for enrollment event queries.
/// </summary>
public class EnrollmentEventService : IEnrollmentEventService
{
    private readonly IUnitOfWork _unitOfWork;

    public EnrollmentEventService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EnrollmentEventDto>> GetRecentAsync(Guid? profileId = null, int limit = 50, CancellationToken ct = default)
    {
        var events = profileId.HasValue
            ? await _unitOfWork.EnrollmentEvents.GetByProfileIdAsync(profileId.Value, limit, ct)
            : await _unitOfWork.EnrollmentEvents.GetRecentAsync(limit, ct);

        return events.Select(DtoMapper.ToDto);
    }
}
