using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

public interface IDeviceService
{
    Task<IEnumerable<DeviceDto>> GetAllAsync(CancellationToken ct = default);
    Task<DeviceDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeviceDto> CreateAsync(DeviceCreateDto dto, CancellationToken ct = default);
    Task<DeviceApprovalRequestResponseDto> RequestApprovalAsync(DeviceApprovalRequestDto dto, CancellationToken ct = default);
    Task<DeviceActivationCodeDto?> GenerateActivationCodeAsync(Guid id, DeviceActivationCodeCreateDto dto, CancellationToken ct = default);
    Task<DeviceActivationCodeDto?> ReactivateActivationCodeAsync(Guid id, DeviceActivationCodeCreateDto dto, CancellationToken ct = default);
    Task<DeviceDto?> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<DeviceDto?> RemoveAsync(Guid id, CancellationToken ct = default);
    Task<bool> PurgeAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<CertificateDto>> GetCertificatesAsync(Guid id, CancellationToken ct = default);
    Task<DemoEnrollResponseDto?> DemoEnrollAsync(Guid id, Guid profileId, CancellationToken ct = default);
}
