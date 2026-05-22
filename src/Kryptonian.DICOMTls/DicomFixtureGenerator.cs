using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using System.Globalization;

namespace Kryptonian.DICOMTls;

internal sealed record GeneratedDicomFile(string Path, string SopInstanceUid);

internal static class DicomFixtureGenerator
{
    public static async Task<IReadOnlyList<GeneratedDicomFile>> CreateAsync(int count, string outputPath)
    {
        Directory.CreateDirectory(outputPath);
        var generated = new List<GeneratedDicomFile>(count);

        for (var i = 1; i <= count; i++)
        {
            var sopInstanceUid = DicomUID.Generate();
            var studyUid = DicomUID.Generate();
            var seriesUid = DicomUID.Generate();
            var now = DateTime.UtcNow;
            var dataset = new DicomDataset
            {
                { DicomTag.SpecificCharacterSet, "ISO_IR 100" },
                { DicomTag.PatientName, "Kryptonian^Demo" },
                { DicomTag.PatientID, $"KRYPTONIAN-{i:000}" },
                { DicomTag.StudyInstanceUID, studyUid },
                { DicomTag.SeriesInstanceUID, seriesUid },
                { DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage },
                { DicomTag.SOPInstanceUID, sopInstanceUid },
                { DicomTag.Modality, "OT" },
                { DicomTag.StudyDate, now.ToString("yyyyMMdd", CultureInfo.InvariantCulture) },
                { DicomTag.StudyTime, now.ToString("HHmmss", CultureInfo.InvariantCulture) },
                { DicomTag.SeriesNumber, i },
                { DicomTag.InstanceNumber, i },
                { DicomTag.SamplesPerPixel, (ushort)1 },
                { DicomTag.PhotometricInterpretation, PhotometricInterpretation.Monochrome2.Value },
                { DicomTag.Rows, (ushort)64 },
                { DicomTag.Columns, (ushort)64 },
                { DicomTag.BitsAllocated, (ushort)8 },
                { DicomTag.BitsStored, (ushort)8 },
                { DicomTag.HighBit, (ushort)7 },
                { DicomTag.PixelRepresentation, (ushort)0 }
            };

            var pixels = new byte[64 * 64];
            for (var pixel = 0; pixel < pixels.Length; pixel++)
            {
                pixels[pixel] = (byte)((pixel + (i * 17)) % 256);
            }

            var pixelData = DicomPixelData.Create(dataset, true);
            pixelData.AddFrame(new MemoryByteBuffer(pixels));

            var file = new DicomFile(dataset);
            var path = System.IO.Path.Combine(outputPath, $"dummy-{i:000}-{sopInstanceUid}.dcm");
            await file.SaveAsync(path);
            generated.Add(new GeneratedDicomFile(path, sopInstanceUid.UID));
        }

        return generated;
    }
}
