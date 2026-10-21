using NodaTime;

namespace Mastera.Infrastructure.Outbox;

/// <summary>
/// Task 6.1: a domain event waiting to be handled. Written in the same transaction as the change that raised it,
/// so a booking is never saved without its notification (and never notified without being saved).
/// Platform table: the processor reads it for every salon.
/// </summary>
public sealed class OutboxMessage
{
    public const int MaxAttempts = 10;

    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public Instant OccurredAt { get; init; }

    public Instant? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? Error { get; set; }
}

/// <summary>Task 6.2: one row per notification that was sent, so a repeated delivery doesn't send it again.</summary>
public sealed class NotificationLogEntry
{
    public required string Key { get; init; }

    public Instant CreatedAt { get; init; }
}

/// <summary>Task 6.6: a reminder SMS to send at <see cref="DueAt"/>.</summary>
public sealed class ScheduledReminder
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid BookingId { get; init; }

    public required string Kind { get; init; }

    public Instant DueAt { get; init; }

    public Instant? SentAt { get; set; }

    public int Attempts { get; set; }
}

/// <summary>Task 6.5: a one-time code a master sends to the Telegram bot (only its SHA-256 is stored).</summary>
public sealed class TelegramLinkRequest
{
    public required string CodeHash { get; init; }

    public Guid TenantId { get; init; }

    public Guid MasterId { get; init; }

    public Instant ExpiresAt { get; init; }
}
