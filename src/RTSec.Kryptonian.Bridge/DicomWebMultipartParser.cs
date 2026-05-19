using System.Net.Http.Headers;

namespace RTSec.Kryptonian.Bridge;

public class DicomWebMultipartParser
{
    public async Task<IReadOnlyList<byte[]>> ReadDicomPartsAsync(HttpContent content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var mediaType = content.Headers.ContentType;
        if (mediaType == null || mediaType.MediaType?.Contains("multipart/", StringComparison.OrdinalIgnoreCase) != true)
        {
            return [await content.ReadAsByteArrayAsync(ct)];
        }

        var boundary = GetBoundary(mediaType)
            ?? throw new InvalidOperationException("DICOMweb multipart response is missing a boundary.");

        var payload = await content.ReadAsByteArrayAsync(ct);
        return ParseMultipart(payload, boundary);
    }

    private static string? GetBoundary(MediaTypeHeaderValue contentType)
    {
        var boundary = contentType.Parameters
            .FirstOrDefault(p => p.Name.Equals("boundary", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return boundary?.Trim('"');
    }

    private static IReadOnlyList<byte[]> ParseMultipart(byte[] payload, string boundary)
    {
        var marker = System.Text.Encoding.ASCII.GetBytes("--" + boundary);
        var headerSeparator = System.Text.Encoding.ASCII.GetBytes("\r\n\r\n");
        var parts = new List<byte[]>();

        var position = 0;
        while (position < payload.Length)
        {
            var markerIndex = IndexOf(payload, marker, position);
            if (markerIndex < 0)
            {
                break;
            }

            var partStart = markerIndex + marker.Length;
            if (partStart + 1 < payload.Length && payload[partStart] == (byte)'-' && payload[partStart + 1] == (byte)'-')
            {
                break;
            }

            if (partStart + 1 < payload.Length && payload[partStart] == (byte)'\r' && payload[partStart + 1] == (byte)'\n')
            {
                partStart += 2;
            }

            var headerEnd = IndexOf(payload, headerSeparator, partStart);
            if (headerEnd < 0)
            {
                throw new InvalidOperationException("Malformed DICOMweb multipart response: missing part header terminator.");
            }

            var bodyStart = headerEnd + headerSeparator.Length;
            var nextMarker = IndexOf(payload, marker, bodyStart);
            if (nextMarker < 0)
            {
                throw new InvalidOperationException("Malformed DICOMweb multipart response: missing closing boundary.");
            }

            var bodyEnd = nextMarker;
            if (bodyEnd >= 2 && payload[bodyEnd - 2] == (byte)'\r' && payload[bodyEnd - 1] == (byte)'\n')
            {
                bodyEnd -= 2;
            }

            if (bodyEnd > bodyStart)
            {
                parts.Add(payload[bodyStart..bodyEnd]);
            }

            position = nextMarker;
        }

        if (parts.Count == 0)
        {
            throw new InvalidOperationException("DICOMweb multipart response contained no DICOM payload parts.");
        }

        return parts;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
    {
        for (var i = startIndex; i <= haystack.Length - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return i;
            }
        }

        return -1;
    }
}
