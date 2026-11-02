using SkiaSharp;

namespace PijacaUDzepu.API.Helpers;

public static class ImageResizer
{
    public const string ContentType = "image/jpeg";

    // Smanjuje sliku da stane u maxWidth x maxHeight (čuva odnos stranica) i vraća JPEG
    public static async Task<byte[]> ToJpeg(IFormFile file, int maxWidth = 800, int maxHeight = 600, int quality = 80)
    {
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using var original = SKBitmap.Decode(memoryStream)
            ?? throw new InvalidOperationException("Neispravan format slike.");

        var bitmap = original;
        if (original.Width > maxWidth || original.Height > maxHeight)
        {
            var ratio = Math.Min((float)maxWidth / original.Width, (float)maxHeight / original.Height);
            var newWidth = (int)(original.Width * ratio);
            var newHeight = (int)(original.Height * ratio);
            bitmap = original.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default);
        }

        try
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            return data.ToArray();
        }
        finally
        {
            if (bitmap != original) bitmap.Dispose();
        }
    }
}
