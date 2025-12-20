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

        // Path to the local RA Badge directory detected from the loaded project path
        public static string? LocalBadgeDirectory { get; private set; }

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
            LocalBadgeDirectory = null;
        }

        /// <summary>
        /// Attempts to locate the "Badge" directory relative to the loaded file.
        /// Standard RA Structure: ~RACache\Data\User.txt -> ~RACache\Badge\Image.png
        /// </summary>
        public static void ConfigureLocalPaths(string loadedFilePath)
        {
            LocalBadgeDirectory = null;
            if (string.IsNullOrEmpty(loadedFilePath)) return;

            try
            {
                string? dataDir = Path.GetDirectoryName(loadedFilePath);
                if (dataDir == null) return;

                // 1. Check sibling "Badge" directory (Standard RA)
                // loadedFilePath is inside "Data", so we go up one level and look for "Badge"
                DirectoryInfo? parent = Directory.GetParent(dataDir);
                if (parent != null)
                {
                    string siblingBadgeDir = Path.Combine(parent.FullName, "Badge");
                    if (Directory.Exists(siblingBadgeDir))
                    {
                        LocalBadgeDirectory = siblingBadgeDir;
                        return;
                    }
                }

                // 2. Check subdirectory "Badge" (Non-standard / Flat)
                string subBadgeDir = Path.Combine(dataDir, "Badge");
                if (Directory.Exists(subBadgeDir))
                {
                    LocalBadgeDirectory = subBadgeDir;
                }
            }
            catch { /* Ignore path errors */ }
        }

        public static async Task<Image> GetBadgeAsync(string badgeName)
        {
            if (string.IsNullOrEmpty(badgeName) || badgeName == "00000")
                return CreatePlaceholder();

            // Normalize "local\hash.png" or "local/hash.png" to just "hash"
            string cleanName = badgeName;

            // Strip directory info (local\)
            if (cleanName.Contains("\\") || cleanName.Contains("/"))
            {
                cleanName = Path.GetFileName(cleanName);
            }

            // Remove extension to normalize cache key
            // (RA Badges are typically accessed by hash, saved as .png)
            if (cleanName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = Path.GetFileNameWithoutExtension(cleanName);
            }

            // 1. Check Memory
            if (_memoryCache.ContainsKey(cleanName))
                return _memoryCache[cleanName];

            // 2. Check Local RA Directory (The "Clever" Lookup)
            // If we detected a Badge folder near the User.txt, check there first.
            if (!string.IsNullOrEmpty(LocalBadgeDirectory))
            {
                string raLocalPath = Path.Combine(LocalBadgeDirectory, $"{cleanName}.png");
                if (File.Exists(raLocalPath))
                {
                    var img = LoadImageSafe(raLocalPath);
                    if (img != null)
                    {
                        _memoryCache[cleanName] = img;
                        return img;
                    }
                }
            }

            // 3. Check Internal App Cache
            string appCachePath = Path.Combine(CacheDir, $"{cleanName}.png");
            if (File.Exists(appCachePath))
            {
                var img = LoadImageSafe(appCachePath);
                if (img != null)
                {
                    _memoryCache[cleanName] = img;
                    return img;
                }
            }

            try
            {
                // 4. Download from RA
                string url = $"https://media.retroachievements.org/Badge/{cleanName}.png";
                var data = await _client.GetByteArrayAsync(url);

                await File.WriteAllBytesAsync(appCachePath, data);
                var img = LoadImageSafe(appCachePath);

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
                // Use FileStream to avoid locking issues
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    // Copy to memory stream to keep file unlocked and avoid GDI+ generic errors
                    using (var ms = new MemoryStream())
                    {
                        fs.CopyTo(ms);
                        ms.Position = 0;
                        return new Bitmap(ms);
                    }
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