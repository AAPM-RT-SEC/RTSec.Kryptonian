using Kryptonian.MedicalDevice.Enrollment;

namespace Kryptonian.MedicalDevice.Enrollment.Tests;

public class InstalledCertificateRenewalTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(19, false)]
    [InlineData(20, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    public void RenewsAfterTwoThirdsOfLifetimeButNeverUsesExpiredCredential(int day, bool due)
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(due, InstalledCertificateRenewal.IsDue(start.AddDays(day), start, start.AddDays(30)));
    }
}
