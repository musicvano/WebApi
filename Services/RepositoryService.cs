using System.Net;
using System.Text.Json;
using WebApi.Models;

namespace WebApi.Services
{
    public class RepositoryException(string message) : Exception(message);

    public class RepositoryService(HttpClient httpClient)
    {
        private static readonly string[] SupportedHosts =
        [
            "github.com",
            "gitlab.com",
            "bitbucket.org",
            "codeberg.org"
        ];

        private readonly HttpClient httpClient = httpClient;

        public async Task<PackageMetadata> GetMetadataAsync(
            string repositoryUrl, CancellationToken cancellationToken)
        {
            var (host, owner, repo) = ParseRepositoryUrl(repositoryUrl);
            var (defaultBranch, hostLicense) =
                await GetRepositoryInfoAsync(host, owner, repo, cancellationToken);
            var manifest = await GetManifestAsync(host, owner, repo, defaultBranch, cancellationToken);
            var package = ParsePackageSection(manifest);
            if (!package.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                throw new RepositoryException(
                    "Rux.toml does not define a package name in the [Package] section.");
            }
            var license = hostLicense ?? package.GetValueOrDefault("License");
            if (string.IsNullOrWhiteSpace(license))
            {
                throw new RepositoryException(
                    "Unable to determine the repository license. " +
                    "Add a license file to the repository or a License field to Rux.toml.");
            }
            package.TryGetValue("Description", out var description);
            return new PackageMetadata(
                name,
                description ?? "",
                $"https://{host}/{owner}/{repo}",
                license);
        }

        private static (string Host, string Owner, string Repo) ParseRepositoryUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !SupportedHosts.Contains(uri.Host.ToLowerInvariant()))
            {
                throw new RepositoryException(
                    "Repository must be a valid HTTPS URL from GitHub, GitLab, Bitbucket, or Codeberg.");
            }
            var host = uri.Host.ToLowerInvariant();
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length != 2 || segments.Any(string.IsNullOrEmpty))
            {
                throw new RepositoryException(
                    $"Repository URL must point to a repository: https://{host}/owner/repo.");
            }
            var repo = segments[1].EndsWith(".git") ? segments[1][..^4] : segments[1];
            return (host, segments[0], repo);
        }

        private async Task<(string DefaultBranch, string? License)> GetRepositoryInfoAsync(
            string host, string owner, string repo, CancellationToken cancellationToken)
        {
            var infoUrl = host switch
            {
                "github.com" => $"https://api.github.com/repos/{owner}/{repo}",
                "gitlab.com" => "https://gitlab.com/api/v4/projects/" +
                    $"{Uri.EscapeDataString($"{owner}/{repo}")}?license=true",
                "bitbucket.org" => $"https://api.bitbucket.org/2.0/repositories/{owner}/{repo}",
                _ => $"https://codeberg.org/api/v1/repos/{owner}/{repo}"
            };
            using var response = await httpClient.GetAsync(infoUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new RepositoryException(
                    "Repository not found. Make sure it exists and is public.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new RepositoryException(
                    $"Repository host API request failed with status {(int)response.StatusCode}.");
            }
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            var root = json.RootElement;
            return host switch
            {
                "github.com" => (GetString(root, "default_branch") ?? "main", ReadGitHubLicense(root)),
                "gitlab.com" => (GetString(root, "default_branch") ?? "main", ReadGitLabLicense(root)),
                "bitbucket.org" => (ReadBitbucketBranch(root), null),
                _ => (GetString(root, "default_branch") ?? "main", ReadCodebergLicense(root))
            };
        }

        private async Task<string> GetManifestAsync(
            string host, string owner, string repo, string branch,
            CancellationToken cancellationToken)
        {
            var manifestUrl = host switch
            {
                "github.com" => $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/Rux.toml",
                "gitlab.com" => $"https://gitlab.com/{owner}/{repo}/-/raw/{branch}/Rux.toml",
                "bitbucket.org" => $"https://bitbucket.org/{owner}/{repo}/raw/{branch}/Rux.toml",
                _ => $"https://codeberg.org/{owner}/{repo}/raw/branch/{branch}/Rux.toml"
            };
            using var response = await httpClient.GetAsync(manifestUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new RepositoryException("Rux.toml not found in the repository root.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new RepositoryException(
                    $"Failed to download Rux.toml with status {(int)response.StatusCode}.");
            }
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }

        private static string? GetString(JsonElement element, string property)
            => element.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static string? ReadGitHubLicense(JsonElement root)
        {
            if (root.TryGetProperty("license", out var license)
                && license.ValueKind == JsonValueKind.Object)
            {
                var spdxId = GetString(license, "spdx_id");
                return spdxId == "NOASSERTION" ? null : spdxId;
            }
            return null;
        }

        private static string? ReadGitLabLicense(JsonElement root)
        {
            if (root.TryGetProperty("license", out var license)
                && license.ValueKind == JsonValueKind.Object)
            {
                return GetString(license, "nickname") ?? GetString(license, "name");
            }
            return null;
        }

        private static string ReadBitbucketBranch(JsonElement root)
        {
            if (root.TryGetProperty("mainbranch", out var branch)
                && branch.ValueKind == JsonValueKind.Object)
            {
                return GetString(branch, "name") ?? "main";
            }
            return "main";
        }

        private static string? ReadCodebergLicense(JsonElement root)
        {
            if (root.TryGetProperty("licenses", out var licenses)
                && licenses.ValueKind == JsonValueKind.Array
                && licenses.GetArrayLength() > 0
                && licenses[0].ValueKind == JsonValueKind.String)
            {
                return licenses[0].GetString();
            }
            return null;
        }

        // Minimal TOML reader for string keys of the [Package] section.
        private static Dictionary<string, string> ParsePackageSection(string toml)
        {
            var values = new Dictionary<string, string>();
            var inPackage = false;
            foreach (var rawLine in toml.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }
                if (line.StartsWith('['))
                {
                    inPackage = line.StartsWith("[Package]");
                    continue;
                }
                if (!inPackage)
                {
                    continue;
                }
                var separator = line.IndexOf('=');
                if (separator < 0)
                {
                    continue;
                }
                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();
                if (value.StartsWith('"'))
                {
                    var end = value.IndexOf('"', 1);
                    if (end < 0)
                    {
                        continue;
                    }
                    value = value[1..end];
                }
                values[key] = value;
            }
            return values;
        }
    }
}
