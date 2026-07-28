namespace NetWatch.Application.Common.Interfaces;

/// <summary>
/// Commits everything the repositories staged in the current scope as one transaction.
/// Repositories deliberately do not save on their own: a check result, a probe state
/// update and an incident row must land together or not at all.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
