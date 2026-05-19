using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Application.Mapping;

internal static class DtoMapper
{
    public static CaBackendDto ToDto(CaBackend entity)
    {
        var dto = new CaBackendDto
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            Type = ToApiString(entity.Type),
            Url = entity.Url,
            IsEnabled = entity.IsEnabled,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        foreach (var item in entity.Config)
        {
            dto.Config.Add(item.Key, item.Value);
        }

        return dto;
    }

    public static CaBackend ToEntity(CaBackendCreateDto dto)
    {
        var entity = new CaBackend
        {
            Name = dto.Name,
            Type = ParseCaBackendType(dto.Type),
            Url = string.IsNullOrWhiteSpace(dto.Url) ? null : new Uri(dto.Url),
            IsEnabled = dto.IsEnabled,
            IsActive = dto.IsActive
        };

        if (dto.Config != null)
        {
            foreach (var item in dto.Config)
            {
                entity.Config.Add(item.Key, item.Value);
            }
        }

        return entity;
    }

    public static DeviceDto ToDto(Device entity)
    {
        return new DeviceDto
        {
            Id = entity.Id.ToString(),
            DisplayName = entity.DisplayName,
            SubjectCommonName = entity.SubjectCommonName,
            Manufacturer = entity.Manufacturer,
            Model = entity.Model,
            SerialNumber = entity.SerialNumber,
            Status = entity.Status.ToString().ToLowerInvariant(),
            ApprovedAt = entity.ApprovedAt,
            RemovedAt = entity.RemovedAt,
            LastCertificateId = entity.LastCertificateId?.ToString(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static CertificateDto ToDto(Certificate entity)
    {
        return new CertificateDto
        {
            Id = entity.Id.ToString(),
            SerialNumber = entity.SerialNumber,
            SubjectDn = entity.SubjectDn,
            IssuerDn = entity.IssuerDn,
            Thumbprint = entity.Thumbprint,
            CertificatePem = entity.CertificatePem,
            CertificateDerBase64 = entity.CertificateDerBase64,
            CaBackendId = entity.CaBackendId?.ToString(),
            CaBackendType = entity.CaBackendType,
            GatewayOid = entity.GatewayOid,
            NotBefore = entity.NotBefore,
            NotAfter = entity.NotAfter,
            CreatedAt = entity.CreatedAt
        };
    }

    public static EstProfileDto ToDto(EstProfile entity)
    {
        var dto = new EstProfileDto
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            HostnameMatchType = entity.HostnameMatchType.ToString().ToLowerInvariant(),
            AllowedWildcardSuffix = entity.AllowedWildcardSuffix,
            PathPrefix = entity.PathPrefix,
            CaBackendId = entity.CaBackendId.ToString(),
            CertificateTemplate = entity.CertificateTemplate,
            ValidityDays = entity.ValidityDays,
            RequireClientCertificate = entity.RequireClientCertificate,
            ValidateClientCertificateChain = entity.ValidateClientCertificateChain,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };

        foreach (var hostname in entity.Hostnames)
        {
            dto.Hostnames.Add(hostname);
        }

        foreach (var usage in entity.AllowedKeyUsages)
        {
            dto.AllowedKeyUsages.Add(usage);
        }

        foreach (var thumbprint in entity.TrustedClientCaThumbprints)
        {
            dto.TrustedClientCaThumbprints.Add(thumbprint);
        }

        return dto;
    }

    public static EstProfile ToEntity(EstProfileCreateDto dto, Guid caBackendId)
    {
        var entity = new EstProfile
        {
            Name = dto.Name,
            HostnameMatchType = ParseHostnameMatchType(dto.HostnameMatchType),
            AllowedWildcardSuffix = dto.AllowedWildcardSuffix,
            PathPrefix = dto.PathPrefix,
            CaBackendId = caBackendId,
            CertificateTemplate = dto.CertificateTemplate,
            ValidityDays = dto.ValidityDays,
            RequireClientCertificate = dto.RequireClientCertificate,
            ValidateClientCertificateChain = dto.ValidateClientCertificateChain,
            IsEnabled = dto.IsEnabled
        };

        foreach (var hostname in dto.Hostnames)
        {
            entity.Hostnames.Add(hostname);
        }

        foreach (var usage in dto.AllowedKeyUsages ?? Enumerable.Empty<string>())
        {
            entity.AllowedKeyUsages.Add(usage);
        }

        foreach (var thumbprint in dto.TrustedClientCaThumbprints ?? Enumerable.Empty<string>())
        {
            entity.TrustedClientCaThumbprints.Add(thumbprint);
        }

        return entity;
    }

    public static EnrollmentEventDto ToDto(EnrollmentEvent entity)
    {
        return new EnrollmentEventDto
        {
            Id = entity.Id.ToString(),
            Timestamp = entity.Timestamp,
            ProfileId = entity.ProfileId.ToString(),
            DeviceId = entity.DeviceId,
            Status = entity.Status.ToString().ToLowerInvariant(),
            SubjectDn = entity.SubjectDn,
            RequestorIpAddress = entity.RequestorIpAddress,
            ErrorMessage = entity.ErrorMessage,
            IssuedCertificateId = entity.IssuedCertificateId?.ToString()
        };
    }

    public static HackathonSettingsDto ToDto(HackathonSettings entity)
    {
        return new HackathonSettingsDto
        {
            Id = entity.Id.ToString(),
            HarnessBaseUrl = entity.HarnessBaseUrl,
            TeamToken = entity.TeamToken,
            DimseHost = entity.DimseHost,
            DimseTlsPort = entity.DimseTlsPort,
            OrthancDimsePort = entity.OrthancDimsePort,
            DicomWebBaseUrl = entity.DicomWebBaseUrl,
            CalledAeTitle = entity.CalledAeTitle,
            BridgeAeTitle = entity.BridgeAeTitle,
            BridgeListenPort = entity.BridgeListenPort,
            TrustedProxyCertificateThumbprint = entity.TrustedProxyCertificateThumbprint,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public static CaBackendType ParseCaBackendType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "adcs" => CaBackendType.Adcs,
            "ejbca" => CaBackendType.Ejbca,
            "cfssl" => CaBackendType.Cfssl,
            "selfsigned" => CaBackendType.SelfSigned,
            "acme" => CaBackendType.Acme,
            _ => throw new ArgumentException($"Unknown CA backend type: {type}")
        };
    }

    public static HostnameMatchType ParseHostnameMatchType(string? type)
    {
        return (type?.ToLowerInvariant()) switch
        {
            "exact" or null => HostnameMatchType.Exact,
            "suffix" => HostnameMatchType.Suffix,
            "wildcard" => HostnameMatchType.Wildcard,
            _ => throw new ArgumentException($"Unknown hostname match type: {type}. Valid types are: exact, suffix, wildcard")
        };
    }

    private static string ToApiString(CaBackendType type)
    {
        return type == CaBackendType.SelfSigned ? "selfsigned" : type.ToString().ToLowerInvariant();
    }
}
