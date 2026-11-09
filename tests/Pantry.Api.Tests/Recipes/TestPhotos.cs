using SkiaSharp;

namespace Pantry.Api.Tests.Recipes;

/// <summary>Pictures made on the fly for photo tests: the left half red and the right half blue, so a turn shows.</summary>
public static class TestPhotos
{
    public static byte[] Jpeg(int width, int height, ushort? exifOrientation = null)
    {
        var jpeg = Encode(width, height, SKEncodedImageFormat.Jpeg);
        return exifOrientation is { } orientation ? WithExifOrientation(jpeg, orientation) : jpeg;
    }

    public static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static SKBitmap Decode(byte[] content) => SKBitmap.Decode(content);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Blue);
        using (var paint = new SKPaint { Color = SKColors.Red })
        {
            surface.Canvas.DrawRect(0, 0, width / 2f, height, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>Puts an EXIF segment with only an orientation (and a camera make, to check it is dropped) after the JPEG start marker.</summary>
    private static byte[] WithExifOrientation(byte[] jpeg, ushort orientation)
    {
        byte[] make = "TestCam\0"u8.ToArray();
        var tiff = new List<byte> { (byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 2 };
        tiff.AddRange([0x01, 0x0F, 0, 2, 0, 0, 0, (byte)make.Length, 0, 0, 0, 38]);
        tiff.AddRange([0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0]);
        tiff.AddRange([0, 0, 0, 0]);
        tiff.AddRange(make);

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        var segment = new byte[] { 0xFF, 0xE1, (byte)(length >> 8), (byte)length }.Concat(payload);
        return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
    }
}
