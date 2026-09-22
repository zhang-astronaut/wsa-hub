using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;

namespace WsaHub
{
    public class MainForm : Form
    {
        readonly TextBox _log = new TextBox();
        readonly Label _status = new Label();
        readonly TabControl _tabs = new TabControl();
        string _installDir = @"C:\WSA";
        string _downloadDir = @"C:\WSA\_download";
        string _assetPattern = @"(?i)WSA_.*_x64_.*GApps.*NoAmazon.*\.7z$";

        public MainForm()
        {
            Text = "WsaHub — WSA 管理中心";
            Width = 1100; Height = 740;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(32, 32, 36);
            ForeColor = Color.FromArgb(230, 230, 235);
            Font = new Font("Segoe UI", 9.5f);

            _status.Dock = DockStyle.Top;
            _status.Height = 36;
            _status.TextAlign = ContentAlignment.MiddleLeft;
            _status.BackColor = Color.FromArgb(40, 40, 46);
            _status.Padding = new Padding(12, 0, 0, 0);
            _status.Text = "正在检测 WSA…";

            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.BackColor = Color.FromArgb(26, 26, 30);
            _log.ForeColor = Color.FromArgb(180, 255, 180);
            _log.Font = new Font("Consolas", 9f);
            _log.Dock = DockStyle.Fill;

            _tabs.Dock = DockStyle.Fill;
            _tabs.Padding = new Point(12, 4);
            _tabs.TabPages.Add(BuildHome());
            _tabs.TabPages.Add(BuildUpdate());
            _tabs.TabPages.Add(BuildAdb());
            _tabs.TabPages.Add(BuildApps());
            _tabs.TabPages.Add(BuildSettings());

            var foot = new Panel { Dock = DockStyle.Bottom, Height = 140 };
            foot.Controls.Add(_log);

            Controls.Add(_tabs);
            Controls.Add(foot);
            Controls.Add(_status);

            Load += (s, e) => ThreadPool.QueueUserWorkItem(_ => BeginInvoke((Action)(() =>
            {
                LoadConfig();
                RefreshHome();
                if (!string.IsNullOrEmpty(DetectVersion()))
                    Append("可到「更新与安装」检查 WSABuilds 更新；配置已从 %APPDATA%\\WsaHub\\config.json 加载（若有）");
            })));
        }

