namespace DiskCleanup.Core;

public enum ActionKind
{
    None,
    EmptyRecycleBin,
    DeleteContents,
    DeleteFolder,
    MoveFolderToRecycleBin,
    SuggestCommand,
}

public record CheckItem(
    string Label,
    long SizeBytes,
    string Risk,
    string? Path = null,
    string? SizeOverride = null,
    ActionKind Action = ActionKind.None,
    string? CommandSuggestion = null)
{
    public string FormattedSize => SizeOverride ?? Format(SizeBytes);

    static string Format(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.#}{units[unit]}";
    }
}
