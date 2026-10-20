using Mastera.Application.Common;
using Mastera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mastera.Infrastructure.Repositories;

public sealed class UnitOfWork(MasteraDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // Another booking or hold for the same master got there first (see BookingConstraints).
            throw new SlotTakenException("The time slot was taken by someone else.", e);
        }
    }

    public async Task<Result<T>> InTransactionAsync<T>(
        Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var result = await work(cancellationToken);

        if (result.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        return result;
    }
}
