using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace DayPlanner;

public sealed record ScheduleItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public int Start { get; set; }
    public int? End { get; set; }
    public string Color { get; set; } = "#009DA4";
    public bool ReminderEnabled { get; set; }
    public int ReminderMinutes { get; set; } = 5;
    public bool IsPoint => End is null;
    public string TimeLabel => End is int end ? $"{TimeMath.Format(Start)}—{TimeMath.Format(end)} · {end - Start}分钟" : $"{TimeMath.Format(Start)} · 时间点";
    public ScheduleItem Copy() => this with { };
}

public sealed class PlannerData
{
    public int Version { get; set; } = 2;
    public int GridMinutes { get; set; } = 10;
    public bool CloseToTray { get; set; }
    public bool RememberCloseChoice { get; set; }
    public HashSet<string> ReminderReceipts { get; set; } = [];
    public List<string> CustomColors { get; set; } = [];
    public Dictionary<string, List<ScheduleItem>> Days { get; set; } = [];
    // Convenience for today's data; the persisted document contains all dates in Days.
    [JsonIgnore]
    public List<ScheduleItem> Items { get => ForDate(DateTime.Today); set => Days[DateKey(DateTime.Today)] = value; }
    public static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public List<ScheduleItem> ForDate(DateTime date)
    {
        var key = DateKey(date);
        if (!Days.TryGetValue(key, out var items)) Days[key] = items = [];
        return items;
    }
}

public static class TimeMath
{
    public static string Format(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";
    public static bool TryParse(string text, out int minutes)
    {
        minutes = 0;
        var parts = text.Trim().Replace('：', ':').Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out int hour) || !int.TryParse(parts[1], out int minute)) return false;
        if (hour < 0 || hour > 24 || minute < 0 || minute > 59 || (hour == 24 && minute != 0)) return false;
        minutes = hour * 60 + minute;
        return true;
    }
    public static int Snap(double minute, int grid) => Numeric.Clamp((int)Math.Round(minute / grid, MidpointRounding.AwayFromZero) * grid, 0, 1440);
    public static (int Start, int? End) Move(ScheduleItem item, int delta)
    {
        var length = (item.End ?? item.Start) - item.Start;
        var start = Numeric.Clamp(item.Start + delta, 0, item.IsPoint ? 1439 : 1440 - length);
        return (start, item.IsPoint ? null : start + length);
    }
    public static bool IsValid(ScheduleItem item) => !string.IsNullOrWhiteSpace(item.Title) && item.Title.Length <= 120 && item.Start >= 0 && item.Start < 1440 && (item.End is null || item.End > item.Start && item.End <= 1440);
}

public sealed class ScheduleStore(string path)
{
    public string FilePath => path;
    public bool RequiresMigration { get; private set; }
    public PlannerData Load()
    {
        if (!File.Exists(path)) return new();
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("Version", out var versionElement) || !versionElement.TryGetInt32(out var version) || (version != 1 && version != 2))
            throw new InvalidDataException("日程文件格式异常，原文件已保留。");
        var data = root.Deserialize<PlannerData>() ?? throw new InvalidDataException("日程文件内容为空。");
        if (version == 1)
        {
            if (!root.TryGetProperty("Items", out var oldItems)) throw new InvalidDataException("日程文件缺少标记内容。");
            var items = oldItems.Deserialize<List<ScheduleItem>>() ?? throw new InvalidDataException("日程内容异常。");
            data.Days = new() { [PlannerData.DateKey(DateTime.Today)] = items };
        }
        else if (!root.TryGetProperty("Days", out _)) throw new InvalidDataException("日程文件缺少日期内容。");
        if (data.Days is null || data.Days.Any(day =>
            !DateTime.TryParseExact(day.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            day.Value is null || day.Value.Any(item => item is null || !TimeMath.IsValid(item)) ||
            day.Value.Select(item => item.Id).Distinct().Count() != day.Value.Count))
            throw new InvalidDataException("日期或标记内容异常，原文件已保留。");
        data.Version = 2;
        data.GridMinutes = data.GridMinutes == 5 ? 5 : 10;
        foreach (var item in data.Days.Values.SelectMany(items => items))
        {
            item.Color = NormalizeColor(item.Color) ?? "#009DA4";
            if (item.ReminderMinutes is < 1 or > 1440) item.ReminderMinutes = 5;
        }
        data.ReminderReceipts ??= [];
        data.CustomColors = (data.CustomColors ?? []).Select(NormalizeColor).OfType<string>().Where(c => !Palette.Contains(c)).Distinct().ToList();
        RequiresMigration = version == 1;
        return data;
    }
    public void Save(PlannerData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (RequiresMigration && File.Exists(path) && !File.Exists(path + ".v1.bak"))
            File.Copy(path, path + ".v1.bak");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
        RequiresMigration = false;
    }
    public static readonly string[] Palette = ["#009DA4", "#7564F4", "#E6A23A", "#428BD0", "#CF7497"];
    public static string? NormalizeColor(string? value)
    {
        var hex = value?.Trim().TrimStart('#');
        if (hex is null || (hex.Length != 3 && hex.Length != 6) || !hex.All(Uri.IsHexDigit)) return null;
        if (hex.Length == 3) hex = string.Concat(hex.Select(c => new string(c, 2)));
        return "#" + hex.ToUpperInvariant();
    }
}

public sealed class PlannerHistory
{
    private readonly Stack<List<ScheduleItem>> undo = new();
    private readonly Stack<List<ScheduleItem>> redo = new();
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    private static List<ScheduleItem> Copy(IEnumerable<ScheduleItem> items) => items.Select(x => x.Copy()).ToList();
    public void Remember(IEnumerable<ScheduleItem> items) { undo.Push(Copy(items)); redo.Clear(); }
    public List<ScheduleItem> Undo(IEnumerable<ScheduleItem> current) { redo.Push(Copy(current)); return undo.Pop(); }
    public List<ScheduleItem> Redo(IEnumerable<ScheduleItem> current) { undo.Push(Copy(current)); return redo.Pop(); }
}
