using System.ComponentModel.Design.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackPostExtUpdator;

public class GithubService : IDisposable
{
    private bool _disposed;
    private readonly HttpClient _client;

    //private static readonly JsonSerializerOptions options = new JsonSerializerOptions
    //{
    //  PropertyNamingPolicy = new SnakeCaseNamingPolicy(),
    //  PropertyNameCaseInsensitive = true,
    //  DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    //};

    internal GithubService(string userAgent, string token)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _client?.Dispose();
        }

        _disposed = true;
    }

    public async Task<Repository> GetRepository(string owner, string repoName)
    {
        var response = await _client.GetAsync($"https://api.github.com/repos/{owner}/{repoName}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var repository = JsonSerializer.Deserialize(content, GitHubJsonContext.Default.Repository);
        return repository ?? throw new FileNotFoundException("repository is not found");
    }

    // New method to get all contents of the repository
    public async Task<List<Content>> GetContents(
        string owner,
        string repo,
        string path = ""
    )
    {
        var response = await _client.GetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/contents/{path}"
        );
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        if (content.TrimStart().StartsWith('['))
        {
            // It's a list of contents
            var contents = JsonSerializer.Deserialize(
                content,
                GitHubJsonContext.Default.ListContent
            );
            return contents ?? throw new FileNotFoundException("Contents not found.");
        }
        else
        {
            // It's a single content object (file)
            var singleContent = JsonSerializer.Deserialize(
                content,
                GitHubJsonContext.Default.Content
            );
            return singleContent != null
                ? [singleContent]
                : throw new FileNotFoundException("Content not found.");
        }
    }
    public async Task<DownloadFileStream> GetLatestCommitFileStream(string owner, string repo, string fileName)
    {
        var contents = await GetContents(owner, repo, fileName);

        if (contents.Count < 1)
            throw new FileNotFoundException($"Github에서 {fileName}을 찾을 수 없습니다.");

        var download_url = contents[0].DownloadUrl;

        using var response = await _client.GetAsync(
            download_url,
            HttpCompletionOption.ResponseHeadersRead
        );
        response.EnsureSuccessStatusCode();

        return new DownloadFileStream(response);
    }

    // Equivalent to Octokit's UserClient.Get()
    public async Task<User> GetUser(string username)
    {
        var response = await _client.GetAsync($"https://api.github.com/users/{username}");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize(content, GitHubJsonContext.Default.User);

        return user ?? throw new FileNotFoundException("User not found.");
    }

    public async Task<RateLimit> GetRateLimits()
    {
        var response = await _client.GetAsync($"https://api.github.com/rate_limit");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var rateLimitResponse = JsonSerializer.Deserialize(
            content,
            GitHubJsonContext.Default.RateLimitResponse
        );
        var rateLimit = rateLimitResponse?.Resources?.Core;
        return rateLimit ?? throw new FileNotFoundException("Rate Limit not found.");
    }

    public async Task<List<Release>> GetReleases(string owner, string repo)
    {
        var response = await _client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var releases = JsonSerializer.Deserialize(content, GitHubJsonContext.Default.ListRelease);

        return releases ?? throw new FileNotFoundException("No releases found.");
    }

    public async Task<Release> GetLatestRelease(string owner, string repo)
    {
        var releases = await GetReleases(owner, repo);
        if (releases.Count < 1)
            throw new FileNotFoundException($"Github에서 Release를 찾을 수 없습니다.");
        return releases[0];
    }

    public async Task<DownloadFileStream> GetReleaseFileStream(Release release)
    {
        var url = release.Assets[0].BrowserDownloadUrl;

        var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead
        );
        response.EnsureSuccessStatusCode();

        return new DownloadFileStream(response);
    }
}

public class DownloadFileStream(HttpResponseMessage response) : IDisposable
{
    private readonly HttpResponseMessage _response = response;
    public long FileSize { get; } = response.Content.Headers.ContentLength ?? -1L;
    public Stream Stream { get; } = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

