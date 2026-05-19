using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service for EST profile CRUD operations.
/// </summary>
public class EstProfileService : IEstProfileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EstProfileService> _logger;

    public EstProfileService(
        IUnitOfWork unitOfWork,
        ILogger<EstProfileService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<EstProfileDto>> GetAllAsync(CancellationToken ct = default)
    {
        var profiles = await _unitOfWork.EstProfiles.GetAllAsync(ct);
        return profiles.Select(DtoMapper.ToDto);
    }

    /// <inheritdoc />
    public async Task<EstProfileDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await _unitOfWork.EstProfiles.GetByIdAsync(id, ct);
        return profile == null ? null : DtoMapper.ToDto(profile);
    }

    /// <inheritdoc />
    public async Task<EstProfileDto> CreateAsync(EstProfileCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _logger.LogInformation("Creating EST profile: {Name}", dto.Name);

        // Validate CA backend ID format
        if (!Guid.TryParse(dto.CaBackendId, out var caBackendId))
        {
            _logger.LogWarning("Invalid CA backend ID format: {CaBackendId}", dto.CaBackendId);
            throw new ArgumentException($"Invalid CA backend ID format: {dto.CaBackendId}");
        }

        // Validate CA backend exists
        var caBackend = await _unitOfWork.CaBackends.GetByIdAsync(caBackendId, ct);
        if (caBackend == null)
        {
            throw new ArgumentException($"CA backend not found: {dto.CaBackendId}");
        }

        // Check for duplicate path/hostname combination
        foreach (var hostname in dto.Hostnames)
        {
            var existing = await _unitOfWork.EstProfiles.GetByPathAndHostnameAsync(dto.PathPrefix, hostname, ct);
            if (existing != null)
            {
                throw new InvalidOperationException($"EST profile already exists for path '{dto.PathPrefix}' and hostname '{hostname}'");
            }
        }

        var entity = DtoMapper.ToEntity(dto, caBackendId);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.EstProfiles.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created EST profile with ID: {Id}", entity.Id);

        return DtoMapper.ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<EstProfileDto?> UpdateAsync(Guid id, EstProfileUpdateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await _unitOfWork.EstProfiles.GetByIdAsync(id, ct);
        if (entity == null)
        {
            _logger.LogWarning("EST profile not found for update: {Id}", id);
            return null;
        }

        _logger.LogInformation("Updating EST profile: {Id}", id);

        // Validate CA backend if being changed
        if (dto.CaBackendId != null)
        {
            if (!Guid.TryParse(dto.CaBackendId, out var caBackendId))
            {
                _logger.LogWarning("Invalid CA backend ID format: {CaBackendId}", dto.CaBackendId);
                throw new ArgumentException($"Invalid CA backend ID format: {dto.CaBackendId}");
            }

            var caBackend = await _unitOfWork.CaBackends.GetByIdAsync(caBackendId, ct);
            if (caBackend == null)
            {
                throw new ArgumentException($"CA backend not found: {dto.CaBackendId}");
            }
            entity.CaBackendId = caBackendId;
        }

        // Check for duplicate path/hostname if being changed
        var newPath = dto.PathPrefix ?? entity.PathPrefix;
        var newHostnames = dto.Hostnames?.ToList() ?? entity.Hostnames.ToList();

        if (dto.PathPrefix != null || dto.Hostnames != null)
        {
            foreach (var hostname in newHostnames)
            {
                var existing = await _unitOfWork.EstProfiles.GetByPathAndHostnameAsync(newPath, hostname, ct);
                if (existing != null && existing.Id != id)
                {
                    throw new InvalidOperationException($"EST profile already exists for path '{newPath}' and hostname '{hostname}'");
                }
            }
        }

        // Apply partial updates
        if (dto.Name != null)
            entity.Name = dto.Name;

        if (dto.Hostnames != null)
        {
            entity.Hostnames.Clear();
            foreach (var hostname in dto.Hostnames)
            {
                entity.Hostnames.Add(hostname);
            }
        }

        if (dto.PathPrefix != null)
            entity.PathPrefix = dto.PathPrefix;

        if (dto.CertificateTemplate != null)
            entity.CertificateTemplate = dto.CertificateTemplate;

        if (dto.AllowedKeyUsages != null)
        {
            entity.AllowedKeyUsages.Clear();
            foreach (var keyUsage in dto.AllowedKeyUsages)
            {
                entity.AllowedKeyUsages.Add(keyUsage);
            }
        }

        if (dto.ValidityDays.HasValue)
            entity.ValidityDays = dto.ValidityDays.Value;

        if (dto.RequireClientCertificate.HasValue)
            entity.RequireClientCertificate = dto.RequireClientCertificate.Value;

        if (dto.IsEnabled.HasValue)
            entity.IsEnabled = dto.IsEnabled.Value;

        if (dto.HostnameMatchType != null)
        {
            if (!TryParseHostnameMatchType(dto.HostnameMatchType, out var matchType))
            {
                _logger.LogWarning("Invalid hostname match type: {HostnameMatchType}", dto.HostnameMatchType);
                throw new ArgumentException($"Invalid hostname match type: {dto.HostnameMatchType}. Valid types are: exact, suffix, wildcard");
            }
            entity.HostnameMatchType = matchType;
        }

        if (dto.AllowedWildcardSuffix != null)
            entity.AllowedWildcardSuffix = dto.AllowedWildcardSuffix;

        if (dto.ValidateClientCertificateChain.HasValue)
            entity.ValidateClientCertificateChain = dto.ValidateClientCertificateChain.Value;

        if (dto.TrustedClientCaThumbprints != null)
        {
            entity.TrustedClientCaThumbprints.Clear();
            foreach (var thumbprint in dto.TrustedClientCaThumbprints)
            {
                entity.TrustedClientCaThumbprints.Add(thumbprint);
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.EstProfiles.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated EST profile: {Id}", id);

        return DtoMapper.ToDto(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.EstProfiles.GetByIdAsync(id, ct);
        if (entity == null)
        {
            _logger.LogWarning("EST profile not found for deletion: {Id}", id);
            return false;
        }

        _logger.LogInformation("Deleting EST profile: {Id}", id);

        _unitOfWork.EstProfiles.Delete(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted EST profile: {Id}", id);

        return true;
    }

    private static bool TryParseHostnameMatchType(string type, out HostnameMatchType result)
    {
        result = type.ToLowerInvariant() switch
        {
            "exact" => HostnameMatchType.Exact,
            "suffix" => HostnameMatchType.Suffix,
            "wildcard" => HostnameMatchType.Wildcard,
            _ => (HostnameMatchType)(-1) // Invalid marker
        };

        return (int)result >= 0;
    }
}
