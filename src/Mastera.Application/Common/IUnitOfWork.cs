namespace Mastera.Application.Common;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Runs <paramref name="work"/> in one database transaction: committed on success, rolled back on failure.</summary>
    Task<Result<T>> InTransactionAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken);
}
