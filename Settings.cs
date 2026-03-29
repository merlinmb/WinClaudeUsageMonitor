// This class will handle loading/saving the API key securely (simple file for now)
using System;
using System.IO;

namespace ClaudeUsageBar
{
    public static class Settings
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageBar.key");

        public static string LoadApiKey()
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading API key from {FilePath}");
            if (File.Exists(FilePath))
            {
                var key = File.ReadAllText(FilePath).Trim();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] API key loaded (length: {key.Length})");
                return key;
            }
            System.Diagnostics.Debug.WriteLine("[DEBUG] API key file does not exist.");
            return string.Empty;
        }

        public static void SaveApiKey(string key)
        {
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Writing API key to {FilePath}");
            File.WriteAllText(FilePath, key);
            System.Diagnostics.Debug.WriteLine("[DEBUG] API key written to file.");
        }
    }
}
