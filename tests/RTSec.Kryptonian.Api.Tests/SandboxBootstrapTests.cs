using FluentAssertions;
using Microsoft.Extensions.Configuration;
using RTSec.Kryptonian.Api;

namespace RTSec.Kryptonian.Api.Tests;

public class SandboxBootstrapTests
{
    [Fact]
    public void InitializeRejectsExistingDatabaseWithoutAuthorityMaterial()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KryptonianSandboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "kryptonian.db"), "existing state");
            var action = () => SandboxBootstrap.Initialize(Configuration(directory));
            action.Should().Throw<InvalidOperationException>().WithMessage("*incomplete*");
            File.Exists(Path.Combine(directory, "ca.pfx")).Should().BeFalse();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public void InitializeCreatesAndReusesACompletePrivateState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KryptonianSandboxTests", Guid.NewGuid().ToString("N"));
        try
        {
            var configuration = Configuration(directory);
            var first = SandboxBootstrap.Initialize(configuration);
            var firstSecret = first.Configuration["Kryptonian:Auth:JwtSecret"];
            var second = SandboxBootstrap.Initialize(configuration);

            first.DataDirectory.Should().Be(directory);
            second.Configuration["Kryptonian:Auth:JwtSecret"].Should().Be(firstSecret);
            File.ReadAllText(Path.Combine(directory, "secrets.json")).Should().Contain("\"caPfxPassword\"")
                .And.Contain("\"pfxPassword\"").And.Contain("\"jwtSecret\"").And.Contain("\"adminApiKey\"");
            first.Configuration["Kryptonian:Tls:RedirectHttpToHttps"].Should().Be("false");
            foreach (var file in new[] { "secrets.json", "ca.pfx", "ca.pem", "server.pfx", "admin-ca.pfx", "admin-ca.pem", "admin.pfx" })
                File.Exists(Path.Combine(directory, file)).Should().BeTrue();
            Directory.Exists(Path.Combine(directory, "dataprotection")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InitializeRejectsPartialStateWithoutGeneratingNewAuthority()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KryptonianSandboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "ca.pfx"), "partial");
            var action = () => SandboxBootstrap.Initialize(Configuration(directory));

            action.Should().Throw<InvalidOperationException>().WithMessage("*incomplete*");
            File.ReadAllText(Path.Combine(directory, "ca.pfx")).Should().Be("partial");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void InitializeRejectsOrphanedDataProtectionState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "KryptonianSandboxTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "dataprotection"));
        try
        {
            File.WriteAllText(Path.Combine(directory, "dataprotection", "key.xml"), "partial");

            var action = () => SandboxBootstrap.Initialize(Configuration(directory));

            action.Should().Throw<InvalidOperationException>().WithMessage("*incomplete*");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(7445, "/api/status/health", "GET", true)]
    [InlineData(7445, "/.well-known/est/simpleenroll", "POST", false)]
    [InlineData(7443, "/.well-known/est/simpleenroll", "POST", true)]
    [InlineData(7443, "/api/status/health", "GET", false)]
    [InlineData(7444, "/api/crl/issuer.crl", "GET", true)]
    [InlineData(7444, "/api/crl/issuer.crl", "POST", false)]
    public void ListenerBoundariesAllowOnlyTheirAssignedRoutes(int port, string path, string method, bool expected) =>
        SandboxBootstrap.AllowsRequest(port, 7445, 7443, 7444, path, method).Should().Be(expected);

    private static IConfiguration Configuration(string directory) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kryptonian:Sandbox:DataDirectory"] = directory,
            ["Kryptonian:Sandbox:AdminPort"] = "7445",
            ["Kryptonian:Sandbox:EstPort"] = "7443",
            ["Kryptonian:Sandbox:CrlPort"] = "7444"
        })
        .Build();
}
