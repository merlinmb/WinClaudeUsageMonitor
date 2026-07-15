using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http;
using System.Net.Http.Headers;
using ClaudeUsageBar.Models;

namespace ClaudeUsageBar.Services;

public class CredentialService
{
    private static readonly HttpClient _httpClient = new();
    private const string TokenRefreshUrl = "https://console.anthropic.com/v1/oauth/token";

    private static string? _cachedCredentialsPath;
    private static DateTime _cacheTimestamp = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    private static string GetWindowsNativePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude",
            ".credentials.json"
        );
    }

    private static async Task<string?> FindCredentialsPathAsync()
    {
        // Fast path: return cached path if still valid
        if (_cachedCredentialsPath != null
            && File.Exists(_cachedCredentialsPath)
            && DateTime.UtcNow - _cacheTimestamp < CacheLifetime)
        {
            return _cachedCredentialsPath;
        }

        var candidates = new List<(string Path, DateTime LastModified)>();

        // 1. Check Windows native path first (instant local FS check)
        var windowsPath = GetWindowsNativePath();
        if (File.Exists(windowsPath))
        {
            try
            {
                candidates.Add((windowsPath, File.GetLastWriteTimeUtc(windowsPath)));
                System.Diagnostics.Debug.WriteLine($"Found native Windows credentials at: {windowsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading native credentials timestamp: {ex.Message}");
                candidates.Add((windowsPath, DateTime.MinValue));
            }
        }

        // 2. Check WSL paths
        await Task.Run(() =>
        {
            string[] wslDistros = ["Debian", "Ubuntu", "Ubuntu-22.04", "Ubuntu-20.04", "kali-linux"];
            string[] wslRoots = [@"\\wsl.localhost", @"\\wsl$"];

            foreach (var wslRoot in wslRoots)
            foreach (var distro in wslDistros)
            {
                try
                {
                    var wslHomePath = $@"{wslRoot}\{distro}\home";
                    if (!Directory.Exists(wslHomePath)) continue;

                    foreach (var userDir in Directory.GetDirectories(wslHomePath))
                    {
                        var credPath = Path.Combine(userDir, ".claude", ".credentials.json");
                        if (File.Exists(credPath))
                        {
                            try { candidates.Add((credPath, File.GetLastWriteTimeUtc(credPath))); }
                            catch { candidates.Add((credPath, DateTime.MinValue)); }
                            System.Diagnostics.Debug.WriteLine($"Found WSL credentials at: {credPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WSL path error ({wslRoot}\\{distro}): {ex.Message}");
                }
            }
        }).WaitAsync(TimeSpan.FromSeconds(10));

        if (candidates.Count == 0)
        {
            _cachedCredentialsPath = null;
            return null;
        }

        var best = candidates.OrderByDescending(c => c.LastModified).First();
        _cachedCredentialsPath = best.Path;
        _cacheTimestamp = DateTime.UtcNow;

        System.Diagnostics.Debug.WriteLine($"Selected credentials: {best.Path} (modified {best.LastModified:u})");
        return best.Path;
    }

    public static async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var credentialsPath = await FindCredentialsPathAsync();
            if (credentialsPath == null) return null;

            var json = File.ReadAllText(credentialsPath);
            var credentials = JsonSerializer.Deserialize<CredentialsFile>(json);

            if (credentials?.ClaudeAiOauth == null) return null;

            var expiresAt = credentials.ClaudeAiOauth.ExpiresAt ?? 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Refresh if expiring within 5 minutes
            if (now >= expiresAt - (5 * 60 * 1000))
            {
                System.Diagnostics.Debug.WriteLine("Token expired or expiring soon, refreshing...");
                var refreshed = await RefreshTokenAsync(credentials.ClaudeAiOauth.RefreshToken);
                if (refreshed != null)
                {
                    credentials.ClaudeAiOauth = refreshed;
                    await SaveCredentialsAsync(credentials, credentialsPath);
                    return refreshed.AccessToken;
                }
            }

            return credentials.ClaudeAiOauth.AccessToken;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting access token: {ex.Message}");
            return null;
        }
    }

    private static async Task<ClaudeOAuth?> RefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrEmpty(refreshToken)) return null;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenRefreshUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            });

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Token refresh failed: {response.StatusCode} - {error}");
                return null;
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenRefreshResponse>(jsonResponse);
            if (tokenResponse == null) return null;

            return new ClaudeOAuth
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken ?? refreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + (tokenResponse.ExpiresIn * 1000),
                Scopes = tokenResponse.Scope?.Split(' ')
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Exception refreshing token: {ex.Message}");
            return null;
        }
    }

    private static async Task SaveCredentialsAsync(CredentialsFile credentials, string credentialsPath)
    {
        try
        {
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = false });
            await File.WriteAllTextAsync(credentialsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
        }
    }

    private static IEnumerable<string> EnumerateWslCredentialPaths()
    {
        string[] wslDistros = ["Debian", "Ubuntu", "Ubuntu-22.04", "Ubuntu-20.04", "kali-linux"];
        string[] wslRoots = [@"\\wsl.localhost", @"\\wsl$"];
        foreach (var root in wslRoots)
        foreach (var distro in wslDistros)
        {
            string wslHome = $@"{root}\{distro}\home";
            if (!Directory.Exists(wslHome)) continue;
            string[] userDirs;
            try { userDirs = Directory.GetDirectories(wslHome); }
            catch { continue; }
            foreach (var userDir in userDirs)
            {
                var credPath = Path.Combine(userDir, ".claude", ".credentials.json");
                if (File.Exists(credPath)) yield return credPath;
            }
        }
    }

    public static bool CredentialsExist()
    {
        if (_cachedCredentialsPath != null && File.Exists(_cachedCredentialsPath)) return true;
        if (File.Exists(GetWindowsNativePath())) return true;
        return EnumerateWslCredentialPaths().Any();
    }

    public static string? GetCredentialsPath()
    {
        if (_cachedCredentialsPath != null && File.Exists(_cachedCredentialsPath))
            return _cachedCredentialsPath;
        var windowsPath = GetWindowsNativePath();
        if (File.Exists(windowsPath)) return windowsPath;
        return EnumerateWslCredentialPaths().FirstOrDefault();
    }
}

public class TokenRefreshResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
