using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using RTSec.Kryptonian.Bridge;
using Xunit;

namespace RTSec.Kryptonian.Bridge.Tests;

public class DicomWebMultipartParserTests
{
    [Fact]
    public async Task ReadDicomPartsAsyncReturnsSinglePartDicomBody()
    {
        var parser = new DicomWebMultipartParser();
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");

        var parts = await parser.ReadDicomPartsAsync(content);

        parts.Should().ContainSingle()
            .Which.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReadDicomPartsAsyncExtractsMultipartDicomPayload()
    {
        var parser = new DicomWebMultipartParser();
        var payload = Encoding.ASCII.GetBytes(
            "--abc\r\nContent-Type: application/dicom\r\n\r\nDICOM1\r\n--abc\r\nContent-Type: application/dicom\r\n\r\nDICOM2\r\n--abc--\r\n");
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/related; type=\"application/dicom\"; boundary=abc");

        var parts = await parser.ReadDicomPartsAsync(content);

        parts.Should().HaveCount(2);
        Encoding.ASCII.GetString(parts[0]).Should().Be("DICOM1");
        Encoding.ASCII.GetString(parts[1]).Should().Be("DICOM2");
    }

    [Fact]
    public async Task ReadDicomPartsAsyncRejectsMultipartWithoutBoundary()
    {
        var parser = new DicomWebMultipartParser();
        using var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue("multipart/related");

        var act = () => parser.ReadDicomPartsAsync(content);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*boundary*");
    }
}
