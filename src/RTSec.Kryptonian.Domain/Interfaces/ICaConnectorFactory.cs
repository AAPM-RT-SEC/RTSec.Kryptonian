using RTSec.Kryptonian.Domain.Entities;

namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Factory for creating CA connectors.
/// </summary>
public interface ICaConnectorFactory
{
    /// <summary>
    /// Creates a CA connector for the specified backend.
    /// </summary>
    ICaConnector CreateConnector(CaBackend backend);

    /// <summary>
    /// Gets a cached connector for the specified backend ID (if available).
    /// </summary>
    ICaConnector? GetCachedConnector(Guid backendId);
}
