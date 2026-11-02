using System.Globalization;
using PijacaUDzepu.API.Models;

namespace PijacaUDzepu.API.Helpers;

// Da li tezga sada prima porudžbine: pauza, neradni dani (ClosedDates) i radno vreme po danima
// (OpeningHours, Day 1 = ponedeljak ... 7 = nedelja). Bez podešenog radnog vremena tezga radi stalno.
public static class VendorAvailability
{
    public const string ReasonPaused = "Paused";
    public const string ReasonClosedDay = "ClosedDay";
    public const string ReasonHours = "Hours";
    public const string ReasonInactive = "Inactive";

    // "otvara se u sredu" - dani u akuzativu
    private static readonly string[] DayNames = { "", "u ponedeljak", "u utorak", "u sredu", "u četvrtak", "u petak", "u subotu", "u nedelju" };

    public record Status(bool IsOpen, string? ClosedReason, DateTime? OpensAt, string? ClosesAt);

    public static Status Evaluate(Vendor vendor) => Evaluate(vendor, LocalTime.Now);

    public static Status Evaluate(Vendor vendor, DateTime now)
    {
        if (!vendor.IsActive) return new(false, ReasonInactive, null, null);
        if (vendor.OrdersPaused) return new(false, ReasonPaused, null, null);

        var today = DateOnly.FromDateTime(now);
        var closedDates = vendor.ClosedDates ?? new List<DateOnly>();
        var hours = vendor.OpeningHours?.Where(h => IsValid(h)).ToList() ?? new List<DeliveryScheduleEntry>();
        var closedToday = closedDates.Contains(today);

        if (hours.Count == 0)
            return closedToday
                ? new(false, ReasonClosedDay, NextOpening(today, now, closedDates, hours), null)
                : new(true, null, null, null);

        var todayHours = hours.FirstOrDefault(h => h.Day == IsoDay(today));
        if (!closedToday && todayHours != null)
        {
            var from = today.ToDateTime(ParseTime(todayHours.From));
            var to = today.ToDateTime(ParseTime(todayHours.To));
            if (now >= from && now < to) return new(true, null, null, todayHours.To);
        }

        return new(false, closedToday ? ReasonClosedDay : ReasonHours, NextOpening(today, now, closedDates, hours), null);
    }

    // Poruka za kupca kada pokuša da poruči od zatvorene tezge
    public static string ClosedMessage(Vendor vendor, Status status) => status.ClosedReason switch
    {
        ReasonPaused => $"{vendor.Name} trenutno ne prima porudžbine.",
        ReasonInactive => $"{vendor.Name} trenutno ne radi.",
        // Bez radnog vremena (samo neradni dan) tezga radi od početka dana - vreme se ne navodi
        _ when status.OpensAt is { } at && at.TimeOfDay == TimeSpan.Zero => $"{vendor.Name} danas ne radi – otvara se {DescribeDay(at)}.",
        _ when status.OpensAt is { } at => $"{vendor.Name} je sada zatvoren(a) – otvara se {DescribeDay(at)} u {at:HH\\:mm}.",
        _ => $"{vendor.Name} je sada zatvoren(a)."
    };

    public static bool IsValid(DeliveryScheduleEntry h) =>
        h.Day is >= 1 and <= 7 && TryParseTime(h.From, out var from) && TryParseTime(h.To, out var to) && from < to;

    private static DateTime? NextOpening(DateOnly today, DateTime now, List<DateOnly> closedDates, List<DeliveryScheduleEntry> hours)
    {
        for (var d = 0; d <= 14; d++)
        {
            var date = today.AddDays(d);
            if (closedDates.Contains(date)) continue;
            if (hours.Count == 0) return date.ToDateTime(TimeOnly.MinValue);

            var h = hours.FirstOrDefault(x => x.Day == IsoDay(date));
            if (h == null) continue;
            var from = date.ToDateTime(ParseTime(h.From));
            if (from > now) return from;
        }
        return null;
    }

    private static string DescribeDay(DateTime at)
    {
        var today = LocalTime.Today;
        var date = DateOnly.FromDateTime(at);
        if (date == today) return "danas";
        if (date == today.AddDays(1)) return "sutra";
        return DayNames[IsoDay(date)];
    }

    private static int IsoDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

    private static TimeOnly ParseTime(string value) => TryParseTime(value, out var t) ? t : TimeOnly.MinValue;

    private static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value ?? "", new[] { "HH:mm", "H:mm" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
