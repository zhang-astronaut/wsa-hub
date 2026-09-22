using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WsaHub.Core;

public sealed class HubConfig
{
    public string Repo { get; set; } = "MustardChef/WSABuilds";
    public bool PreferLts { get; set; } = true;
    public string AssetPattern { get; set; } = @"(?i)WSA_.*_x64_.*GApps.*NoAmazon.*\.7z$";
    public string[] FallbackPatterns { get; set; } =
    {
        @"(?i)WSA_.*_x64_.*GApps.*\.7z$",
        @"(?i)WSA_.*_x64_.*\.7z$"
    };
    public string WsaInstallDir { get; set; } = @"C:\WSA";
    public string DownloadDir { get; set; } = @"C:\WSA\_download";
    public string AdbPath { get; set; } = "";
    public bool CheckOnLogon { get; set; } = true;
    public bool NotifyOnUpToDate { get; set; } = false;
    public string GithubToken { get; set; } = "";
    public string LastReleaseTag { get; set; } = "";
    public string LastAssetName { get; set; } = "";
    public string LastCheckUtc { get; set; } = "";

    public static string DefaultPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WsaHub", "config.json");

    public static HubConfig Load(string path = null)
    {
        path = path ?? DefaultPath();
        var cfg = new HubConfig();
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<HubConfig>(File.ReadAllText(path));
                if (loaded != null) cfg = loaded;
            }
        }
        catch { }
        return cfg;
    }

    public void Save(string path = null)
    {
        path = path ?? DefaultPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }
}

public sealed class WsaStatus
{
    public bool Installed { get; set; }
    public string Version { get; set; } = "";
    public string PackageFullName { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public bool DevMode { get; set; }
    public bool Running { get; set; }
    public bool GApps { get; set; }
    public bool Magisk { get; set; }
    public string Source { get; set; } = "none";
    public string Detail { get; set; } = "";
}

public sealed class UpdateCheckResult
{
    public bool Ok { get; set; }
    public string Error { get; set; } = "";
    public bool HasUpdate { get; set; }
    public bool ShouldNotify { get; set; }
    public string Reason { get; set; } = "";
    public string ReleaseTag { get; set; } = "";
    public string ReleaseName { get; set; } = "";
    public string ReleaseUrl { get; set; } = "";
    public string AssetName { get; set; } = "";
    public string AssetUrl { get; set; } = "";
    public long AssetSize { get; set; }
    public string AssetVersion { get; set; } = "";
    public string InstalledVersion { get; set; } = "";
}

public static class WsaProbe
{
    public static WsaStatus Detect(HubConfig cfg)
    {
        var st = new WsaStatus { InstallDir = cfg.WsaInstallDir };
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -Command \"(Get-AppxPackage -Name MicrosoftCorporationII.WindowsSubsystemForAndroid | Select-Object -First 1 Version,PackageFullName,InstallLocation) | ConvertTo-Json -Compress\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var json = p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
            if (!string.IsNullOrWhiteSpace(json) && json.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Version", out var v))
                {
                    st.Installed = true;
                    st.Version = v.GetString() ?? "";
                    st.Source = "appx";
                    if (root.TryGetProperty("PackageFullName", out var pf)) st.PackageFullName = pf.GetString() ?? "";
                    if (root.TryGetProperty("InstallLocation", out var il) && il.GetString() is string dir && dir.Length > 0)
                        st.InstallDir = dir;
                }
            }
        }
        catch { }

        var manifest = Path.Combine(st.InstallDir ?? cfg.WsaInstallDir, "AppxManifest.xml");
        if (!st.Installed && File.Exists(manifest))
        {
            var text = File.ReadAllText(manifest);
            var m = Regex.Match(text, "Version=\"(\\d+\\.\\d+\\.\\d+\\.\\d+)\"");
            if (m.Success)
            {
                st.Installed = true;
                st.Version = m.Groups[1].Value;
                st.Source = "manifest";
            }
        }
        if (string.IsNullOrEmpty(st.InstallDir)) st.InstallDir = cfg.WsaInstallDir;

        st.Running = Process.GetProcessesByName("WsaClient").Length > 0
                    || Process.GetProcessesByName("vmmemWSA").Length > 0
                    || Process.GetProcessesByName("WsaService").Length > 0;

        // GApps / Magisk heuristics via adb if available
        var adb = AdbTool.ResolveAdb(cfg);
        if (st.Installed && adb != null)
        {
            try
            {
                AdbTool.EnsureConnected(cfg);
                var pkgs = AdbShell(cfg, "pm list packages");
                st.GApps = pkgs.Contains("com.android.vending") || pkgs.Contains("com.google.android.gms");
                st.Magisk = pkgs.Contains("com.topjohnwu.magisk");
            }
            catch { }
        }
        return st;
    }

