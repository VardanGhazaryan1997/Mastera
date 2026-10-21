using System.Globalization;
using Mastera.Domain.Tenants;

namespace Mastera.Application.Notifications;

/// <summary>Short texts for SMS, Telegram and email, in the salon's language (Georgian, Russian or English).</summary>
public static class NotificationTexts
{
    public static string Booked(Language language, string salon, string service, string when, string link) => Pick(language,
        ka: $"{salon}: თქვენ ჩაწერილი ხართ — {service}, {when}. შეცვლა ან გაუქმება: {link}",
        ru: $"{salon}: вы записаны — {service}, {when}. Изменить или отменить: {link}",
        en: $"{salon}: you're booked — {service}, {when}. Change or cancel: {link}");

    public static string Moved(Language language, string salon, string service, string when, string link) => Pick(language,
        ka: $"{salon}: თქვენი ვიზიტი გადატანილია — {service}, {when}. {link}",
        ru: $"{salon}: ваша запись перенесена — {service}, {when}. {link}",
        en: $"{salon}: your visit was moved — {service}, {when}. {link}");

    public static string CancelledBySalon(Language language, string salon, string service, string when) => Pick(language,
        ka: $"{salon}: სამწუხაროდ, თქვენი ვიზიტი ({service}, {when}) გაუქმდა. დაგვიკავშირდით ახალი დროისთვის.",
        ru: $"{salon}: к сожалению, ваша запись ({service}, {when}) отменена. Свяжитесь с нами, чтобы выбрать другое время.",
        en: $"{salon}: sorry, your visit ({service}, {when}) was cancelled. Contact us to pick another time.");

    public static string Reminder(Language language, string salon, string service, string when, string link) => Pick(language,
        ka: $"შეხსენება — {salon}: {service}, {when}. {link}",
        ru: $"Напоминание — {salon}: {service}, {when}. {link}",
        en: $"Reminder — {salon}: {service}, {when}. {link}");

    public static string ForMaster(Language language, string kind, string client, string service, string when) => kind switch
    {
        NotificationKinds.Created => Pick(language, $"🆕 ახალი ჩაწერა: {client} — {service}, {when}", $"🆕 Новая запись: {client} — {service}, {when}", $"🆕 New booking: {client} — {service}, {when}"),
        NotificationKinds.Cancelled => Pick(language, $"❌ გაუქმდა: {client} — {service}, {when}", $"❌ Отмена: {client} — {service}, {when}", $"❌ Cancelled: {client} — {service}, {when}"),
        NotificationKinds.Rescheduled => Pick(language, $"🔁 გადატანილია: {client} — {service}, {when}", $"🔁 Перенос: {client} — {service}, {when}", $"🔁 Moved: {client} — {service}, {when}"),
        _ => Pick(language, $"✔ დადასტურდა: {client} — {service}, {when}", $"✔ Подтверждено: {client} — {service}, {when}", $"✔ Confirmed: {client} — {service}, {when}"),
    };

    public static string TelegramLinked(Language language, string master) => Pick(language,
        ka: $"✅ {master}, ახლა აქ მიიღებთ შეტყობინებებს ახალი ჩაწერების შესახებ.",
        ru: $"✅ {master}, теперь уведомления о записях будут приходить сюда.",
        en: $"✅ {master}, booking notifications will now arrive here.");

    public const string TelegramLinkInvalid = "This link has expired. Open Mastera and press \"Connect Telegram\" again.";

    public static string FormatWhen(NodaTime.ZonedDateTime when) =>
        when.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);

    private static string Pick(Language language, string ka, string ru, string en) => language.Code switch
    {
        "ka" => ka,
        "ru" => ru,
        _ => en,
    };
}

public static class NotificationKinds
{
    public const string Created = "booking.created";
    public const string Confirmed = "booking.confirmed";
    public const string Cancelled = "booking.cancelled";
    public const string Rescheduled = "booking.rescheduled";
}
