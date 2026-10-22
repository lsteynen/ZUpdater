using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Updater
{
    // code and data to handle a github release source, getting the latest release version of a github repo.

    public class GithubReleaseSource
    {
        public long Id;
        public string Url;
        public string Html_url;
        public string Name;
        public string Tag_name;
        public string Published_at;
        public string Body;
        public string VersionStr;
        public Version Version;
        public string asset_name;
        public string asset_browser_download_url;
        public string asset_file_name;
        public long asset_download_count;

        private readonly string userAgent = "ZUpdater/x";

        /// <summary>
        /// Log lessage callback
        /// </summary>
        public event Action<string> LogMessage;

        private void Log(string message)
        {
            LogMessage?.Invoke(message);
        }

        /// <summary>
        /// Get the latest release data from a github repo using the github API.
        /// Throw exception on error.
        /// </summary>
        /// <param name="url">URL github release latest</param>
        public void GetData(string url)
        {
            Log($"  Checking url '{url}'");

            // get the github release json text
            string json = GetLatestReleaseJsonAsync(url).GetAwaiter().GetResult();

            ParseJson(json);
        }

        private async Task<string> GetLatestReleaseJsonAsync(string url)
        {
            HttpClient Client = CreateHttpClient();

            using (HttpResponseMessage response = await Client.GetAsync(url).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        private HttpClient CreateHttpClient()
        {
            // GitHub requires TLS 1.2+
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var client = new HttpClient();
            // Required by the GitHub API, otherwise you get 403
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            return client;
        }

        /// <summary>
        /// Parse the JSON string returned by the GitHub API for a release and extract the relevant information.
        /// Throw exception on error.
        /// /// </summary>
        private void ParseJson(string json)
        {
            Log($"  Parse json");

            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                JsonElement root = doc.RootElement;
                JsonElement json_element;

                this.Id = root.GetProperty("id").GetInt64();
                if (root.TryGetProperty("url", out json_element))
                {
                    this.Url = json_element.GetString();
                }
                Log($"  url='{this.Url}'");

                if (root.TryGetProperty("html_url", out json_element))
                {
                    this.Html_url = json_element.GetString();
                }
                Log($"  html_url='{this.Html_url}'");

                this.Name = root.GetProperty("name").GetString();
                Log($"  release_name='{this.Name}'");

                if (root.TryGetProperty("tag_name", out json_element))
                {
                    this.Tag_name = json_element.GetString();
                }
                
                if (root.TryGetProperty("published_at", out json_element))
                {
                    this.Published_at = json_element.GetString();
                }
                Log($"  published_at={this.Published_at}");

                if (root.TryGetProperty("body", out json_element))
                {
                    this.Body = json_element.GetString();
                }

                // version string: from release name
                this.VersionStr = this.Name;
                
                // convert version string to Version object
                // may throw exception
                this.Version = GetVersionFromNameWithVersionString(this.VersionStr);
                Log($"  version={this.Version}");

                // iterate the assets, find the installer asset and get its data
                JsonElement assets = root.GetProperty("assets");
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string asset_name = asset.GetProperty("name").GetString();
                    string asset_browser_download_url = asset.GetProperty("browser_download_url").GetString();
                    long asset_download_count = asset.GetProperty("download_count").GetInt64();

                    Log($"  asset_name='{asset_name}', asset_download_url='{asset_browser_download_url}'");

                    // first asset name that ends with ".msi" is the installer
                    if (asset_name.EndsWith(".msi")) 
                    {
                        this.asset_name = asset_name;
                        this.asset_browser_download_url = asset_browser_download_url;
                        this.asset_file_name = Path.GetFileName(asset_browser_download_url);
                        this.asset_download_count = asset_download_count;

                        return;
                    }
                }

                throw new Exception("No *.msi asset found in the GitHub release.");
            }
        }

        /// <summary>
        /// VersionStr is a string with text and the version number. Get the version number as a Version object.
        /// Version string like "v1.12.8" or "version 1.2" or "aaaa 5.6.15.15888_zzz". 
        /// Take version number from the first digit, take all digits and dots, garbage may follow.
        /// </summary>
        private Version GetVersionFromNameWithVersionString(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new FormatException($"No version number found in \"{text}\".");
            }

            // find the first digit
            int start = 0;
            while (start < text.Length && !char.IsDigit(text[start]))
            {
                start++;
            }

            if (start == text.Length)
            {
                throw new FormatException($"No version number found in \"{text}\".");
            }

            // take digits, and dots that are followed by a digit
            int end = start;
            while (end < text.Length)
            {
                char c = text[end];

                if (char.IsDigit(c))
                {
                    end++;
                }
                else if ((c == '.') && (end + 1 < text.Length) && (char.IsDigit(text[end + 1])))
                {
                    end++;
                }
                else
                {
                    break;
                }
            }

            string versionText = text.Substring(start, end - start);

            // convert to Version object
            Version version;
            try
            {
                version = Version.Parse(versionText);
            }
            catch (Exception)
            {
                throw new FormatException($"\"{versionText}\" is not a valid version (a part may be too large).");
            }

            return version;
        }

        /// <summary>
        /// Download the installer from the GitHub release asset download URL.
        /// </summary>
        /// <param name="url">Download file url</param>
        /// <param name="localSavePath">Local destination path</param>
        public void Download(string url, string localSavePath)
        { 
            Log($"GitHub release download URL '{url}'");

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using (var client = new WebClient())
            {
                client.Headers.Add("User-Agent", userAgent);
                client.DownloadFile(url, localSavePath);
            }
        }
    }
}
