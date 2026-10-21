using System.Text.Json;
using System.Text.Json.Serialization;
using Mastera.Domain.Common;
using NodaTime;
using NodaTime.Text;

namespace Mastera.Infrastructure.Outbox;

/// <summary>Turns domain events into JSON for the outbox and back. Event types are found in the Domain assembly by name.</summary>
public static class DomainEventSerializer
{
    private static readonly Dictionary<string, Type> Types = typeof(IDomainEvent).Assembly.GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IDomainEvent).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    private static readonly JsonSerializerOptions Options = new() { Converters = { new InstantConverter() } };

    public static (string Type, string Payload) Serialize(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var type = domainEvent.GetType();
        return (type.Name, JsonSerializer.Serialize(domainEvent, type, Options));
    }

    public static IDomainEvent Deserialize(string type, string payload) =>
        Types.TryGetValue(type, out var clrType)
            ? (IDomainEvent)JsonSerializer.Deserialize(payload, clrType, Options)!
            : throw new InvalidOperationException($"Unknown domain event type '{type}'.");

    private sealed class InstantConverter : JsonConverter<Instant>
    {
        public override Instant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            InstantPattern.ExtendedIso.Parse(reader.GetString()!).Value;

        public override void Write(Utf8JsonWriter writer, Instant value, JsonSerializerOptions options) =>
            writer.WriteStringValue(InstantPattern.ExtendedIso.Format(value));
    }
}
