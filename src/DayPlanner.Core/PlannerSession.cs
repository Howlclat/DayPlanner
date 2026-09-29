namespace DayPlanner.Core;

public enum PlannerMode { Day, Week, Month }

public static class PlanningDates
{
    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);
    public static int MonthRows(DateTime date) => (((int)MonthStart(date).DayOfWeek + 6) % 7 + DateTime.DaysInMonth(date.Year, date.Month) + 6) / 7;
    public static IReadOnlyList<ScheduleItem> Items(PlannerData data, DateTime date) => data.Days.TryGetValue(PlannerData.DateKey(date), out var items) ? items : [];
}

public sealed class PlannerSession
{
    private readonly Dictionary<DateTime, PlannerHistory> histories = [];
    public ScheduleStore Store { get; }
    public PlannerData Data { get; }
    public DateTime Date { get; private set; } = DateTime.Today;
    public PlannerMode Mode { get; set; }
    public string? LoadError { get; }
    public string Status { get; private set; } = "已自动保存";
    public bool HasUnsavedChanges { get; private set; }
    public event Action? Changed;
    public event Action? Saved;
    public PlannerSession(string path)
    {
        Store = new(path);
        try { Data = Store.Load(); if (Store.RequiresMigration) Save(); }
        catch (Exception ex) { Data = new(); LoadError = ex.Message; Status = "数据读取失败，原文件已保留：" + ex.Message; }
    }
    public IReadOnlyList<ScheduleItem> Items => PlanningDates.Items(Data, Date);
    public PlannerHistory History => histories.TryGetValue(Date, out var history) ? history : histories[Date] = new();
    public void Select(DateTime date)
    {
        Date = date.Date < new DateTime(2, 1, 1) ? new(2, 1, 1) : date.Date > new DateTime(9998, 12, 31) ? new(9998, 12, 31) : date.Date;
        Refresh();
    }
    public void Navigate(int delta)
    {
        try { Select(Mode == PlannerMode.Month ? Date.AddMonths(delta) : Date.AddDays(delta * (Mode == PlannerMode.Week ? 7 : 1))); }
        catch (ArgumentOutOfRangeException) { }
    }
    public void Put(ScheduleItem item)
    {
        if (!TimeMath.IsValid(item)) throw new ArgumentException("请检查标题和起止时间。");
        Commit(() => { var list = Data.ForDate(Date); var index = list.FindIndex(x => x.Id == item.Id); if (index < 0) list.Add(item.Copy()); else list[index] = item.Copy(); });
    }
    public void Delete(ScheduleItem item) => Commit(() => Data.ForDate(Date).RemoveAll(x => x.Id == item.Id));
    public void ChangeTime(ScheduleItem item, int start, int? end) => Put(item with { Start = start, End = end });
    private void Commit(Action change)
    {
        if (LoadError != null) return;
        History.Remember(Items); change(); Save(); Refresh();
    }
    public void Undo() { if (LoadError == null && History.CanUndo) { Data.Days[PlannerData.DateKey(Date)] = History.Undo(Items); Save(); Refresh(); } }
    public void Redo() { if (LoadError == null && History.CanRedo) { Data.Days[PlannerData.DateKey(Date)] = History.Redo(Items); Save(); Refresh(); } }
    public bool Save()
    {
        if (LoadError != null) return false;
        HasUnsavedChanges = true;
        try { Store.Save(Data); HasUnsavedChanges = false; Status = "已自动保存"; }
        catch (Exception ex) { Status = "保存失败，请重试：" + ex.Message; return false; }
        Saved?.Invoke();
        return true;
    }
    public void Refresh() => Changed?.Invoke();
    public IEnumerable<(DateTime Date, ScheduleItem Item, string Key)> Due(DateTime now)
    {
        foreach (var day in new[] { now.Date, now.Date.AddDays(1) })
        foreach (var item in PlanningDates.Items(Data, day))
        {
            var start = day.AddMinutes(item.Start);
            var key = $"{PlannerData.DateKey(day)}/{item.Id:N}/{item.Start}/{item.ReminderMinutes}";
            if (item.ReminderEnabled && now >= start.AddMinutes(-item.ReminderMinutes) && now < start && !Data.ReminderReceipts.Contains(key))
                yield return (day, item, key);
        }
    }
}
