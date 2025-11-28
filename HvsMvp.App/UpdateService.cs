using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HvsMvp.App
{
    /// <summary>
    /// Service for checking GitHub releases for updates.
    /// </summary>
    public class UpdateService
    {
        private const string GitHubOwner = "trilhagoldcapital-gif";
        private const string GitHubRepo = "HvsMvpSuite";
        private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
        private const string ReleasesPageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";

        /// <summary>
        /// Get the current application version.
        /// </summary>
        public static string GetCurrentVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                if (version != null)
                {
                    return $"{version.Major}.{version.Minor}.{version.Build}";
                }
            }
            catch
            {
                // Fallback
            }

            return "1.0.0";
        }

        /// <summary>
        /// Check GitHub for the latest release version.
        /// Returns (hasUpdate, latestVersion, errorMessage).
        /// </summary>
        public async Task<(bool HasUpdate, string? LatestVersion, string? ErrorMessage)> CheckForUpdatesAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "MicroLab-HVS-MVP");
                client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                client.Timeout = TimeSpan.FromSeconds(15);

                var response = await client.GetAsync(ReleasesApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // No releases yet
                        return (false, null, null);
                    }
                    return (false, null, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("tag_name", out var tagElement))
                {
                    var latestTag = tagElement.GetString();
                    if (!string.IsNullOrWhiteSpace(latestTag))
                    {
                        var latestVersion = NormalizeVersion(latestTag);
                        var currentVersion = GetCurrentVersion();

                        var hasUpdate = CompareVersions(latestVersion, currentVersion) > 0;
                        return (hasUpdate, latestVersion, null);
                    }
                }

                return (false, null, "Unable to parse release information");
            }
            catch (TaskCanceledException)
            {
                return (false, null, "Timeout ao conectar com GitHub");
            }
            catch (HttpRequestException ex)
            {
                return (false, null, $"Erro de conexão: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        /// <summary>
        /// Open the GitHub releases page in the default browser.
        /// </summary>
        public void OpenReleasesPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = ReleasesPageUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Silent fail
            }
        }

        /// <summary>
        /// Normalize version string (remove 'v' prefix if present).
        /// </summary>
        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "0.0.0";

            version = version.Trim();
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                version = version.Substring(1);

            return version;
        }

        /// <summary>
        /// Compare two version strings.
        /// Returns: positive if v1 > v2, negative if v1 < v2, 0 if equal.
        /// </summary>
        private static int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.');
            var parts2 = v2.Split('.');

            int maxLen = Math.Max(parts1.Length, parts2.Length);

            for (int i = 0; i < maxLen; i++)
            {
                int p1 = 0, p2 = 0;

                if (i < parts1.Length)
                    int.TryParse(parts1[i], out p1);

                if (i < parts2.Length)
                    int.TryParse(parts2[i], out p2);

                if (p1 > p2) return 1;
                if (p1 < p2) return -1;
            }

            return 0;
        }
    }
}
