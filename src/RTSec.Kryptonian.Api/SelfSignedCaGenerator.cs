using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace RTSec.Kryptonian.Api;

internal static class SelfSignedCaGenerator
{
    internal static object Generate(string pfxPath, string password, string commonName)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.Length > 1024)
            throw new ArgumentException("Use a PFX password of 12 to 1024 characters.");
        if (string.IsNullOrWhiteSpace(commonName) || commonName.Length > 64 || commonName.Any(char.IsControl))
            throw new ArgumentException("CA name must contain 1 to 64 characters without control characters.");
        if (string.IsNullOrWhiteSpace(pfxPath) || !Path.IsPathFullyQualified(pfxPath)
            || pfxPath.StartsWith(@"\\", StringComparison.Ordinal) || pfxPath.StartsWith("//", StringComparison.Ordinal)
            || (OperatingSystem.IsWindows() && pfxPath[2..].Contains(':'))
            || !(Path.GetExtension(pfxPath).Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(pfxPath).Equals(".p12", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose an absolute local server path ending in .pfx or .p12 (not a network path).");

        var path = Path.GetFullPath(pfxPath);
        if (OperatingSystem.IsWindows() && new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new ArgumentException("Generate certificates on a local disk, not a mapped network drive.");
        var directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
        if (!directory.Exists)
            throw new ArgumentException("The destination directory must already exist and be writable by the gateway service.");
        for (var parent = directory; parent != null; parent = parent.Parent)
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Certificate paths cannot pass through symbolic links or junctions.");
        if (File.Exists(path))
            throw new IOException("Destination already exists.");

        var subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(commonName.Trim());
        using var key = RSA.Create(3072);
        var request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(5));
        var pfx = cert.Export(X509ContentType.Pfx, password);
        try
        {
            // CreateNew is the final overwrite guard. Set private permissions at creation,
            // before writing any key bytes; never change the parent directory's ACL.
            using var stream = CreatePrivateFile(path);
            stream.Write(pfx);
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
        return new { pfxPath = path, thumbprint = cert.Thumbprint, notAfter = cert.NotAfter.ToUniversalTime() };
    }

    private static FileStream CreatePrivateFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { identity.User!,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) }.Distinct())
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.None, security);
        }
        return new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
    }
}
