using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BASpark
{
    public static class SidebarBackgroundHelper
    {
        private const int MaxFileBytes = 8 * 1024 * 1024;
        private const int DecodePixelWidth = 360;

        private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };
        private static readonly SemaphoreSlim LoadGate = new(1, 1);

        private static volatile string? _cachedPath;
        private static volatile ImageBrush? _cachedBrush;

        public static bool IsSupportedImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string ext = Path.GetExtension(path);
            return Array.Exists(AllowedExtensions, item =>
                item.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        public static async Task<ImageBrush?> LoadBrushAsync(string? path, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path) || !IsSupportedImage(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length <= 0 || fileInfo.Length > MaxFileBytes)
                {
                    AppLogger.Warn($"Sidebar background ignored: file size out of range ({fileInfo.Length} bytes).");
                    return null;
                }

                string normalizedPath = Path.GetFullPath(path);

                if (string.Equals(_cachedPath, normalizedPath, StringComparison.OrdinalIgnoreCase) && _cachedBrush != null)
                {
                    return _cachedBrush;
                }

                await LoadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (string.Equals(_cachedPath, normalizedPath, StringComparison.OrdinalIgnoreCase) && _cachedBrush != null)
                    {
                        return _cachedBrush;
                    }

                    ImageBrush? brush = await Task.Run(() => 
                    {
                        return DecodeBrushFromFile(normalizedPath);
                    }, cancellationToken).ConfigureAwait(false);

                    _cachedPath = brush == null ? null : normalizedPath;
                    _cachedBrush = brush;

                    return brush;
                }
                finally
                {
                    LoadGate.Release();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to load sidebar background image.", ex);
                return null;
            }
        }

        public static void ClearCache()
        {
            _cachedPath = null;
            _cachedBrush = null;
        }

        private static ImageBrush? DecodeBrushFromFile(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = fs;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = DecodePixelWidth;
                bitmap.EndInit();
                bitmap.Freeze(); 

                var brush = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = 0.6
                };
                
                brush.Freeze();
                return brush;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to decode sidebar background image from path: {filePath}", ex);
                return null;
            }
        }
    }
}