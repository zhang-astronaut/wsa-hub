using System.Text.Json;
using System.Text.RegularExpressions;

namespace WsaHub.Core;

public sealed class ReleaseAssetInfo
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public long Size { get; set; }
    public string WsaVersion { get; set; } = "";
    public bool GApps { get; set; }
    public bool Amazon { get; set; }
    public bool Magisk { get; set; }
    public bool KernelSu { get; set; }
    public string RootLabel => Magisk ? "Magisk" : KernelSu ? "KernelSU" : "无Root";
    public string Summary =>
        (GApps ? "GApps" : "NoGApps") + " · " + (Amazon ? "Amazon" : "NoAmazon") + " · " + RootLabel;

    public override string ToString() => WsaVersion + "  |  " + Summary + "  |  " + Name;
}

public sealed class ReleaseInfo
{
    public string Tag { get; set; } = "";
    public string Name { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public bool IsLts { get; set; }
    public List<ReleaseAssetInfo> Assets { get; set; } = new();
}

public static class ReleaseCatalog
{
    public static ReleaseAssetInfo ParseAsset(string name, string url, long size)
    {
        var n = name ?? "";
        var m = Regex.Match(n, @"WSA_(\d+\.\d+\.\d+\.\d+)");
        var noAmz = Regex.IsMatch(n, "NoAmazon|RemovedAmazon", RegexOptions.IgnoreCase);
        return new ReleaseAssetInfo
        {
            Name = n,
            Url = url,
            Size = size,
            WsaVersion = m.Success ? m.Groups[1].Value : "",
            GApps = Regex.IsMatch(n, "GApps|MindTheGapps", RegexOptions.IgnoreCase),
            Amazon = !noAmz,
            Magisk = Regex.IsMatch(n, "magisk", RegexOptions.IgnoreCase),
            KernelSu = Regex.IsMatch(n, "kernelsu|kernel-su", RegexOptions.IgnoreCase)
        };
    }

    public static async Task<List<ReleaseInfo>> ListReleasesAsync(HubConfig cfg, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WsaHub");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        if (!string.IsNullOrWhiteSpace(cfg.GithubToken))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cfg.GithubToken);

        var url = $"https://api.github.com/repos/{cfg.Repo}/releases?per_page=30";
        var body = await http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(body);
        var list = new List<ReleaseInfo>();
        foreach (var rel in doc.RootElement.EnumerateArray())
        {
            if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
            var tag = rel.GetProperty("tag_name").GetString() ?? "";
            var name = rel.GetProperty("name").GetString() ?? "";
            var info = new ReleaseInfo
            {
                Tag = tag,
                Name = name,
                PublishedAt = rel.GetProperty("published_at").GetString() ?? "",
                IsLts = tag.Contains("LTS", StringComparison.OrdinalIgnoreCase) || name.Contains("LTS", StringComparison.OrdinalIgnoreCase)
            };
            if (rel.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var an = a.GetProperty("name").GetString() ?? "";
                    if (!an.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!an.StartsWith("WSA_", StringComparison.OrdinalIgnoreCase)) continue;
                    var item = ParseAsset(an,
                        a.GetProperty("browser_download_url").GetString() ?? "",
                        a.GetProperty("size").GetInt64());
                    info.Assets.Add(item);
                }
            }
            list.Add(info);
        }
        return list;
    }
}
