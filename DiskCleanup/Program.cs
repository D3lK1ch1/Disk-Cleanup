using DiskCleanup.Core;

var items = new List<CheckItem>();

items.AddRange(Scanners.RecycleBin());
items.AddRange(Scanners.TempFolders());
items.AddRange(Scanners.VsCodeCache());
items.AddRange(Scanners.Wsl());
items.AddRange(Scanners.Docker());
items.AddRange(Scanners.DockerVhdxBloat());
items.AddRange(Scanners.DownloadsTopFolders());
items.AddRange(Scanners.StalePackages());
items.AddRange(Scanners.AiFolders());
items.AddRange(Scanners.InstalledAppsBySize());

Console.WriteLine("disk-cleanup — scan results");
Console.WriteLine("============================");
Console.WriteLine();

for (int i = 0; i < items.Count; i++)
{
    var item = items[i];
    Console.WriteLine($"[{i + 1}] {item.Label} — {item.FormattedSize} — {item.Risk} — {item.ActionDescription}");
}

Console.WriteLine();
Console.WriteLine($"{items.Count} items found.");
Console.WriteLine();

Console.Write("Enter numbers to act on (e.g. 1,2,5 or all-safe), or press Enter to quit: ");
var input = Console.ReadLine() ?? "";

var risks = items.Select(i => i.Risk).ToList();
var selectedNumbers = SelectionParser.Parse(input, risks);

if (selectedNumbers.Count == 0)
{
    Console.WriteLine("Nothing selected. Exiting.");
    return;
}

var selectedItems = selectedNumbers.Select(n => items[n - 1]).ToList();

Console.WriteLine();
Console.WriteLine("The following items will be processed:");
foreach (var n in selectedNumbers)
{
    var item = items[n - 1];
    Console.WriteLine($"[{n}] {item.Label} — {item.FormattedSize} — {item.Risk}");
}

Console.Write("Proceed? (y/n): ");
var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
if (confirm != "y" && confirm != "yes")
{
    Console.WriteLine("Cancelled. Nothing was changed.");
    return;
}

var systemDrive = System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))!;
var freeBefore = new DriveInfo(systemDrive).AvailableFreeSpace;

Console.WriteLine();
Console.WriteLine("Results:");
foreach (var item in selectedItems)
{
    var result = ActionExecutor.Execute(item);
    var status = result.Success ? "OK" : "FAILED";
    Console.WriteLine($"[{status}] {item.Label}: {result.Message}");
}

var freeAfter = new DriveInfo(systemDrive).AvailableFreeSpace;

Console.WriteLine();
Console.WriteLine($"Free space on {systemDrive} before: {FormatBytes(freeBefore)}");
Console.WriteLine($"Free space on {systemDrive} after:  {FormatBytes(freeAfter)}");
Console.WriteLine($"Difference: {FormatBytes(freeAfter - freeBefore)}");

static string FormatBytes(long bytes)
{
    string[] units = { "B", "KB", "MB", "GB", "TB" };
    double size = bytes;
    int unit = 0;
    var sign = size < 0 ? "-" : "";
    size = Math.Abs(size);
    while (size >= 1024 && unit < units.Length - 1)
    {
        size /= 1024;
        unit++;
    }
    return $"{sign}{size:0.#}{units[unit]}";
}
