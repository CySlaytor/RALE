using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public static class ImageCache
    {
        // Saves to bin/Debug/Cache/Badges
        private static readonly string CacheDir = Path.Combine(Application.StartupPath, "Cache", "Badges");
        private static readonly HttpClient _client = new HttpClient();

        // In-memory cache for this session
        private static Dictionary<string, Image> _memoryCache = new Dictionary<string, Image>();

        static ImageCache()
        {
            if (!Directory.Exists(CacheDir))
                Directory.CreateDirectory(CacheDir);
        }

        public static void Clear()
        {
            foreach (var img in _memoryCache.Values)
            {
                img.Dispose();
            }
            _memoryCache.Clear();
        }

        public static async Task<Image> GetBadgeAsync(string badgeName)
        {
            if (string.IsNullOrEmpty(badgeName) || badgeName == "00000")
                return CreatePlaceholder();

            // Handle local paths e.g. "local\\hash.png" or "local/hash.png"
            // We extract the filename (hash) to use as the cache key and download target
            string cleanName = badgeName;

            // Normalize slashes
            if (cleanName.Contains("\\") || cleanName.Contains("/"))
            {
                cleanName = Path.GetFileName(cleanName);
            }

            // Remove extension if present to normalize cache key (RA Badges are typically accessed by hash, saved as .png)
            if (cleanName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = Path.GetFileNameWithoutExtension(cleanName);
            }

            // 1. Check Memory
            if (_memoryCache.ContainsKey(cleanName))
                return _memoryCache[cleanName];

            string localPath = Path.Combine(CacheDir, $"{cleanName}.png");

            // 2. Check Disk
            if (File.Exists(localPath))
            {
                var img = LoadImageSafe(localPath);
                if (img != null)
                {
                    _memoryCache[cleanName] = img;
                    return img;
                }
            }

            try
            {
                // 3. Download from RA
                // Even for "local" badges, if they follow the hash naming convention, they might exist on the server.
                // If not, this fails gracefully to placeholder.
                string url = $"https://media.retroachievements.org/Badge/{cleanName}.png";
                var data = await _client.GetByteArrayAsync(url);

                await File.WriteAllBytesAsync(localPath, data);
                var img = LoadImageSafe(localPath);

                if (img != null)
                {
                    _memoryCache[cleanName] = img;
                    return img;
                }
                return CreatePlaceholder();
            }
            catch
            {
                return CreatePlaceholder();
            }
        }

        private static Image? LoadImageSafe(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    return new Bitmap(fs);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image CreatePlaceholder()
        {
            var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightGray);
                g.DrawRectangle(Pens.Gray, 0, 0, 63, 63);
                g.DrawString("?", SystemFonts.DefaultFont, Brushes.Gray, 25, 25);
            }
            return bmp;
        }
    }
}