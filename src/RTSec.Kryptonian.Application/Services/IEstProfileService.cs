using RTSec.Kryptonian.Application.DTOs;

namespace RTSec.Kryptonian.Application.Services;

/// <summary>
/// Service interface for EST profile operations.
/// </summary>
public interface IEstProfileService
{
    /// <summary>
    /// Gets all EST profiles.
    /// </summary>
    Task<IEnumerable<EstProfileDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets an EST profile by ID.
    /// </summary>
    Task<EstProfileDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new EST profile.
    /// </summary>
    Task<EstProfileDto> CreateAsync(EstProfileCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing EST profile.
    /// </summary>
    Task<EstProfileDto?> UpdateAsync(Guid id, EstProfileUpdateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Deletes an EST profile.
    /// </summary>
    /// <returns>True if deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
