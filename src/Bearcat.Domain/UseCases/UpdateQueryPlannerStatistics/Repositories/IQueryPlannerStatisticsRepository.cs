namespace Bearcat.Domain.UseCases.UpdateQueryPlannerStatistics.Repositories;

public interface IQueryPlannerStatisticsRepository
{
    Task UpdateQueryPlannerStatisticsAsync(CancellationToken cancellationToken);
}