        void Append(string s)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => Append(s))); return; }
            _log.AppendText(s + Environment.NewLine);
        }

        Button Btn(string text, Action onClick)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                BackColor = Color.FromArgb(47, 111, 237),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 6, 8, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => { try { onClick(); } catch (Exception ex) { Append(ex.Message); } };
            return b;
        }

        static Label Lbl(string t) => new Label { Text = t, AutoSize = true, ForeColor = Color.FromArgb(210, 210, 216), Margin = new Padding(0, 2, 0, 2) };

        string AdbPath()
        {
            var candidates = new[]
            {
                Path.Combine(_installDir, "platform-tools", "adb.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe")
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return "adb";
        }

        string DetectVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command \"(Get-AppxPackage -Name MicrosoftCorporationII.WindowsSubsystemForAndroid | Select-Object -First 1 -ExpandProperty Version)\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    var v = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(6000);
                    return v;
                }
            }
            catch { return ""; }
        }

        void RefreshHome()
        {
            var ver = DetectVersion();
            var running = Process.GetProcessesByName("WsaClient").Length > 0 || Process.GetProcessesByName("vmmemWSA").Length > 0;
            _status.Text = string.IsNullOrEmpty(ver)
                ? "未检测到 WSA — 请到「更新与安装」执行全新安装"
                : ("WSA " + ver + (running ? " · 运行中" : " · 未运行") + "  ·  " + _installDir);
            Append("检测: version=" + (string.IsNullOrEmpty(ver) ? "(none)" : ver) + " running=" + running);
        }

        TabPage BuildHome()
        {
            var t = new TabPage("首页") { BackColor = Color.FromArgb(32, 32, 36) };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = true };
            p.Controls.Add(Lbl("WsaHub 会：启动时检测 WSA；没有则引导全新安装，有则检查 WSABuilds 更新。"));
            p.Controls.Add(Lbl("安装目录: " + _installDir));
            p.Controls.Add(Btn("重新检测", RefreshHome));
            p.Controls.Add(Btn("启动 WSA 设置", () =>
                Process.Start(new ProcessStartInfo { FileName = "shell:AppsFolder\\MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe!SettingsApp", UseShellExecute = true })));
            p.Controls.Add(Btn("打开安装目录", () => Process.Start(new ProcessStartInfo { FileName = _installDir, UseShellExecute = true })));
            t.Controls.Add(p);
            return t;
        }

        TabPage BuildUpdate()
        {
            var t = new TabPage("更新与安装") { BackColor = Color.FromArgb(32, 32, 36) };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = true };
            var info = new Label { AutoSize = true, ForeColor = Color.White, MaximumSize = new Size(1000, 0) };
            p.Controls.Add(info);
            p.Controls.Add(Btn("检查更新", () =>
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        var json = HttpGet("https://api.github.com/repos/MustardChef/WSABuilds/releases?per_page=20");
                        // Split releases roughly by tag_name blocks; pick first LTS (or first) and use ONLY its assets.
                        var blocks = Regex.Split(json, "\\{\\s*\"url\"\\s*:\\s*\"https://api\\.github\\.com/repos/");
                        string tag = null, block = null;
                        foreach (var b in blocks)
                        {
                            var tm = Regex.Match(b, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                            if (!tm.Success) continue;
                            if (tag == null) { tag = tm.Groups[1].Value; block = b; }
                            if (tm.Groups[1].Value.IndexOf("LTS", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                tag = tm.Groups[1].Value; block = b; break;
                            }
                        }
                        string asset = null, url = null;
                        if (block != null)
                        {
                            foreach (Match m in Regex.Matches(block, "\"name\"\\s*:\\s*\"(WSA_[^\"]+\\.7z)\""))
                            {
                                var n = m.Groups[1].Value;
                                if (Regex.IsMatch(n, _assetPattern) && n.IndexOf("canary", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    asset = n;
                                    var um = Regex.Match(block, "\"browser_download_url\"\\s*:\\s*\"([^\"]+" + Regex.Escape(n) + ")\"");
                                    if (um.Success) url = um.Groups[1].Value.Replace("\\/", "/");
                                    break;
                                }
                            }
                        }
                        var inst = DetectVersion();
                        var av = Regex.Match(asset ?? "", @"WSA_(\d+\.\d+\.\d+\.\d+)").Groups[1].Value;
                        BeginInvoke((Action)(() =>
                        {
                            info.Text = "远端: " + tag + "\n包: " + (asset ?? "(无匹配)") + "\n本地: " + (inst.Length == 0 ? "(未安装)" : inst) +
                                "\n状态: " + ((av.Length > 0 && av == inst) ? "已是最新" : "可更新/可安装");
                            _pendingAsset = asset; _pendingUrl = url; _pendingVer = av; _pendingTag = tag;
                        }));
                    }
                    catch (Exception ex) { BeginInvoke((Action)(() => info.Text = ex.Message)); }
                });
            }));
            p.Controls.Add(Btn("下载并确认安装", () =>
            {
                ThreadPool.QueueUserWorkItem(_ => DownloadAndInstall());
            }));
            t.Controls.Add(p);
            return t;
        }

        string _pendingAsset, _pendingUrl, _pendingVer, _pendingTag;

        string ConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WsaHub", "config.json");
        }

        void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath());
                Directory.CreateDirectory(dir);
                var sb = new StringBuilder();
                sb.AppendLine("install=" + _installDir);
                sb.AppendLine("download=" + _downloadDir);
                sb.AppendLine("pattern=" + _assetPattern);
                File.WriteAllText(ConfigPath(), sb.ToString());
                Append("配置已保存: " + ConfigPath());
            }
            catch (Exception ex) { Append("保存配置失败: " + ex.Message); }
        }

        void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath())) return;
                foreach (var line in File.ReadAllLines(ConfigPath()))
                {
                    var i = line.IndexOf('=');
                    if (i <= 0) continue;
                    var k = line.Substring(0, i);
                    var v = line.Substring(i + 1);
                    if (k == "install") _installDir = v;
                    if (k == "download") _downloadDir = v;
                    if (k == "pattern") _assetPattern = v;
                }
            }
            catch { }
        }

        void BackupUserdata()
        {
            try
            {
                var backup = Path.Combine(_installDir, "_backup");
                Directory.CreateDirectory(backup);
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages", "MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe", "LocalCache");
                if (!Directory.Exists(local)) { Append("无 userdata LocalCache"); return; }
                foreach (var f in Directory.GetFiles(local, "userdata*.vhdx"))
                {
                    var dest = Path.Combine(backup, Path.GetFileName(f) + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    File.Copy(f, dest, true);
                    Append("已备份 userdata -> " + dest);
                }
            }
            catch (Exception ex) { Append("备份 userdata 失败: " + ex.Message); }
        }

        void ApplyAppDlls()
        {
            // Extract *_APP.dll from bundled Microsoft.VCLibs.140.00_x64.appx (zip) and copy beside host exes.
            try
            {
                var appx = Path.Combine(_installDir, "Microsoft.VCLibs.140.00_x64.appx");
                if (!File.Exists(appx)) { Append("无 VCLibs appx，跳过 *_APP.dll"); return; }
                var ext = Path.Combine(Path.GetTempPath(), "WsaHub-vclibs-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(ext);
                // .appx is a zip
                var zip = Path.Combine(ext, "vclibs.zip");
                File.Copy(appx, zip, true);
                Run("powershell.exe", "-NoProfile -Command \"Expand-Archive -LiteralPath '" + zip + "' -DestinationPath '" + ext + "' -Force\"");
                var appDlls = Directory.GetFiles(ext, "*_APP.dll", SearchOption.AllDirectories);
                var hosts = new[] { "", "WsaClient", "WsaService", "WsaSettingsBroker", "WsaProxy", "WSACrashUploader", "amd64" };
                int n = 0;
                foreach (var dll in appDlls)
                {
                    foreach (var h in hosts)
                    {
                        var destDir = h.Length == 0 ? _installDir : Path.Combine(_installDir, h);
                        if (!Directory.Exists(destDir)) continue;
                        try { File.Copy(dll, Path.Combine(destDir, Path.GetFileName(dll)), true); n++; } catch { }
                    }
                }
                Append("已应用 *_APP.dll x" + n);
                try { Directory.Delete(ext, true); } catch { }
            }
            catch (Exception ex) { Append("ApplyAppDlls: " + ex.Message); }
        }

        void RegisterElevated()
        {
            var ps1 = Path.Combine(_installDir, "_register.ps1");
            var inst = _installDir.Replace("'", "''");
            var body =
                "$ErrorActionPreference='Stop'\r\n" +
                "Get-Process WsaClient,WsaService,WsaSettings,vmmemWSA -ErrorAction SilentlyContinue | Stop-Process -Force\r\n" +
                "$ex = Get-AppxPackage -Name MicrosoftCorporationII.WindowsSubsystemForAndroid -ErrorAction SilentlyContinue\r\n" +
                "if ($ex) { try { Remove-AppxPackage -Package $ex.PackageFullName -PreserveApplicationData } catch { try { Remove-AppxPackage -Package $ex.PackageFullName } catch {} } }\r\n" +
                "Set-Location '" + inst + "'\r\n" +
                "[xml]$x = Get-Content .\\AppxManifest.xml\r\n" +
                "$arch = $x.Package.Identity.ProcessorArchitecture\r\n" +
                "foreach ($d in $x.Package.Dependencies.PackageDependency) {\r\n" +
                "  $dep = Get-AppxPackage -Name $d.Name -ErrorAction SilentlyContinue | ? { $_.Architecture -eq $arch } | sort Version | select -Last 1\r\n" +
                "  if (-not $dep -or ([version]$dep.Version -lt [version]$d.MinVersion)) {\r\n" +
                "    $a = Join-Path '" + inst + "' ($d.Name + '_' + $arch + '.appx')\r\n" +
                "    if (Test-Path $a) { Add-AppxPackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion -Path $a }\r\n" +
                "  }\r\n" +
                "}\r\n" +
                "Add-AppxPackage -ForceApplicationShutdown -ForceUpdateFromAnyVersion -Register .\\AppxManifest.xml\r\n" +
                "Write-Output REGISTER_OK\r\n";
            File.WriteAllText(ps1, body);
            Append("请求管理员注册 Appx...");
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + ps1 + "\"",
                    Verb = "runas",
                    UseShellExecute = true
                });
                if (p != null) { p.WaitForExit(180000); Append("注册脚本 exit=" + p.ExitCode); }
            }
            catch (Exception ex) { Append("提权注册失败: " + ex.Message); }
        }

        string HttpGet(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "WsaHub";
            req.Accept = "application/vnd.github+json";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        void DownloadAndInstall()
        {
            try
            {
                if (string.IsNullOrEmpty(_pendingUrl))
                {
                    BeginInvoke((Action)(() => MessageBox.Show(this, "请先点「检查更新」", "WsaHub")));
                    return;
                }
                Directory.CreateDirectory(_downloadDir);
                var dest = Path.Combine(_downloadDir, _pendingAsset);
                Append("下载 " + _pendingAsset + " ...");
                var req = (HttpWebRequest)WebRequest.Create(_pendingUrl);
                req.UserAgent = "WsaHub";
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var fs = File.Create(dest))
                using (var st = resp.GetResponseStream())
                {
                    var buf = new byte[256 * 1024];
                    int n; long total = 0;
                    while ((n = st.Read(buf, 0, buf.Length)) > 0) { fs.Write(buf, 0, n); total += n; }
                    Append("已下载 " + total + " bytes -> " + dest);
                }
                var confirm = MessageBox.Show(this,
                    "安装包已就绪：\n" + _pendingAsset + "\n版本 " + _pendingVer + "\nRelease " + (_pendingTag ?? "") + "\n\n安装流程：备份 userdata → 停止 WSA → 解压合并 → 补 *_APP.dll → 注册 Appx（UAC）\n继续？",
                    "WsaHub 确认安装", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) { Append("用户取消安装"); return; }

                BackupUserdata();
                Append("停止 WSA 进程...");
                foreach (var name in new[] { "WsaClient", "WsaService", "WsaSettings", "vmmemWSA", "WSACrashUploader" })
                    foreach (var pr in Process.GetProcessesByName(name)) { try { pr.Kill(); } catch { } }

                var seven = Find7z();
                if (seven == null) { Append("找不到 7z.exe，请安装 7-Zip/NanaZip"); return; }
                var stage = Path.Combine(_installDir, "_apply_stage");
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
                Directory.CreateDirectory(stage);
                Append("解压...");
                Run(seven, "x \"" + dest + "\" -o\"" + stage + "\" -y");
                var pkg = Directory.GetDirectories(stage);
                if (pkg.Length == 0) { Append("解压结果为空"); return; }
                Append("合并到 " + _installDir);
                CopyDir(pkg[0], _installDir);
                ApplyAppDlls();
                RegisterElevated();
                Append("安装流程结束（确认 Start Menu 中已有 Windows Subsystem for Android）");
                BeginInvoke((Action)RefreshHome);
            }
            catch (Exception ex) { Append("错误: " + ex.Message); }
        }

        void CopyDir(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.GetFiles(src))
            {
                var n = Path.GetFileName(f);
                if (n.StartsWith("_")) continue;
                try { File.Copy(f, Path.Combine(dest, n), true); } catch (Exception ex) { Append("skip " + n + " " + ex.Message); }
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                var n = Path.GetFileName(d);
                if (n == "_download" || n == "_backup" || n == "_apply_stage" || n == "platform-tools") continue;
                CopyDir(d, Path.Combine(dest, n));
            }
        }

        static string Find7z()
        {
            foreach (var c in new[] { @"C:\Program Files\7-Zip\7z.exe", @"C:\Program Files (x86)\7-Zip\7z.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\7z.exe") })
                if (File.Exists(c)) return c;
            return null;
        }

        static void Run(string exe, string args)
        {
            var p = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true });
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
        }

        TabPage BuildAdb()
        {
            var t = new TabPage("APK / ADB") { BackColor = Color.FromArgb(32, 32, 36) };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12), AutoScroll = true };
            var apk = new TextBox { Width = 480 };
            var pkg = new TextBox { Width = 320, Text = "com.android.vending" };
            p.Controls.Add(Lbl("APK 路径"));
            p.Controls.Add(apk);
            p.Controls.Add(Btn("选择 APK…", () =>
            {
                using (var ofd = new OpenFileDialog { Filter = "APK|*.apk|All|*.*" })
                    if (ofd.ShowDialog() == DialogResult.OK) apk.Text = ofd.FileName;
            }));
            p.Controls.Add(Btn("连接 ADB", () =>
            {
                var a = AdbPath();
                Append(RunOut(a, "connect 127.0.0.1:58526"));
                Append(RunOut(a, "devices"));
            }));
            p.Controls.Add(Btn("安装 APK", () =>
            {
                var a = AdbPath();
                Append(RunOut(a, "connect 127.0.0.1:58526"));
                Append(RunOut(a, "-s 127.0.0.1:58526 install -r \"" + apk.Text + "\"", 120000));
            }));
            p.Controls.Add(Lbl("包名"));
            p.Controls.Add(pkg);
            p.Controls.Add(Btn("启动包", () => Append(RunOut(AdbPath(), "shell monkey -p " + pkg.Text.Trim() + " -c android.intent.category.LAUNCHER 1"))));
            p.Controls.Add(Btn("卸载包", () =>
            {
                if (MessageBox.Show("卸载 " + pkg.Text + "？", "WsaHub", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    Append(RunOut(AdbPath(), "shell pm uninstall " + pkg.Text.Trim()));
            }));
            p.Controls.Add(Btn("截图", () =>
            {
                var a = AdbPath();
                RunOut(a, "shell screencap -p /sdcard/wsahub.png");
                Directory.CreateDirectory(_downloadDir);
                var dest = Path.Combine(_downloadDir, "wsahub_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                Append(RunOut(a, "pull /sdcard/wsahub.png \"" + dest + "\""));
            }));
            t.Controls.Add(p);
            return t;
        }

        string RunOut(string exe, string args, int timeoutMs = 20000)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = exe, Arguments = args,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                });
                var so = p.StandardOutput.ReadToEnd();
                var se = p.StandardError.ReadToEnd();
                p.WaitForExit(timeoutMs);
                return string.IsNullOrWhiteSpace(so) ? se : so;
            }
            catch (Exception ex) { return ex.Message; }
        }

        TabPage BuildApps()
        {
            var t = new TabPage("应用管理") { BackColor = Color.FromArgb(32, 32, 36) };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
            var list = new ListBox { Width = 520, Height = 320 };
            p.Controls.Add(Btn("刷新第三方应用", () =>
            {
                Append(RunOut(AdbPath(), "connect 127.0.0.1:58526"));
                var text = RunOut(AdbPath(), "shell pm list packages -3");
                list.Items.Clear();
                foreach (var line in text.Split('\n'))
                {
                    var s = line.Trim();
                    if (s.StartsWith("package:")) list.Items.Add(s.Substring(8));
                }
            }));
            p.Controls.Add(list);
            p.Controls.Add(Btn("启动选中", () =>
            {
                if (list.SelectedItem is string s) Append(RunOut(AdbPath(), "shell monkey -p " + s + " -c android.intent.category.LAUNCHER 1"));
            }));
            p.Controls.Add(Btn("卸载选中", () =>
            {
                if (list.SelectedItem is string s && MessageBox.Show("卸载 " + s + "?", "WsaHub", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Append(RunOut(AdbPath(), "shell pm uninstall " + s));
                    list.Items.Remove(s);
                }
            }));
            t.Controls.Add(p);
            return t;
        }

        TabPage BuildSettings()
        {
            var t = new TabPage("设置") { BackColor = Color.FromArgb(32, 32, 36) };
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(12) };
            var tbInst = new TextBox { Width = 480, Text = _installDir };
            var tbDl = new TextBox { Width = 480, Text = _downloadDir };
            var tbPat = new TextBox { Width = 480, Text = _assetPattern };
            p.Controls.Add(Lbl("WSA 安装目录")); p.Controls.Add(tbInst);
            p.Controls.Add(Lbl("下载目录")); p.Controls.Add(tbDl);
            p.Controls.Add(Lbl("资产正则")); p.Controls.Add(tbPat);
            p.Controls.Add(Btn("保存", () =>
            {
                _installDir = tbInst.Text.Trim();
                _downloadDir = tbDl.Text.Trim();
                _assetPattern = tbPat.Text.Trim();
                SaveConfig();
                Append("设置已更新");
            }));
            p.Controls.Add(Btn("备份 userdata", () =>
            {
                var backup = Path.Combine(_installDir, "_backup");
                Directory.CreateDirectory(backup);
                var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Packages", "MicrosoftCorporationII.WindowsSubsystemForAndroid_8wekyb3d8bbwe", "LocalCache");
                if (!Directory.Exists(local)) { Append("无 LocalCache"); return; }
                foreach (var f in Directory.GetFiles(local, "userdata*.vhdx"))
                {
                    var dest = Path.Combine(backup, Path.GetFileName(f) + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                    File.Copy(f, dest, true);
                    Append("备份 " + dest);
                }
            }));
            t.Controls.Add(p);
            return t;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "WsaHub-startup.log"), "winforms " + DateTime.Now.ToString("o"));
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "WsaHub-startup.log"), "\nfatal " + ex); } catch { }
                MessageBox.Show(ex.ToString(), "WsaHub fatal");
            }
        }
    }
}
