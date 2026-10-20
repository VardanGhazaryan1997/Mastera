using Mastera.Application.Common;
using Mastera.Application.Masters;
using Mastera.Application.Users;
using Mastera.Domain.Common;
using Mastera.Domain.Masters;
using Mastera.Domain.Memberships;
using Mastera.Domain.Schedules;

namespace Mastera.Application.Schedules;

public sealed record ExceptionInput(string? From, string? To, DayScheduleDto? Hours, string? Note);

/// <summary>
/// Task 3.4: reading and changing a master's schedule. Owners and admins change everything; a master can add and
/// remove their own exceptions (days off), but not their regular pattern.
/// </summary>
public sealed class ScheduleHandler(
    IScheduleRepository schedules,
    IMasterRepository masters,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork)
{
    public async Task<Result<ScheduleView>> GetAsync(Guid masterId, CancellationToken ct)
    {
        var master = await masters.FindAsync(new MasterId(masterId), ct);
        if (master is null)
        {
            return Result.Failure<ScheduleView>(MasterNotFound());
        }

        var schedule = await schedules.FindAsync(master.Id, ct);
        return Result.Success(schedule is null
            ? new ScheduleView(masterId, null, [])
            : ScheduleMapping.ToView(schedule));
    }

    public async Task<Result<ScheduleView>> SetPatternAsync(Guid masterId, WorkPatternDto? pattern, CancellationToken ct)
    {
        if (currentUser.Role?.CanManageSalon() != true)
        {
            return Result.Failure<ScheduleView>(Forbidden());
        }

        var loaded = await LoadAsync(masterId, ct);
        if (!loaded.IsSuccess)
        {
            return Result.Failure<ScheduleView>(loaded.Error!);
        }

        var schedule = loaded.Value!;
        try
        {
            schedule.SetPattern(pattern is null ? null : ScheduleMapping.ToDomain(pattern));
        }
        catch (DomainException e)
        {
            return Result.Failure<ScheduleView>(Errors.FromDomain(e));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ScheduleMapping.ToView(schedule));
    }

    public async Task<Result<ScheduleExceptionDto>> AddExceptionAsync(Guid masterId, ExceptionInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var loaded = await LoadAsync(masterId, ct, requireOwnOrManager: true);
        if (!loaded.IsSuccess)
        {
            return Result.Failure<ScheduleExceptionDto>(loaded.Error!);
        }

        var schedule = loaded.Value!;
        ScheduleException exception;
        try
        {
            exception = schedule.AddException(
                ScheduleMapping.ParseDate(input.From),
                ScheduleMapping.ParseDate(input.To ?? input.From),
                input.Hours is null ? null : ScheduleMapping.ToDomain(input.Hours),
                input.Note);
        }
        catch (DomainException e)
        {
            return Result.Failure<ScheduleExceptionDto>(Errors.FromDomain(e));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(ScheduleMapping.ToDto(exception));
    }

    public async Task<Result<bool>> RemoveExceptionAsync(Guid masterId, Guid exceptionId, CancellationToken ct)
    {
        var loaded = await LoadAsync(masterId, ct, requireOwnOrManager: true);
        if (!loaded.IsSuccess)
        {
            return Result.Failure<bool>(loaded.Error!);
        }

        var schedule = loaded.Value!;
        if (!schedule.RemoveException(new ScheduleExceptionId(exceptionId)))
        {
            return Result.Failure<bool>(Error.NotFound("schedule.exception.not_found", "The exception was not found."));
        }

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }

    private async Task<Result<MasterSchedule>> LoadAsync(Guid masterId, CancellationToken ct, bool requireOwnOrManager = false)
    {
        var master = await masters.FindAsync(new MasterId(masterId), ct);
        if (master is null)
        {
            return Result.Failure<MasterSchedule>(MasterNotFound());
        }

        if (requireOwnOrManager && !CanEditExceptions(master))
        {
            return Result.Failure<MasterSchedule>(Forbidden());
        }

        var schedule = await schedules.FindAsync(master.Id, ct);
        if (schedule is null)
        {
            schedule = MasterSchedule.For(master.Id);
            schedules.Add(schedule);
        }

        return Result.Success(schedule);
    }

    private bool CanEditExceptions(Master master) =>
        currentUser.Role?.CanManageSalon() == true
        || (currentUser.Role == MembershipRole.Master && currentUser.UserId is { } id && master.UserId == id);

    private static Error MasterNotFound() => Error.NotFound("master.not_found", "The master was not found.");

    private static Error Forbidden() =>
        new("schedule.forbidden", "You can only change your own days off.", ErrorKind.Forbidden);
}
