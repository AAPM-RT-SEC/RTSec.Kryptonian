using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

public interface IHackathonSettingsService
{
    Task<HackathonSettingsDto> GetAsync(CancellationToken ct = default);
    Task<HackathonSettingsDto> UpdateAsync(HackathonSettingsUpdateDto dto, CancellationToken ct = default);
}