    public static string AdbShell(HubConfig cfg, string args)
    {
        var adb = AdbTool.ResolveAdb(cfg)
            ?? throw new InvalidOperationException("adb not found");
        var psi = new ProcessStartInfo
        {
            FileName = adb,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var so = p.StandardOutput.ReadToEnd();
        var se = p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        return string.IsNullOrWhiteSpace(so) ? se : so;
    }
}

public static class AdbTool
{
    public static string ResolveAdb(HubConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.AdbPath) && File.Exists(cfg.AdbPath)) return cfg.AdbPath;
        var candidates = new[]
        {
            Path.Combine(cfg.WsaInstallDir, "platform-tools", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"),
            @"C:\platform-tools\adb.exe"
        };
        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "adb",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi)!;
            var line = p.StandardOutput.ReadLine();
            p.WaitForExit(3000);
            if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim())) return line.Trim();
        }
        catch { }
        return null;
    }

    public static void EnsureConnected(HubConfig cfg)
    {
        var adb = ResolveAdb(cfg) ?? throw new InvalidOperationException("adb not found");
        Run(adb, "connect 127.0.0.1:58526");
    }

    public static string Run(string fileName, string args, int timeoutMs = 20000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var so = p.StandardOutput.ReadToEnd();
        var se = p.StandardError.ReadToEnd();
        p.WaitForExit(timeoutMs);
        return string.IsNullOrWhiteSpace(so) ? se : so;
    }

    public static string InstallApk(HubConfig cfg, string apkPath)
    {
        EnsureConnected(cfg);
        var adb = ResolveAdb(cfg)!;
        return Run(adb, $"-s 127.0.0.1:58526 install -r \"{apkPath}\"", 120000);
    }
}

public static class GithubWsabuilds
{
    public static async Task<UpdateCheckResult> CheckAsync(HubConfig cfg, CancellationToken ct = default)
    {
        var result = new UpdateCheckResult();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("wsa-hub");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            if (!string.IsNullOrWhiteSpace(cfg.GithubToken))
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.GithubToken);

