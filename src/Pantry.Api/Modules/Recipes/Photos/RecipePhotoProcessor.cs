using SkiaSharp;

namespace Pantry.Api.Modules.Recipes.Photos;

/// <summary>
/// Turns an uploaded picture into the stored sizes (ADR 0025): JPEG, PNG or WebP in, WebP out, turned upright by its
/// EXIF orientation. Only pixels are encoded, so no metadata (a phone's location among it) reaches the volume.
/// </summary>
public static class RecipePhotoProcessor
{
    public const string ContentType = "image/webp";
    public const int CardSide = 480;
    public const int DetailMaxSide = 1600;
    public const long MaxPixels = 50_000_000;

    private const int Quality = 80;

    private static readonly IReadOnlySet<SKEncodedImageFormat> Accepted = new HashSet<SKEncodedImageFormat>
    {
        SKEncodedImageFormat.Jpeg,
        SKEncodedImageFormat.Png,
        SKEncodedImageFormat.Webp,
    };

    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>The stored sizes of <paramref name="upload"/>, or null when it is not a JPEG, PNG or WebP picture this can read.</summary>
    public static IReadOnlyDictionary<RecipePhotoSize, byte[]>? Process(byte[] upload)
    {
        if (upload.Length == 0)
        {
            return null;
        }

        using var data = SKData.CreateCopy(upload);
        using var codec = SKCodec.Create(data);
        if (codec is null || !Accepted.Contains(codec.EncodedFormat) || (long)codec.Info.Width * codec.Info.Height > MaxPixels)
        {
            return null;
        }

        using var decoded = Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        using var detail = Upright(decoded, codec.EncodedOrigin);
        using var card = SquareCrop(detail);
        return new Dictionary<RecipePhotoSize, byte[]>
        {
            [RecipePhotoSize.Card] = Encode(card),
            [RecipePhotoSize.Detail] = Encode(detail),
        };
    }

    /// <summary>Decodes at the smallest scale the codec offers that still covers the detail size (JPEG decodes in eighths).</summary>
    private static SKImage? Decode(SKCodec codec)
    {
        var longer = Math.Max(codec.Info.Width, codec.Info.Height);
        var size = longer > DetailMaxSide ? codec.GetScaledDimensions((float)DetailMaxSide / longer) : codec.Info.Size;
        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
        {
            return null;
        }

        bitmap.SetImmutable();
        return SKImage.FromBitmap(bitmap);
    }

    private static SKImage Upright(SKImage image, SKEncodedOrigin origin)
    {
        var (w, h) = (image.Width, image.Height);
        var turned = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (uprightWidth, uprightHeight) = turned ? (h, w) : (w, h);
        var scale = Math.Min(1f, (float)DetailMaxSide / Math.Max(uprightWidth, uprightHeight));
        var orientation = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var width = Math.Max(1, (int)Math.Round(uprightWidth * scale));
        var height = Math.Max(1, (int)Math.Round(uprightHeight * scale));
        return Draw(width, height, canvas =>
        {
            canvas.SetMatrix(SKMatrix.CreateScale(scale, scale).PreConcat(orientation));
            canvas.DrawImage(image, 0, 0, Sampling);
        });
    }

    private static SKImage SquareCrop(SKImage image)
    {
        var side = Math.Min(image.Width, image.Height);
        var source = SKRect.Create((image.Width - side) / 2f, (image.Height - side) / 2f, side, side);
        return Draw(CardSide, CardSide, canvas => canvas.DrawImage(image, source, SKRect.Create(CardSide, CardSide), Sampling));
    }

    private static SKImage Draw(int width, int height, Action<SKCanvas> draw)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            ?? throw new InvalidOperationException($"Could not create a {width}x{height} surface.");
        draw(surface.Canvas);
        return surface.Snapshot();
    }

    private static byte[] Encode(SKImage image)
    {
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, Quality)
            ?? throw new InvalidOperationException("Could not encode the photo as WebP.");
        return encoded.ToArray();
    }
}
