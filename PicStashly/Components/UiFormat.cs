namespace PicStashly.Components;

internal static class UiFormat
{
    public static string FileSize(long bytes)
    {
        if (bytes < 0)
        {
            return "N/A";
        }

        if (bytes == 0)
        {
            return "0 B";
        }

        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int index = (int)Math.Floor(Math.Log(bytes) / Math.Log(1024));
        index = Math.Clamp(index, 0, sizes.Length - 1);
        double value = bytes / Math.Pow(1024, index);
        return $"{value:0.##} {sizes[index]}";
    }

    public static List<string> ParseTags(string? text) =>
        (text ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string JoinTags(IEnumerable<string>? tags) =>
        string.Join(", ", tags ?? []);

    public static (string BaseName, string Extension) SplitFileName(string? fileName)
    {
        string name = fileName ?? string.Empty;
        int dotIndex = name.LastIndexOf('.');
        if (dotIndex <= 0)
        {
            return (name, string.Empty);
        }

        return (name[..dotIndex], name[dotIndex..]);
    }
}
