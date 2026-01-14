using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using RTSec.Kryptonian.Domain.Interfaces;

namespace RTSec.Kryptonian.Infrastructure.Security;

/// <summary>
/// Data protection service using ASP.NET Core Data Protection.
/// Provides encryption for sensitive data stored in the database.
/// </summary>
public class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionService> _logger;

    private const string Purpose = "RTSec.Kryptonian.SensitiveData.v1";

    public DataProtectionService(
        IDataProtectionProvider provider,
        ILogger<DataProtectionService> logger)
    {
        _protector = provider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        try
        {
            return _protector.Protect(plaintext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to protect data");
            throw new InvalidOperationException("Data protection failed", ex);
        }
    }

    /// <inheritdoc />
    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return string.Empty;
        }

        try
        {
            return _protector.Unprotect(ciphertext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unprotect data");
            throw new InvalidOperationException("Data unprotection failed", ex);
        }
    }
}
