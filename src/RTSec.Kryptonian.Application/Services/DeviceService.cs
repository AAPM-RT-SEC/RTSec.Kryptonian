using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Application.Mapping;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

public class DeviceService : IDeviceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEnrollmentOrchestrator _orchestrator;
    private readonly IDataProtectionService _dataProtection;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IUnitOfWork unitOfWork,
        IEnrollmentOrchestrator orchestrator,
        IDataProtectionService dataProtection,
        ILogger<DeviceService> logger)
    {
        _unitOfWork = unitOfWork;
        _orchestrator = orchestrator;
        _dataProtection = dataProtection;
        _logger = logger;
    }

    public async Task<IEnumerable<DeviceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var devices = await _unitOfWork.Devices.GetAllAsync(ct);
        return devices.Select(DtoMapper.ToDto);
    }

    public async Task<DeviceDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var device = await _unitOfWork.Devices.GetByIdAsync(id, ct);
        return device == null ? null : DtoMapper.ToDto(device);
    }

    public async Task<DeviceDto> CreateAsync(DeviceCreateDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var device = await CreatePendingDeviceAsync(
            dto.DisplayName,
            dto.SubjectCommonName,
            dto.Manufacturer,
            dto.Model,
            dto.SerialNumber,
            ct);

        return DtoMapper.ToDto(device);
    }

    public async Task<DeviceApprovalRequestResponseDto> RequestApprovalAsync(DeviceApprovalRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var device = await CreatePendingDeviceAsync(
            dto.DisplayName,
            dto.SubjectCommonName,
            dto.Manufacturer,
            dto.Model,
            dto.SerialNumber,
            ct);

        _logger.LogInformation("Device approval requested for {SubjectCommonName}", device.SubjectCommonName);

        return new DeviceApprovalRequestResponseDto
        {
            DeviceId = device.Id.ToString(),
            Status = device.Status.ToString().ToLowerInvariant(),
            SubjectCommonName = device.SubjectCommonName,
            Message = "Approval request received. An administrator must approve this device before EST enrollment is allowed."
        };
    }

    public async Task<DeviceDto?> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var device = await _unitOfWork.Devices.GetByIdAsync(id, ct);
        if (device == null)
        {
            return null;
        }

        device.Status = DeviceStatus.Active;
        device.ApprovedAt ??= DateTime.UtcNow;
        device.RemovedAt = null;
        _unitOfWork.Devices.Update(device);
        await _unitOfWork.SaveChangesAsync(ct);

        return DtoMapper.ToDto(device);
    }

    public async Task<DeviceDto?> RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var device = await _unitOfWork.Devices.GetByIdAsync(id, ct);
        if (device == null)
        {
            return null;
        }

        device.Status = DeviceStatus.Removed;
        device.RemovedAt = DateTime.UtcNow;
        _unitOfWork.Devices.Update(device);
        await _unitOfWork.SaveChangesAsync(ct);

        return DtoMapper.ToDto(device);
    }

    public async Task<IEnumerable<CertificateDto>> GetCertificatesAsync(Guid id, CancellationToken ct = default)
    {
        var certificates = await _unitOfWork.Certificates.GetByDeviceRecordIdAsync(id, ct);
        return certificates.Select(DtoMapper.ToDto);
    }

    public async Task<DemoEnrollResponseDto?> DemoEnrollAsync(Guid id, Guid profileId, CancellationToken ct = default)
    {
        var device = await _unitOfWork.Devices.GetByIdAsync(id, ct);
        if (device == null)
        {
            return null;
        }

        if (device.Status != DeviceStatus.Active)
        {
            throw new InvalidOperationException("Only active devices can be demo enrolled.");
        }

        var activeBackend = await _unitOfWork.CaBackends.GetActiveAsync(ct)
            ?? throw new InvalidOperationException("No active CA backend is configured.");

        var (csr, privateKeyPem) = CreateDemoCsr(device.SubjectCommonName);
        var result = await _orchestrator.EnrollAsync(profileId, csr, device.SubjectCommonName, "demo-ui", ct);
        if (!result.Success || result.EnrollmentEventId == null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Demo enrollment failed.");
        }

        var enrollmentEvent = await _unitOfWork.EnrollmentEvents.GetByIdAsync(result.EnrollmentEventId.Value, ct)
            ?? throw new InvalidOperationException("Enrollment event was not persisted.");

        if (enrollmentEvent.IssuedCertificateId == null)
        {
            throw new InvalidOperationException("Enrollment completed without a stored certificate.");
        }

        var certificate = await _unitOfWork.Certificates.GetByIdAsync(enrollmentEvent.IssuedCertificateId.Value, ct)
            ?? throw new InvalidOperationException("Issued certificate was not persisted.");

        certificate.EncryptedPrivateKeyPem = _dataProtection.Protect(privateKeyPem);
        device.LastCertificateId = certificate.Id;
        _unitOfWork.Certificates.Update(certificate);
        _unitOfWork.Devices.Update(device);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Demo-enrolled device {DeviceId} through backend {BackendId}", id, activeBackend.Id);

        return new DemoEnrollResponseDto
        {
            Device = DtoMapper.ToDto(device),
            Certificate = DtoMapper.ToDto(certificate),
            IssuedByActiveBackend = DtoMapper.ToDto(activeBackend)
        };
    }

    private static (byte[] Csr, string PrivateKeyPem) CreateDemoCsr(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            new X500DistinguishedName($"CN={commonName}"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return (request.CreateSigningRequest(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private async Task<Device> CreatePendingDeviceAsync(
        string displayName,
        string subjectCommonName,
        string? manufacturer,
        string? model,
        string? serialNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(subjectCommonName))
        {
            throw new ArgumentException("Subject common name is required.");
        }

        var normalizedCn = subjectCommonName.Trim();
        var existing = await _unitOfWork.Devices.GetBySubjectCommonNameAsync(normalizedCn, ct);
        if (existing != null)
        {
            throw new ArgumentException($"A device with subject common name '{normalizedCn}' already exists.");
        }

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            SubjectCommonName = normalizedCn,
            Manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            Status = DeviceStatus.Pending
        };

        _unitOfWork.Devices.Add(device);
        await _unitOfWork.SaveChangesAsync(ct);

        return device;
    }
}