    public void Dispose()
    {
        try
        {
            Stream?.Dispose();
        }
        finally
        {
            _response?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    public override string ConvertName(string name)
    {
        return string.Concat(
            name.Select(
                (c, i) =>
                    i > 0 && char.IsUpper(c) ? "_" + c.ToString().ToLower() : c.ToString().ToLower()
            )
        );
    }
}

[JsonSerializable(typeof(List<Repository>))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(List<Content>))]
[JsonSerializable(typeof(RateLimitResponse))]
[JsonSerializable(typeof(List<Release>))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal partial class GitHubJsonContext : JsonSerializerContext { }

public class Repository
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("owner")]
    public User Owner { get; set; } = new User();

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("fork")]
    public bool Fork { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; set; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("has_issues")]
    public bool HasIssues { get; set; }

    [JsonPropertyName("has_projects")]
    public bool HasProjects { get; set; }

    [JsonPropertyName("has_downloads")]
    public bool HasDownloads { get; set; }

    [JsonPropertyName("has_wiki")]
    public bool HasWiki { get; set; }

    [JsonPropertyName("has_pages")]
    public bool HasPages { get; set; }

    [JsonPropertyName("has_discussions")]
    public bool HasDiscussions { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }

    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("open_issues_count")]
    public int OpenIssuesCount { get; set; }

    [JsonPropertyName("allow_forking")]
    public bool AllowForking { get; set; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = "public";

    [JsonPropertyName("forks")]
    public int Forks { get; set; }

    [JsonPropertyName("open_issues")]
    public int OpenIssues { get; set; }

    [JsonPropertyName("watchers")]
    public int Watchers { get; set; }

    [JsonPropertyName("default_branch")]
    public string DefaultBranch { get; set; } = "main";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("pushed_at")]
    public DateTimeOffset PushedAt { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("license")]
    public License? License { get; set; }

    [JsonPropertyName("topics")]
    public List<string> Topics { get; set; } = [];

    [JsonPropertyName("subscribers_count")]
    public int SubscribersCount { get; set; }

    [JsonPropertyName("watchers_count")]
    public int WatchersCount { get; set; }
}

public class Content
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha")]
    public string Sha { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("git_url")]
    public string GitUrl { get; set; } = string.Empty;

    [JsonPropertyName("download_url")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

public class User
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;
}

public class License
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("spdx_id")]
    public string? SpdxId { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("node_id")]
    public string? NodeId { get; set; }
}

public class RateLimit
{
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }

    [JsonPropertyName("reset")]
    public int Reset { get; set; }
}

public class RateLimitResponse
{
    [JsonPropertyName("resources")]
    public RateLimitResources? Resources { get; set; }
}

public class RateLimitResources
{
    [JsonPropertyName("core")]
    public RateLimit? Core { get; set; }

    [JsonPropertyName("search")]
    public RateLimit? Search { get; set; }

    [JsonPropertyName("graphql")]
    public RateLimit? Graphql { get; set; }

    [JsonPropertyName("integration_manifest")]
    public RateLimit? IntegrationManifest { get; set; }

    [JsonPropertyName("source_import")]
    public RateLimit? SourceImport { get; set; }
}

public class Release
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("assets_url")]
    public string AssetsUrl { get; set; } = string.Empty;

    [JsonPropertyName("upload_url")]
    public string UploadUrl { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("author")]
    public User Author { get; set; } = new User();

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("target_commitish")]
    public string TargetCommitish { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("immutable")]
    public bool Immutable { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<Asset> Assets { get; set; } = new List<Asset>();

    [JsonPropertyName("tarball_url")]
    public string? TarballUrl { get; set; }

    [JsonPropertyName("zipball_url")]
    public string? ZipballUrl { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public class Asset
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("uploader")]
    public User Uploader { get; set; } = new User();

    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("digest")]
    public string? Digest { get; set; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}