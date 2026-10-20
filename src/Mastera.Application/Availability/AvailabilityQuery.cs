using Mastera.Application.Catalog;
using Mastera.Application.Common;
using Mastera.Application.Masters;
using Mastera.Application.Schedules;
using Mastera.Application.Tenancy;
using Mastera.Domain.Availability;
using Mastera.Domain.Catalog;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using NodaTime;

namespace Mastera.Application.Availability;

public sealed record MasterSlotOption(Guid MasterId, string MasterName, decimal Price, int DurationMinutes, DateTimeOffset End);

/// <summary>A free start time: local wall-clock time for display, the instant for booking, and who is free.</summary>
public sealed record AvailableSlot(string Time, DateTimeOffset Start, IReadOnlyList<MasterSlotOption> Masters);

public sealed record AvailabilityView(string Date, string TimeZone, Guid ServiceId, IReadOnlyList<AvailableSlot> Slots);

/// <summary>Task 4.6: free times for a service on a date, for one master or any master.</summary>
public sealed class AvailabilityQuery(
    ITenantContext tenantContext,
    ITenantSettings tenantSettings,
    ICatalogRepository catalog,
    IMasterRepository masters,
    IScheduleRepository schedules,
    IBusyTimeSource busyTime,
    IClock clock)
{
    public async Task<Result<AvailabilityView>> GetAsync(Guid serviceId, Guid? masterId, string? date, CancellationToken ct)
    {
        if (tenantContext.Current is not { } salon)
        {
            return Result.Failure<AvailabilityView>(Error.NotFound("tenant.none", "No salon at this address."));
        }

        LocalDate day;
        try
        {
            day = ScheduleMapping.ParseDate(date);
        }
        catch (DomainException e)
        {
            return Result.Failure<AvailabilityView>(Errors.FromDomain(e));
        }

        var service = await catalog.FindServiceAsync(new ServiceId(serviceId), ct);
        if (service is null || service.IsArchived)
        {
            return Result.Failure<AvailabilityView>(Error.NotFound("service.not_found", "The service was not found."));
        }

        var candidates = (await masters.ListOfferingAsync(service.Id, ct))
            .Where(m => m.IsActive && (masterId is null || m.Id.Value == masterId))
            .ToList();

        if (masterId is not null && candidates.Count == 0)
        {
            return Result.Failure<AvailabilityView>(Error.NotFound("master.not_found", "This master doesn't offer this service."));
        }

        var zone = await tenantSettings.GetTimeZoneAsync(salon.Id, ct);
        var ids = candidates.Select(m => m.Id).ToList();
        var schedulesById = (await schedules.ListAsync(ids, ct)).ToDictionary(s => s.MasterId);
        var dayStart = zone.AtStartOfDay(day).ToInstant();
        var busy = await busyTime.GetBusyAsync(ids, dayStart, zone.AtStartOfDay(day.PlusDays(1)).ToInstant(), ct);

        var inputs = candidates.Select(m => new MasterAvailabilityInput(
            m.Id.Value,
            schedulesById.GetValueOrDefault(m.Id)?.ScheduleOn(day),
            Offer(m, service).EffectiveDuration(service),
            busy.GetValueOrDefault(m.Id) ?? []));

        var byId = candidates.ToDictionary(m => m.Id.Value);
        var slots = AvailabilityCalculator
            .GetSlotsForAnyMaster(inputs, day, zone, AvailabilityRules.Default, clock.GetCurrentInstant())
            .Select(slot => new AvailableSlot(
                ScheduleMapping.Format(slot.Start.InZone(zone).TimeOfDay),
                slot.Start.ToDateTimeOffset(),
                slot.Masters.Select(option =>
                {
                    var master = byId[option.MasterKey];
                    var offer = Offer(master, service);
                    return new MasterSlotOption(
                        master.Id.Value,
                        master.DisplayName,
                        offer.EffectivePrice(service),
                        (int)offer.EffectiveDuration(service).TotalMinutes,
                        option.End.ToDateTimeOffset());
                }).ToList()))
            .ToList();

        return Result.Success(new AvailabilityView(ScheduleMapping.Format(day), zone.Id, service.Id.Value, slots));
    }

    private static MasterService Offer(Master master, Service service) =>
        master.Services.Single(s => s.ServiceId == service.Id);
}
