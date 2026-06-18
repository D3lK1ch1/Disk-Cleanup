using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace DiskCleanup.Core;

public static class Scanners
{
    public static List<CheckItem> RecycleBin()
    {
        var items = new List<CheckItem>();
        try
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null) return items;

            long total = 0;
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
            {
                var recycleBinPath = System.IO.Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin", sid);
                if (Directory.Exists(recycleBinPath))
                    total += GetDirectorySize(recycleBinPath);
            }
            items.Add(new CheckItem("Recycle Bin", total, "SAFE", Action: ActionKind.EmptyRecycleBin));
        }
        catch { }
        return items;
    }

    public static List<CheckItem> TempFolders()
    {
        var items = new List<CheckItem>();

        var userTemp = System.IO.Path.GetTempPath();
        if (Directory.Exists(userTemp))
            items.Add(new CheckItem("User Temp folder", GetDirectorySize(userTemp), "SAFE", userTemp, Action: ActionKind.DeleteContents));

        var softwareDistribution = @"C:\Windows\SoftwareDistribution\Download";
        if (Directory.Exists(softwareDistribution))
            items.Add(new CheckItem("Windows Update cache (SoftwareDistribution\\Download)", GetDirectorySize(softwareDistribution), "SAFE", softwareDistribution, Action: ActionKind.DeleteContents));

        return items;
    }

    public static List<CheckItem> VsCodeCache()
    {
        var items = new List<CheckItem>();
        var tempRoot = System.IO.Path.GetTempPath();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(tempRoot, "*CachedExtensionVSIXs*"))
                items.Add(new CheckItem($"VS Code extension cache ({System.IO.Path.GetFileName(dir)})", GetDirectorySize(dir), "SAFE", dir, Action: ActionKind.DeleteFolder));
        }
        catch { }
        return items;
    }

    public static List<CheckItem> Wsl()
    {
        var items = new List<CheckItem>();
        try
        {
            foreach (var distro in GetWslDistros())
            {
                var basePath = $@"\\wsl.localhost\{distro}\home";
                if (!Directory.Exists(basePath)) continue;

                foreach (var userDir in Directory.EnumerateDirectories(basePath))
                {
                    var cache = System.IO.Path.Combine(userDir, ".cache");
                    if (Directory.Exists(cache))
                        items.Add(new CheckItem($"WSL ({distro}) ~/.cache", GetDirectorySize(cache), "SAFE", cache, Action: ActionKind.DeleteFolder));

                    var npm = System.IO.Path.Combine(userDir, ".npm");
                    if (Directory.Exists(npm))
                        items.Add(new CheckItem($"WSL ({distro}) ~/.npm", GetDirectorySize(npm), "SAFE", npm, Action: ActionKind.DeleteFolder));

                    var projects = System.IO.Path.Combine(userDir, "projects");
                    if (Directory.Exists(projects))
                    {
                        foreach (var buildDir in FindBuildDirs(projects))
                        {
                            var rel = System.IO.Path.GetRelativePath(projects, buildDir);
                            items.Add(new CheckItem($"WSL ({distro}) ~/projects/{rel.Replace('\\', '/')}", GetDirectorySize(buildDir), "SAFE", buildDir, Action: ActionKind.DeleteFolder));
                        }
                    }
                }
            }
        }
        catch { }
        return items;
    }

    public static List<CheckItem> Docker()
    {
        var items = new List<CheckItem>();
        try
        {
            var psi = new ProcessStartInfo("docker", "system df")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var proc = Process.Start(psi);
            if (proc == null) return items;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            if (proc.ExitCode != 0) return items;

            // Skip the header row, parse the rest.
            var lines = output.Split('\n').Skip(1);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;

                // Columns are whitespace-padded: TYPE TOTAL ACTIVE SIZE RECLAIMABLE
                var cols = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length < 2) continue;

                var type = cols[0];
                var reclaimable = string.Join(' ', cols.Skip(cols.Length - 2));
                if (reclaimable.Contains('%') || reclaimable.Contains("0B"))
                {
                    var command = type switch
                    {
                        "Images" => "docker image prune -a",
                        "Containers" => "docker container prune",
                        "Local" or "Build" => "docker builder prune",
                        "Volumes" => "docker volume prune",
                        _ => "docker system prune"
                    };
                    items.Add(new CheckItem($"Docker {type} (reclaimable)", 0, "REVIEW", SizeOverride: reclaimable, Action: ActionKind.SuggestCommand, CommandSuggestion: command));
                }
            }
        }
        catch { }
        return items;
    }

    public static List<CheckItem> DownloadsTopFolders(int topN = 5)
    {
        var items = new List<CheckItem>();
        var downloads = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (!Directory.Exists(downloads)) return items;

        // Never offer to delete the disk-cleanup tool's own folder, even if it
        // ranks in the top N by size (e.g. after a few builds fill bin/obj).
        var selfRoot = GetSelfRootUnder(downloads);

        try
        {
            var entries = Directory.EnumerateFileSystemEntries(downloads)
                .Where(p => selfRoot == null || !string.Equals(p, selfRoot, StringComparison.OrdinalIgnoreCase))
                .Select(p => (Path: p, Size: Directory.Exists(p) ? GetDirectorySize(p) : SafeFileSize(p)))
                .OrderByDescending(x => x.Size)
                .Take(topN);

            foreach (var (path, size) in entries)
                items.Add(new CheckItem($"Downloads\\{System.IO.Path.GetFileName(path)}", size, "REVIEW", path, Action: ActionKind.MoveFolderToRecycleBin));
        }
        catch { }
        return items;
    }

    /// <summary>
    /// Walks up from the running executable's location to find the ancestor
    /// directory that sits directly inside <paramref name="downloads"/>, if any.
    /// Returns null if this tool isn't running from somewhere under Downloads.
    /// </summary>
    static string? GetSelfRootUnder(string downloads)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir?.Parent != null)
        {
            if (string.Equals(dir.Parent.FullName.TrimEnd('\\'), downloads.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    public static List<CheckItem> StalePackages(int monthsThreshold = 6)
    {
        var items = new List<CheckItem>();
        var packagesDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packagesDir)) return items;

        var cutoff = DateTime.Now.AddMonths(-monthsThreshold);
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(packagesDir))
            {
                try
                {
                    var lastWrite = Directory.GetLastWriteTime(dir);
                    if (lastWrite >= cutoff) continue;

                    var size = GetDirectorySize(dir);
                    if (size > 0)
                        items.Add(new CheckItem(
                            $"AppData\\Local\\Packages\\{System.IO.Path.GetFileName(dir)} (untouched since {lastWrite:yyyy-MM-dd})",
                            size, "REVIEW", dir, Action: ActionKind.MoveFolderToRecycleBin));
                }
                catch { }
            }
        }
        catch { }
        return items;
    }

    public static List<CheckItem> InstalledAppsBySize(int topN = 10)
    {
        var apps = new List<(string Name, long SizeBytes)>();
        var keys = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };

        foreach (var (hive, path) in keys)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) continue;

                foreach (var subKeyName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        var name = subKey?.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (subKey?.GetValue("EstimatedSize") is int sizeKb && sizeKb > 0)
                            apps.Add((name, (long)sizeKb * 1024));
                    }
                    catch { }
                }
            }
            catch { }
        }

        return apps.OrderByDescending(a => a.SizeBytes)
            .Take(topN)
            .Select(a => new CheckItem($"Installed: {a.Name}", a.SizeBytes, "INFO"))
            .ToList();
    }

    public static List<CheckItem> AiFolders()
    {
        var items = new List<CheckItem>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (var name in new[] { ".claude", ".codex" })
        {
            var dir = System.IO.Path.Combine(home, name);
            if (Directory.Exists(dir))
                items.Add(new CheckItem($"{name} folder", GetDirectorySize(dir), "REVIEW", dir, Action: ActionKind.MoveFolderToRecycleBin));
        }
        return items;
    }

    // --- helpers ---

    static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                try { size += new FileInfo(file).Length; }
                catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                // Don't follow symlinks/junctions - they may point outside this
                // tree (e.g. pnpm-style node_modules links, WSL mounts) and would
                // give a misleading size and an unsafe target for later deletion.
                try
                {
                    if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint))
                        continue;
                }
                catch { continue; }

                size += GetDirectorySize(dir);
            }
        }
        catch { }
        return size;
    }

    static long SafeFileSize(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }

    static List<string> GetWslDistros()
    {
        var distros = new List<string>();
        try
        {
            var psi = new ProcessStartInfo("wsl", "-l -q")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.Unicode
            };
            using var proc = Process.Start(psi);
            if (proc == null) return distros;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);

            distros = output.Split('\n')
                .Select(l => l.Trim().Replace("\0", ""))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();
        }
        catch { }
        return distros;
    }

    static List<string> FindBuildDirs(string root)
    {
        var results = new List<string>();
        void Walk(string dir, int depth)
        {
            if (depth > 6) return;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = System.IO.Path.GetFileName(sub);
                    if (name == "node_modules" || name == "target")
                    {
                        results.Add(sub);
                        continue;
                    }
                    Walk(sub, depth + 1);
                }
            }
            catch { }
        }
        Walk(root, 0);
        return results;
    }
}