            var url = $"https://api.github.com/repos/{cfg.Repo}/releases?per_page=30";
            var body = await http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(body);
            JsonElement? best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var rel in doc.RootElement.EnumerateArray())
            {
                if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                var tag = rel.GetProperty("tag_name").GetString() ?? "";
                var name = rel.GetProperty("name").GetString() ?? "";
                var isLts = tag.IndexOf("lts", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("lts", StringComparison.OrdinalIgnoreCase) >= 0;
                if (cfg.PreferLts && !isLts) continue;
                var published = rel.GetProperty("published_at").GetDateTime();
                if (best == null || published > bestTime) { best = rel; bestTime = published; }
                if (!cfg.PreferLts) { best = rel; bestTime = published; break; }
            }
            if (best == null && cfg.PreferLts)
            {
                foreach (var rel in doc.RootElement.EnumerateArray())
                {
                    if (rel.TryGetProperty("draft", out var d) && d.GetBoolean()) continue;
                    best = rel; break;
                }
            }
            if (best == null)
            {
                result.Error = "No releases";
                return result;
            }
            var release = best.Value;
            result.Ok = true;
            result.ReleaseTag = release.GetProperty("tag_name").GetString() ?? "";
            result.ReleaseName = release.GetProperty("name").GetString() ?? "";
            result.ReleaseUrl = release.GetProperty("html_url").GetString() ?? "";

            var patterns = new List<string> { cfg.AssetPattern };
            if (cfg.FallbackPatterns != null) patterns.AddRange(cfg.FallbackPatterns);
            JsonElement? asset = null;
            foreach (var pat in patterns)
            {
                var matches = new List<JsonElement>();
                foreach (var a in release.GetProperty("assets").EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    if (Regex.IsMatch(n, pat) && Regex.IsMatch(n, "canary", RegexOptions.IgnoreCase) == false)
                        matches.Add(a);
                }
                if (matches.Count == 0)
                {
                    foreach (var a in release.GetProperty("assets").EnumerateArray())
                    {
                        var n = a.GetProperty("name").GetString() ?? "";
                        if (Regex.IsMatch(n, pat)) matches.Add(a);
                    }
                }
                if (matches.Count > 0)
                {
                    asset = matches.OrderByDescending(x => x.GetProperty("size").GetInt64()).First();
                    break;
                }
            }
            if (asset == null)
            {
                result.Ok = false;
                result.Error = "No matching asset";
                return result;
            }
            var a2 = asset.Value;
            result.AssetName = a2.GetProperty("name").GetString() ?? "";
            result.AssetUrl = a2.GetProperty("browser_download_url").GetString() ?? "";
            result.AssetSize = a2.GetProperty("size").GetInt64();
            var vm = Regex.Match(result.AssetName, @"WSA_(\d+\.\d+\.\d+\.\d+)");
            if (vm.Success) result.AssetVersion = vm.Groups[1].Value;

            var st = WsaProbe.Detect(cfg);
            result.InstalledVersion = st.Version;

            if (!st.Installed)
            {
                result.HasUpdate = true; result.Reason = "no_local_wsa";
                result.ShouldNotify = !(cfg.LastReleaseTag == result.ReleaseTag && cfg.LastAssetName == result.AssetName);
            }
            else if (!string.IsNullOrEmpty(result.AssetVersion) && result.AssetVersion == st.Version)
            {
                result.HasUpdate = false; result.Reason = "up_to_date"; result.ShouldNotify = false;
            }
            else if (!string.IsNullOrEmpty(result.AssetVersion))
            {
                result.HasUpdate = true; result.Reason = "version_mismatch";
                result.ShouldNotify = !(cfg.LastReleaseTag == result.ReleaseTag && cfg.LastAssetName == result.AssetName);
            }
            else
            {
                result.HasUpdate = true;
                if (cfg.LastReleaseTag == result.ReleaseTag && cfg.LastAssetName == result.AssetName)
                {
                    result.Reason = "already_notified"; result.ShouldNotify = false;
                }
                else { result.Reason = "first_seen_release"; result.ShouldNotify = true; }
            }
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = ex.Message;
        }
        return result;
    }

    public static async Task<string> DownloadAsync(HubConfig cfg, UpdateCheckResult r, IProgress<double> progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(cfg.DownloadDir);
        var dest = Path.Combine(cfg.DownloadDir, r.AssetName);
        var partial = dest + ".partial";
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("wsa-hub");
        using var resp = await http.GetAsync(r.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? r.AssetSize;
        await using var fs = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[1024 * 256];
        long read = 0;
        int n;
        while ((n = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (progress != null && total > 0) progress.Report(read / (double)total);
        }
        fs.Close();
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(partial, dest);
        return dest;
    }
}

public static class WsaInstaller
{
    public static void StopWsa()
    {
        foreach (var name in new[] { "WsaClient", "WsaService", "WsaSettings", "WsaProxy", "WSACrashUploader", "vmmemWSA" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(true); } catch { }
            }
        }
    }

    public static string BackupUserdata(HubConfig cfg)
    {
        var backup = Path.Combine(cfg.WsaInstallDir, "_backup");
        Directory.CreateDirectory(backup);
        var localCache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe", "LocalCache");
        if (!Directory.Exists(localCache)) return "";
        string last = "";
        foreach (var f in Directory.GetFiles(localCache, "userdata*.vhdx"))
        {
            var dest = Path.Combine(backup, $"userdata_{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileName(f)}");
            File.Copy(f, dest, true);
            last = dest;
        }
        return last;
    }

    /// <summary>Merge archive into install dir. Requires 7z. Does not register.</summary>
    public static void ExtractAndMerge(string archive, HubConfig cfg, Action<string> log = null)
    {
        var seven = Find7z();
        if (seven == null) throw new InvalidOperationException("7z.exe not found (install 7-Zip/NanaZip)");
        var stage = Path.Combine(cfg.WsaInstallDir, "_apply_stage");
        if (Directory.Exists(stage)) Directory.Delete(stage, true);
        Directory.CreateDirectory(stage);
        log?.Invoke("Extracting " + archive);
        var code = RunEx(seven, $"x \"{archive}\" -o\"{stage}\" -y", log);
        if (code != 0) throw new InvalidOperationException("7z extract failed: " + code);
        var pkg = Directory.GetDirectories(stage).FirstOrDefault() ?? throw new InvalidOperationException("empty archive");
        var exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_download", "_backup", "_gapps_stage", "_apply_stage", "platform-tools"
        };
        foreach (var e in Directory.GetFileSystemEntries(pkg))
        {
            var name = Path.GetFileName(e);
            if (exclude.Contains(name)) continue;
            var dest = Path.Combine(cfg.WsaInstallDir, name);
            if (Directory.Exists(e)) CopyDir(e, dest);
            else File.Copy(e, dest, true);
        }
        ApplyAppDlls(cfg.WsaInstallDir, log);
    }

    public static void ApplyAppDlls(string installDir, Action<string> log = null)
    {
        var vclibs = Path.Combine(installDir, "_download", "vclibs_extract", "Microsoft.VCLibs.140.00_x64.appx");
        if (!Directory.Exists(vclibs)) return;
        var hosts = new[] { "WsaClient", "WsaService", "WsaSettingsBroker", "WsaProxy", "WSACrashUploader", "amd64" };
        foreach (var dll in Directory.GetFiles(vclibs, "*_APP.dll"))
        {
            File.Copy(dll, Path.Combine(installDir, Path.GetFileName(dll)), true);
        }
        foreach (var h in hosts)
        {
            var hp = Path.Combine(installDir, h);
            if (!Directory.Exists(hp)) continue;
            foreach (var dll in Directory.GetFiles(installDir, "*.dll"))
            {
                try { File.Copy(dll, Path.Combine(hp, Path.GetFileName(dll)), true); } catch { }
            }
            foreach (var dll in Directory.GetFiles(vclibs, "*_APP.dll"))
            {
                try { File.Copy(dll, Path.Combine(hp, Path.GetFileName(dll)), true); } catch { }
            }
        }
        log?.Invoke("APP dll helpers applied");
    }

    /// <summary>Elevated register + optional uninstall of previous package.</summary>
    public static int RegisterElevated(HubConfig cfg, Action<string> log = null)
    {
        var installDir = cfg.WsaInstallDir;
        var script = Path.Combine(Path.GetTempPath(), "WsaHub_register.ps1");
        var ps = $@"
$ErrorActionPreference='Stop'
Start-Transcript -Path '{Path.Combine(installDir, "_install_apply_log.txt")}' -Append
Get-Process WsaClient,WsaService,WsaSettings -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$existing = Get-AppxPackage -Name MicrosoftCorporationII.WindowsSubsystemForAndroid -ErrorAction SilentlyContinue
if ($existing) {{
  try {{ Remove-AppxPackage -Package $existing.PackageFullName -PreserveApplicationData }} catch {{
    try {{ Remove-AppxPackage -Package $existing.PackageFullName }} catch {{ }}
  }}
}}
Set-Location '{installDir}'
[xml]$x = Get-Content .\AppxManifest.xml
$arch = $x.Package.Identity.ProcessorArchitecture
foreach ($d in $x.Package.Dependencies.PackageDependency) {{
  $inst = Get-AppxPackage -Name $d.Name -ErrorAction SilentlyContinue | Where-Object {{ $_.Architecture -eq $arch }} | Sort-Object Version | Select-Object -Last 1
  if (-not $inst -or ([version]$inst.Version -lt [version]$d.MinVersion)) {{
    $appx = Join-Path '{installDir}' ('' + $d.Name + '_' + $arch + '.appx')
    if (Test-Path $appx) {{ Add-AppxPackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion -Path $appx }}
  }}
}}
Add-AppxPackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion -Register .\AppxManifest.xml
Write-Output ('REGISTER_OK=' + $?)
Stop-Transcript
";
        File.WriteAllText(script, ps, Encoding.UTF8);
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        try
        {
            using var p = Process.Start(psi);
            p?.WaitForExit(180000);
            return p?.ExitCode ?? -1;
        }
        catch (Exception ex)
        {
            log?.Invoke("Elevation failed: " + ex.Message);
            return -1;
        }
    }

    static string Find7z()
    {
        foreach (var c in new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "7z.exe")
        })
            if (File.Exists(c)) return c;
        return null;
    }

    static int RunEx(string fileName, string args, Action<string> log)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        while (!p.StandardOutput.EndOfStream)
        {
            var line = p.StandardOutput.ReadLine();
            if (line != null) log?.Invoke(line);
        }
        p.WaitForExit(300000);
        return p.ExitCode;
    }

    static void CopyDir(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(src))
            CopyDir(d, Path.Combine(dest, Path.GetFileName(d)));
    }
}

public static class LogonTask
{
    public const string TaskName = "WsaHub-CheckOnLogon";

    public static bool IsRegistered()
    {
        var o = AdbTool.Run("schtasks.exe", $"/Query /TN {TaskName}", 8000);
        return o.IndexOf(TaskName, StringComparison.OrdinalIgnoreCase) >= 0 && o.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) < 0;
    }

    public static void Install(string checkScriptPath)
    {
        AdbTool.Run("schtasks.exe", $"/Create /F /TN {TaskName} /TR \"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \\\"{checkScriptPath}\\\" -Quiet\" /SC ONLOGON /RL LIMITED", 15000);
    }

    public static void Uninstall()
    {
        AdbTool.Run("schtasks.exe", $"/Delete /F /TN {TaskName}", 10000);
    }
}
