using InfoClusMonitor.Api.Models.Entities;

namespace InfoClusMonitor.Api.Services;

public static class ScheduleCalculationHelper
{
    public static TimeZoneInfo GetParaguayTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Asuncion");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Paraguay Standard Time");
            }
            catch
            {
                // Fallback a GMT-3 / GMT-4
                return TimeZoneInfo.CreateCustomTimeZone("Paraguay_Custom", TimeSpan.FromHours(-3), "Paraguay Standard Time", "Paraguay Standard Time");
            }
        }
    }

    public static DateTime ToParaguayTime(DateTime utcDateTime)
    {
        var tz = GetParaguayTimeZone();
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.Kind == DateTimeKind.Utc ? utcDateTime : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), tz);
    }

    public static DateTime ToUtcFromParaguayTime(DateTime pyDateTime)
    {
        var tz = GetParaguayTimeZone();
        var unspecified = DateTime.SpecifyKind(pyDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }

    public static string FormatParaguayDateTime(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue) return "Pendiente";
        var pyTime = ToParaguayTime(utcDateTime.Value);
        return pyTime.ToString("yyyy-MM-dd HH:mm:ss") + " (PY)";
    }

    public static DateTime? CalculateNextRunAt(ScheduledTask task, DateTime fromUtc)
    {
        if (!task.IsEnabled) return null;

        var pyTz = GetParaguayTimeZone();
        var pyNow = TimeZoneInfo.ConvertTimeFromUtc(fromUtc.Kind == DateTimeKind.Utc ? fromUtc : DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc), pyTz);

        switch (task.ScheduleType?.ToLowerInvariant())
        {
            case "everyhours":
            case "hours":
            {
                var hours = Math.Max(1, task.IntervalValue ?? 1);
                return fromUtc.AddHours(hours);
            }

            case "everydays":
            case "days":
            {
                var daysInterval = Math.Max(1, task.IntervalValue ?? 1);
                var (hour, minute) = ParseTime(task.ScheduledTime);

                var targetPyToday = new DateTime(pyNow.Year, pyNow.Month, pyNow.Day, hour, minute, 0, DateTimeKind.Unspecified);
                DateTime targetPy;
                if (targetPyToday > pyNow)
                {
                    targetPy = targetPyToday;
                }
                else
                {
                    targetPy = targetPyToday.AddDays(daysInterval);
                }

                return TimeZoneInfo.ConvertTimeToUtc(targetPy, pyTz);
            }

            case "specificdays":
            case "weekdays":
            {
                var (hour, minute) = ParseTime(task.ScheduledTime);
                var daysList = (task.DaysOfWeek ?? "Monday")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(d => ParseDayOfWeek(d))
                    .Where(d => d.HasValue)
                    .Select(d => d!.Value)
                    .ToList();

                if (daysList.Count == 0) daysList.Add(DayOfWeek.Monday);

                for (int i = 0; i <= 14; i++)
                {
                    var checkDay = pyNow.Date.AddDays(i);
                    if (daysList.Contains(checkDay.DayOfWeek))
                    {
                        var candidatePy = new DateTime(checkDay.Year, checkDay.Month, checkDay.Day, hour, minute, 0, DateTimeKind.Unspecified);
                        if (candidatePy > pyNow)
                        {
                            return TimeZoneInfo.ConvertTimeToUtc(candidatePy, pyTz);
                        }
                    }
                }

                return fromUtc.AddDays(1);
            }

            case "once":
            {
                if (task.SpecificDate.HasValue && task.SpecificDate.Value > fromUtc)
                {
                    return task.SpecificDate.Value;
                }
                return null;
            }

            default:
            {
                var defaultHours = Math.Max(1, task.IntervalValue ?? 1);
                return fromUtc.AddHours(defaultHours);
            }
        }
    }

    private static (int Hour, int Minute) ParseTime(string? timeStr)
    {
        if (string.IsNullOrWhiteSpace(timeStr)) return (3, 0); // Default 03:00 AM

        var parts = timeStr.Trim().Split(':');
        int hour = 0;
        int min = 0;

        if (parts.Length >= 1 && int.TryParse(parts[0], out var h))
            hour = Math.Clamp(h, 0, 23);
        if (parts.Length >= 2 && int.TryParse(parts[1], out var m))
            min = Math.Clamp(m, 0, 59);

        return (hour, min);
    }

    private static DayOfWeek? ParseDayOfWeek(string dayStr)
    {
        if (Enum.TryParse<DayOfWeek>(dayStr, true, out var dow))
            return dow;

        // Soporte en español
        return dayStr.ToLowerInvariant() switch
        {
            "lunes" or "lun" or "1" => DayOfWeek.Monday,
            "martes" or "mar" or "2" => DayOfWeek.Tuesday,
            "miercoles" or "miércoles" or "mie" or "mié" or "3" => DayOfWeek.Wednesday,
            "jueves" or "jue" or "4" => DayOfWeek.Thursday,
            "viernes" or "vie" or "5" => DayOfWeek.Friday,
            "sabado" or "sábado" or "sab" or "sáb" or "6" => DayOfWeek.Saturday,
            "domingo" or "dom" or "0" or "7" => DayOfWeek.Sunday,
            _ => null
        };
    }

    public static string GenerateScheduleSummary(ScheduledTask task)
    {
        switch (task.ScheduleType?.ToLowerInvariant())
        {
            case "everyhours":
            case "hours":
                return task.IntervalValue == 1 ? "Cada 1 hora" : $"Cada {task.IntervalValue} horas";

            case "everydays":
            case "days":
                var days = task.IntervalValue ?? 1;
                var time = task.ScheduledTime ?? "03:00";
                return days == 1 ? $"Todos los días a las {time} (PY)" : $"Cada {days} días a las {time} (PY)";

            case "specificdays":
            case "weekdays":
                var dList = task.DaysOfWeek ?? "Lunes";
                var t = task.ScheduledTime ?? "03:00";
                return $"Días [{dList}] a las {t} (PY)";

            case "once":
                return task.SpecificDate.HasValue
                    ? $"Una sola vez el {FormatParaguayDateTime(task.SpecificDate.Value)}"
                    : "Ejecución única";

            default:
                return $"Frecuencia: {task.ScheduleType}";
        }
    }
}
