using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service interface for CA backend operations.
/// </summary>
public interface ICaBackendService
{
    /// <summary>
    /// Gets all CA backends.
    /// </summary>
    Task<IEnumerable<CaBackendDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a CA backend by ID.
    /// </summary>
    Task<CaBackendDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CaBackendDto?> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new CA backend.
    /// </summary>
    Task<CaBackendDto> CreateAsync(CaBackendCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing CA backend.
    /// </summary>
    Task<CaBackendDto?> UpdateAsync(Guid id, CaBackendUpdateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes a CA backend.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tests connection to a CA backend.
    /// </summary>
    Task<bool> TestConnectionAsync(Guid id, CancellationToken ct = default);

    Task<CaBackendDto?> ActivateAsync(Guid id, CancellationToken ct = default);
}
