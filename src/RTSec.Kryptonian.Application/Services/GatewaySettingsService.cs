using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public class GatewaySettingsService : IGatewaySettingsService
{
    private readonly IUnitOfWork _unitOfWork;

    public GatewaySettingsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GatewaySettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return DtoMapper.ToDto(settings);
    }

    public async Task<GatewaySettingsDto> UpdateAsync(GatewaySettingsUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Validate(dto);

        var settings = await GetOrCreateSettingsAsync(ct);
        settings.DefaultCertificateLifetimeHours = dto.DefaultCertificateLifetimeHours;

        _unitOfWork.GatewaySettings.Update(settings);
        await _unitOfWork.SaveChangesAsync(ct);

        return DtoMapper.ToDto(settings);
    }

    private async Task<GatewaySettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _unitOfWork.GatewaySettings.GetSingletonAsync(ct);
        if (settings != null)
        {
            return settings;
        }

        settings = new GatewaySettings
        {
            Id = Guid.NewGuid(),
            DefaultCertificateLifetimeHours = GatewaySettings.DefaultCertificateLifetimeHoursDefault
        };
        _unitOfWork.GatewaySettings.Add(settings);
        await _unitOfWork.SaveChangesAsync(ct);
        return settings;
    }

    private static void Validate(GatewaySettingsUpdateDto dto)
    {
        if (dto.DefaultCertificateLifetimeHours < GatewaySettings.MinCertificateLifetimeHours
            || dto.DefaultCertificateLifetimeHours > GatewaySettings.MaxCertificateLifetimeHours)
        {
            throw new ArgumentException(
                $"Default certificate lifetime must be between {GatewaySettings.MinCertificateLifetimeHours} and {GatewaySettings.MaxCertificateLifetimeHours} hours.");
        }
    }
}
