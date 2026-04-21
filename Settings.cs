using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ClaudeUsageBar
{
    public static class Settings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClaudeUsageBar.config");

        private static Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

        static Settings() => Load();

        // ── File I/O ─────────────────────────────────────────────────────

        private static void Load()
        {
            _values.Clear();
            if (!File.Exists(FilePath)) return;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                var t = line.Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith('#') || t.StartsWith('['))
                    continue;
                int eq = t.IndexOf('=');
                if (eq < 1) continue;
                _values[t[..eq].Trim()] = t[(eq + 1)..].Trim();
            }
        }

        public static void SaveAll()
        {
            var lines = new List<string> { "# ClaudeUsageBar settings", "" };
            foreach (var kv in _values)
                lines.Add($"{kv.Key}={kv.Value}");
            try { File.WriteAllLines(FilePath, lines); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] Save failed: {ex.Message}");
            }
        }

        private static string Get(string key, string def = "") =>
            _values.TryGetValue(key, out var v) ? v : def;

        private static void Set(string key, string value) => _values[key] = value;

        // ── Typed helpers ─────────────────────────────────────────────────

        public static int LoadRefreshIntervalMs() =>
            int.TryParse(Get("refreshIntervalMs"), out int v) ? Math.Max(300_000, v) : 300_000;
        public static void SaveRefreshIntervalMs(int ms) { Set("refreshIntervalMs", ms.ToString()); SaveAll(); }

        public static Point LoadWindowLocation()
        {
            int x = int.TryParse(Get("windowX"), out int px) ? px : 100;
            int y = int.TryParse(Get("windowY"), out int py) ? py : 100;
            return new Point(x, y);
        }

        public static Size LoadWindowSize()
        {
            int w = int.TryParse(Get("windowWidth"),  out int pw) ? pw : 1200;
            int h = int.TryParse(Get("windowHeight"), out int ph) ? ph : 120;
            return new Size(Math.Max(1100, w), Math.Max(120, h));
        }

        public static void SaveWindowBounds(Point location, Size size)
        {
            Set("windowX",      location.X.ToString());
            Set("windowY",      location.Y.ToString());
            Set("windowWidth",  size.Width.ToString());
            Set("windowHeight", size.Height.ToString());
            SaveAll();
        }
    }
}
