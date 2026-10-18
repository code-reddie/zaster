using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zaster.Backup;

/// <summary>
/// Namen und Aufbewahrung der automatischen Sicherungen. Jede Sicherung bekommt einen Zeitstempel (Ortszeit) im Namen.
/// Behalten wird je Tag, je Kalenderwoche und je Monat die neueste Sicherung, jeweils für die 5 jüngsten Tage,
/// Wochen und Monate, in denen es eine Sicherung gibt. Alles andere ist überflüssig.
/// </summary>
public static partial class BackupRetention
{
    public const int Days = 5;
    public const int Weeks = 5;
    public const int Months = 5;

    private const string StampFormat = "yyyy-MM-dd_HH-mm-ss";

    /// <summary>
    /// Muster für die Anzeige, z. B. in den Einstellungen.
    /// </summary>
    public const string FileNamePattern = "zaster-JJJJ-MM-TT_HH-MM-SS.db";

    public static string FileName(DateTime localTime) =>
        $"zaster-{localTime.ToString(StampFormat, CultureInfo.InvariantCulture)}.db";

    /// <summary>
    /// Zeitstempel aus einem Dateinamen von <see cref="FileName"/>. Andere Dateien (auch eine alte <c>zaster.db</c>)
    /// gehören nicht zu den Sicherungen und werden nie gelöscht.
    /// </summary>
    public static bool TryParse(string fileName, out DateTime localTime)
    {
        localTime = default;
        var match = FileNameRegex().Match(fileName);
        return match.Success && DateTime.TryParseExact(
            match.Groups[1].Value, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out localTime);
    }

    /// <summary>
    /// Alle Sicherungen im Ordner mit ihrem Zeitstempel.
    /// </summary>
    public static IReadOnlyList<(string Path, DateTime Time)> Find(string directory) =>
        System.IO.Directory.EnumerateFiles(directory)
            .Select(path => (Path: path, Ok: TryParse(System.IO.Path.GetFileName(path), out var time), Time: time))
            .Where(f => f.Ok)
            .Select(f => (f.Path, f.Time))
            .ToList();

    /// <summary>
    /// Die Sicherungen, die nicht mehr ins Raster passen.
    /// </summary>
    public static IReadOnlyList<T> Obsolete<T>(IEnumerable<T> backups, Func<T, DateTime> time)
    {
        var newestFirst = backups.OrderByDescending(time).ToList();
        var keep = new HashSet<T>();
        keep.UnionWith(NewestPer(newestFirst, b => time(b).Date, Days));
        keep.UnionWith(NewestPer(newestFirst, b => (ISOWeek.GetYear(time(b)), ISOWeek.GetWeekOfYear(time(b))), Weeks));
        keep.UnionWith(NewestPer(newestFirst, b => (time(b).Year, time(b).Month), Months));
        return newestFirst.Where(b => !keep.Contains(b)).ToList();
    }

    /// <summary>
    /// Je Zeitraum die neueste Sicherung, für die <paramref name="count"/> jüngsten Zeiträume.
    /// <paramref name="newestFirst"/> ist absteigend sortiert, also ist das erste Element einer Gruppe das neueste.
    /// </summary>
    private static IEnumerable<T> NewestPer<T, TKey>(List<T> newestFirst, Func<T, TKey> period, int count) =>
        newestFirst.GroupBy(period).Take(count).Select(g => g.First());

    [GeneratedRegex(@"^zaster-(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})\.db$")]
    private static partial Regex FileNameRegex();
}
