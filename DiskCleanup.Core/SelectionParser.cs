namespace DiskCleanup.Core;

public static class SelectionParser
{
    /// <summary>
    /// Parses user input into a sorted list of 1-based item numbers.
    /// "all-safe" selects every item whose risk (in <paramref name="risks"/>) is "SAFE".
    /// Comma-separated numbers outside 1..risks.Count or non-numeric tokens are ignored.
    /// </summary>
    public static List<int> Parse(string input, IReadOnlyList<string> risks)
    {
        input = input.Trim();
        if (input.Length == 0)
            return new List<int>();

        if (input.Equals("all-safe", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Range(1, risks.Count)
                .Where(n => risks[n - 1] == "SAFE")
                .ToList();
        }

        var result = new List<int>();
        foreach (var part in input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(part, out var num) && num >= 1 && num <= risks.Count)
                result.Add(num);
        }

        return result.Distinct().OrderBy(n => n).ToList();
    }
}
