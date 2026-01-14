using AutoMapper;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Application.DTOs;
using RTSec.Kryptonian.Domain.Entities;
using RTSec.Kryptonian.Domain.Enums;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service for CA backend CRUD operations.
/// </summary>
public class CaBackendService : ICaBackendService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CaBackendService> _logger;
    private readonly ICaConnectorFactory _connectorFactory;

    public CaBackendService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CaBackendService> logger,
        ICaConnectorFactory connectorFactory)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _connectorFactory = connectorFactory;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CaBackendDto>> GetAllAsync(CancellationToken ct = default)
    {
        var backends = await _unitOfWork.CaBackends.GetAllAsync(ct);
        return _mapper.Map<IEnumerable<CaBackendDto>>(backends);
    }

    /// <inheritdoc />
    public async Task<CaBackendDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var backend = await _unitOfWork.CaBackends.GetByIdAsync(id, ct);
        return backend == null ? null : _mapper.Map<CaBackendDto>(backend);
    }

    /// <inheritdoc />
    public async Task<CaBackendDto> CreateAsync(CaBackendCreateDto dto, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating CA backend: {Name}, Type: {Type}", dto.Name, dto.Type);

        var entity = _mapper.Map<CaBackend>(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.CaBackends.Add(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created CA backend with ID: {Id}", entity.Id);

        return _mapper.Map<CaBackendDto>(entity);
    }

    /// <inheritdoc />
    public async Task<CaBackendDto?> UpdateAsync(Guid id, CaBackendUpdateDto dto, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.CaBackends.GetByIdAsync(id, ct);
        if (entity == null)
        {
            _logger.LogWarning("CA backend not found for update: {Id}", id);
            return null;
        }

        _logger.LogInformation("Updating CA backend: {Id}", id);

        // Apply partial updates
        if (dto.Name != null)
            entity.Name = dto.Name;

        if (dto.Type != null)
        {
            if (!TryParseCaBackendType(dto.Type, out var backendType))
            {
                _logger.LogWarning("Invalid CA backend type: {Type}", dto.Type);
                throw new ArgumentException($"Invalid CA backend type: {dto.Type}. Valid types are: adcs, ejbca, cfssl, selfsigned, acme");
            }
            entity.Type = backendType;
        }

        if (dto.Url != null)
            entity.Url = dto.Url;

        if (dto.Config != null)
            entity.Config = dto.Config;

        if (dto.IsEnabled.HasValue)
            entity.IsEnabled = dto.IsEnabled.Value;

        entity.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.CaBackends.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated CA backend: {Id}", id);

        return _mapper.Map<CaBackendDto>(entity);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.CaBackends.GetByIdAsync(id, ct);
        if (entity == null)
        {
            _logger.LogWarning("CA backend not found for deletion: {Id}", id);
            return false;
        }

        // Check if any EST profiles are using this backend
        var profiles = await _unitOfWork.EstProfiles.GetByCaBackendIdAsync(id, ct);
        if (profiles.Any())
        {
            _logger.LogWarning("Cannot delete CA backend {Id}: {Count} EST profiles are using it", id, profiles.Count());
            throw new InvalidOperationException($"Cannot delete CA backend: {profiles.Count()} EST profile(s) are using it");
        }

        _logger.LogInformation("Deleting CA backend: {Id}", id);

        _unitOfWork.CaBackends.Delete(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted CA backend: {Id}", id);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _unitOfWork.CaBackends.GetByIdAsync(id, ct);
        if (entity == null)
        {
            _logger.LogWarning("CA backend not found for connection test: {Id}", id);
            return false;
        }

        try
        {
            var connector = _connectorFactory.CreateConnector(entity);
            return await connector.TestConnectionAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for CA backend: {Id}", id);
            return false;
        }
    }

    private static bool TryParseCaBackendType(string type, out CaBackendType result)
    {
        result = type.ToLowerInvariant() switch
        {
            "adcs" => CaBackendType.Adcs,
            "ejbca" => CaBackendType.Ejbca,
            "cfssl" => CaBackendType.Cfssl,
            "selfsigned" => CaBackendType.SelfSigned,
            "acme" => CaBackendType.Acme,
            _ => (CaBackendType)(-1) // Invalid marker
        };

        return (int)result >= 0;
    }
}
