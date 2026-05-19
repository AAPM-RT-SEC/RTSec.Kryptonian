using System.Collections.ObjectModel;
using AutoMapper;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;

namespace RTSec.Kryptonian.Application.Mapping;

/// <summary>
/// AutoMapper profile for mapping between domain entities and DTOs.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // CaBackend mappings
        CreateMap<CaBackend, CaBackendDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString().ToLowerInvariant()));

        CreateMap<CaBackendCreateDto, CaBackend>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => ParseCaBackendType(src.Type)))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.EstProfiles, opt => opt.Ignore());

        CreateMap<Device, DeviceDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString().ToLowerInvariant()))
            .ForMember(dest => dest.LastCertificateId, opt => opt.MapFrom(src => src.LastCertificateId.HasValue ? src.LastCertificateId.Value.ToString() : null));

        CreateMap<DeviceCreateDto, Device>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.Ignore())
            .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.RemovedAt, opt => opt.Ignore())
            .ForMember(dest => dest.LastCertificateId, opt => opt.Ignore())
            .ForMember(dest => dest.Certificates, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

        CreateMap<Certificate, CertificateDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.CaBackendId, opt => opt.MapFrom(src => src.CaBackendId.HasValue ? src.CaBackendId.Value.ToString() : null));

        // EstProfile mappings
        CreateMap<EstProfile, EstProfileDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.CaBackendId, opt => opt.MapFrom(src => src.CaBackendId.ToString()))
            .ForMember(dest => dest.HostnameMatchType, opt => opt.MapFrom(src => src.HostnameMatchType.ToString().ToLowerInvariant()));

        CreateMap<EstProfileCreateDto, EstProfile>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CaBackendId, opt => opt.Ignore()) // Handled in service with TryParse
            .ForMember(dest => dest.Hostnames, opt => opt.MapFrom(src => src.Hostnames ?? new List<string>()))
            .ForMember(dest => dest.AllowedKeyUsages, opt => opt.MapFrom(src => src.AllowedKeyUsages ?? new List<string>()))
            .ForMember(dest => dest.HostnameMatchType, opt => opt.MapFrom(src => ParseHostnameMatchType(src.HostnameMatchType)))
            .ForMember(dest => dest.TrustedClientCaThumbprints, opt => opt.MapFrom(src => src.TrustedClientCaThumbprints ?? new List<string>()))
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.CaBackend, opt => opt.Ignore())
            .ForMember(dest => dest.Certificates, opt => opt.Ignore())
            .ForMember(dest => dest.EnrollmentEvents, opt => opt.Ignore());

        // EnrollmentEvent mappings
        CreateMap<EnrollmentEvent, EnrollmentEventDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.ProfileId, opt => opt.MapFrom(src => src.ProfileId.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString().ToLowerInvariant()))
            .ForMember(dest => dest.IssuedCertificateId, opt => opt.MapFrom(src => src.IssuedCertificateId.HasValue ? src.IssuedCertificateId.Value.ToString() : null));
    }

    private static CaBackendType ParseCaBackendType(string type)
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

    private static HostnameMatchType ParseHostnameMatchType(string? type)
    {
        return (type?.ToLowerInvariant()) switch
        {
            "exact" or null => HostnameMatchType.Exact,
            "suffix" => HostnameMatchType.Suffix,
            "wildcard" => HostnameMatchType.Wildcard,
            _ => throw new ArgumentException($"Unknown hostname match type: {type}. Valid types are: exact, suffix, wildcard")
        };
    }
}
