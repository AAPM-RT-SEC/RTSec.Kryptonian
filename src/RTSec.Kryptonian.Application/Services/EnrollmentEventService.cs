using AutoMapper;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service for enrollment event queries.
/// </summary>
public class EnrollmentEventService : IEnrollmentEventService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public EnrollmentEventService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EnrollmentEventDto>> GetRecentAsync(Guid? profileId = null, int limit = 50, CancellationToken ct = default)
    {
        var events = profileId.HasValue
            ? await _unitOfWork.EnrollmentEvents.GetByProfileIdAsync(profileId.Value, limit, ct)
            : await _unitOfWork.EnrollmentEvents.GetRecentAsync(limit, ct);

        return _mapper.Map<IEnumerable<EnrollmentEventDto>>(events);
    }
}
