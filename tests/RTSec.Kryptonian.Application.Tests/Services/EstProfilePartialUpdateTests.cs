using System.Text.Json;
using FluentAssertions;
using RTSec.Kryptonian.Application.DTOs;
using Xunit;

namespace RTSec.Kryptonian.Application.Tests.Services;

/// <summary>
/// A name-only PUT must not wipe list fields the caller never mentioned. The service applies
/// these collections only when non-null, so an omitted property has to deserialize to null and
/// an explicit empty array has to stay an empty list (deliberate clearing).
/// </summary>
public class EstProfilePartialUpdateTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void NameOnlyUpdateLeavesCollectionsNull()
    {
        var dto = JsonSerializer.Deserialize<EstProfileUpdateDto>("""{"name":"Renamed"}""", Options);

        dto!.Name.Should().Be("Renamed");
        dto.Hostnames.Should().BeNull();
        dto.AllowedKeyUsages.Should().BeNull();
        dto.TrustedClientCaThumbprints.Should().BeNull();

        // Unmentioned scalars stay null too, so the service cannot overwrite them either.
        dto.PathPrefix.Should().BeNull();
        dto.CaBackendId.Should().BeNull();
        dto.ValidityDays.Should().BeNull();
        dto.RequireClientCertificate.Should().BeNull();
        dto.ValidateClientCertificateChain.Should().BeNull();
        dto.IsEnabled.Should().BeNull();
    }

    [Fact]
    public void EmptyArrayStillMeansClearTheList()
    {
        var json = """{"hostnames":[],"allowedKeyUsages":[],"trustedClientCaThumbprints":[]}""";

        var dto = JsonSerializer.Deserialize<EstProfileUpdateDto>(json, Options);

        dto!.Hostnames.Should().NotBeNull("an explicit [] is a deliberate clear");
        dto.Hostnames.Should().BeEmpty();
        dto.AllowedKeyUsages.Should().NotBeNull();
        dto.AllowedKeyUsages.Should().BeEmpty();
        dto.TrustedClientCaThumbprints.Should().NotBeNull();
        dto.TrustedClientCaThumbprints.Should().BeEmpty();
    }

    [Fact]
    public void ProvidedCollectionsAndScalarsAreDeserialized()
    {
        var json = """
        {
          "name": "Scanner EST",
          "hostnames": ["est.example.com", ".hospital.local"],
          "hostnameMatchType": "suffix",
          "pathPrefix": "/.well-known/est/scanner",
          "caBackendId": "8b6f2b6c-1f4d-4f4a-9d0e-2f3a4b5c6d7e",
          "allowedKeyUsages": ["digitalSignature", "keyEncipherment"],
          "validityDays": 90,
          "requireClientCertificate": false,
          "validateClientCertificateChain": true,
          "trustedClientCaThumbprints": ["AABBCC"],
          "isEnabled": true
        }
        """;

        var dto = JsonSerializer.Deserialize<EstProfileUpdateDto>(json, Options);

        dto!.Name.Should().Be("Scanner EST");
        dto.Hostnames.Should().Equal("est.example.com", ".hospital.local");
        dto.HostnameMatchType.Should().Be("suffix");
        dto.PathPrefix.Should().Be("/.well-known/est/scanner");
        dto.CaBackendId.Should().Be("8b6f2b6c-1f4d-4f4a-9d0e-2f3a4b5c6d7e");
        dto.AllowedKeyUsages.Should().Equal("digitalSignature", "keyEncipherment");
        dto.ValidityDays.Should().Be(90);
        dto.RequireClientCertificate.Should().BeFalse();
        dto.ValidateClientCertificateChain.Should().BeTrue();
        dto.TrustedClientCaThumbprints.Should().Equal("AABBCC");
        dto.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreateDtoKeepsItsNonNullCollectionDefaults()
    {
        // Create is not partial: omitting a list there legitimately means "none".
        var dto = JsonSerializer.Deserialize<EstProfileCreateDto>("""{"name":"New"}""", Options);

        dto!.Hostnames.Should().NotBeNull();
        dto.AllowedKeyUsages.Should().NotBeNull();
        dto.TrustedClientCaThumbprints.Should().NotBeNull();
    }
}
