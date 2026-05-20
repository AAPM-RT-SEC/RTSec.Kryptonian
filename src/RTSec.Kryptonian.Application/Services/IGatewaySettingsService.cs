using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

public interface IGatewaySettingsService
{
    Task<GatewaySettingsDto> GetAsync(CancellationToken ct = default);
    Task<GatewaySettingsDto> UpdateAsync(GatewaySettingsUpdateDto dto, CancellationToken ct = default);
}
