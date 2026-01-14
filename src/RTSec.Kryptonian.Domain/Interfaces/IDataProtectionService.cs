namespace RTSec.Kryptonian.Domain.Interfaces;

/// <summary>
/// Service for encrypting and decrypting sensitive data.
/// Used to protect ACME account keys and other secrets stored in the database.
/// </summary>
public interface IDataProtectionService
{
    /// <summary>
    /// Encrypts the specified plaintext.
    /// </summary>
    /// <param name="plaintext">The plaintext to encrypt.</param>
    /// <returns>The encrypted ciphertext (base64 encoded).</returns>
    string Protect(string plaintext);

    /// <summary>
    /// Decrypts the specified ciphertext.
    /// </summary>
    /// <param name="ciphertext">The ciphertext to decrypt (base64 encoded).</param>
    /// <returns>The decrypted plaintext.</returns>
    string Unprotect(string ciphertext);
}
