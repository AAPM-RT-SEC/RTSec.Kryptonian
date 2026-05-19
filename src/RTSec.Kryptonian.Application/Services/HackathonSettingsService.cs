using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public class HackathonSettingsService : IHackathonSettingsService
{
    private const string DefaultHarnessBaseUrl = "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io";
    private readonly IUnitOfWork _unitOfWork;

    public HackathonSettingsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<HackathonSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateSettingsAsync(ct);
        return DtoMapper.ToDto(settings);
    }

    public async Task<HackathonSettingsDto> UpdateAsync(HackathonSettingsUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        Validate(dto);

        var settings = await GetOrCreateSettingsAsync(ct);
        settings.HarnessBaseUrl = dto.HarnessBaseUrl.Trim().TrimEnd('/');
        settings.TeamToken = dto.TeamToken.Trim();
        settings.DimseHost = dto.DimseHost.Trim();
        settings.DimseTlsPort = dto.DimseTlsPort;
        settings.OrthancDimsePort = dto.OrthancDimsePort;
        settings.DicomWebBaseUrl = dto.DicomWebBaseUrl.Trim().TrimEnd('/');
        settings.CalledAeTitle = dto.CalledAeTitle.Trim();
        settings.BridgeAeTitle = dto.BridgeAeTitle.Trim();
        settings.BridgeListenPort = dto.BridgeListenPort;
        settings.TrustedProxyCertificateThumbprint = string.IsNullOrWhiteSpace(dto.TrustedProxyCertificateThumbprint)
            ? null
            : dto.TrustedProxyCertificateThumbprint.Trim();

        _unitOfWork.HackathonSettings.Update(settings);
        await _unitOfWork.SaveChangesAsync(ct);

        return DtoMapper.ToDto(settings);
    }

    private async Task<HackathonSettings> GetOrCreateSettingsAsync(CancellationToken ct)
    {
        var settings = await _unitOfWork.HackathonSettings.GetSingletonAsync(ct);
        if (settings != null)
        {
            return settings;
        }

        settings = new HackathonSettings
        {
            Id = Guid.NewGuid(),
            HarnessBaseUrl = DefaultHarnessBaseUrl
        };
        _unitOfWork.HackathonSettings.Add(settings);
        await _unitOfWork.SaveChangesAsync(ct);
        return settings;
    }

    private static void Validate(HackathonSettingsUpdateDto dto)
    {
        if (!Uri.TryCreate(dto.HarnessBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Harness base URL must be an absolute URL.");
        }

        if (!Uri.TryCreate(dto.DicomWebBaseUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("DICOMweb base URL must be an absolute URL.");
        }

        ValidatePort(dto.DimseTlsPort, nameof(dto.DimseTlsPort));
        ValidatePort(dto.OrthancDimsePort, nameof(dto.OrthancDimsePort));
        ValidatePort(dto.BridgeListenPort, nameof(dto.BridgeListenPort));

        if (string.IsNullOrWhiteSpace(dto.DimseHost))
        {
            throw new ArgumentException("DIMSE host is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.CalledAeTitle))
        {
            throw new ArgumentException("Called AE title is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.BridgeAeTitle))
        {
            throw new ArgumentException("Bridge AE title is required.");
        }
    }

    private static void ValidatePort(int port, string name)
    {
        if (port < 1 || port > 65535)
        {
            throw new ArgumentException($"{name} must be between 1 and 65535.");
        }
    }
}
