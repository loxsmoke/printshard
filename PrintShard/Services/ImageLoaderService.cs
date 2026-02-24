using System.IO;
using System.Windows.Media.Imaging;

namespace PrintShard.Services;

public static class ImageLoaderService
{
    /// <summary>
    /// Supported file extensions. WIC on Windows handles the actual decoding,
    /// so additional codecs (e.g. WEBP, RAW) installed on the OS also work.
    /// </summary>
    public static readonly string[] SupportedExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".ico", ".wdp", ".hdp"];

    public static readonly string DialogFilter =
        "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp;*.ico;*.wdp;*.hdp|All Files|*.*";

    /// <summary>
    /// Loads an image from <paramref name="path"/> at full resolution.
    /// The BitmapImage is frozen so it can be used from any thread.
    /// </summary>
    public static BitmapSource Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);

        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource       = new Uri(path, UriKind.Absolute);
        bmp.CacheOption     = BitmapCacheOption.OnLoad;
        bmp.CreateOptions   = BitmapCreateOptions.PreservePixelFormat;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>
    /// Returns a rough estimate of RAM required to decode the image at full resolution (bytes).
    /// </summary>
    public static long EstimateMemoryBytes(string path)
    {
        try
        {
            var decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            // 4 bytes per pixel (BGRA32)
            return (long)frame.PixelWidth * frame.PixelHeight * 4;
        }
        catch
        {
            return 0;
        }
    }
}
