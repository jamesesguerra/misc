using Dapper;
using eqd.Context;
using eqd.Models;
using eqd.Interfaces;
using System.Text;

namespace eqd.Repositories;

public class ConfigurationsRepository : IConfigurationsRepository
{
    private readonly ConfigurationsContext _configurationsContext;

    public ConfigurationsRepository(ConfigurationsContext configurationsContext)
    {
        _configurationsContext = configurationsContext;
    }

    public async Task<IEnumerable<Threshold>> GetThresholds(ThresholdParameter parameter)
    {
        IEnumerable<Threshold> thresholds;
        var parameters = new DynamicParameters();
        using var connection = _configurationsContext.CreateConnection();

        try
        {
            var getThresholdsQuery = @" SELECT  Id,
                                                GroupId,
                                                ThresholdTypeId,
                                                CreatedBy,
                                                CreatedDate,
                                                UpdatedBy,
                                                UpdatedDate,
                                                ApprovedBy,
                                                AtRiskThreshold,
                                                NoDetectionThreshold,
                                                InWarningThreshold,
                                                FullThreshold
                                        FROM    [dbo].[Thresholds] 
                                        WHERE   1 = 1 ";

            var queryBuilder = new StringBuilder(getThresholdsQuery);

            if (parameter.Id != 0)
            {
                queryBuilder.Append(" AND Id = @Id ");
                parameters.Add("@Id", parameter.Id);
            }

            if (parameter.GroupId.HasValue)
            {
                queryBuilder.Append(" AND GroupId = @GroupId ");
                parameters.Add("@GroupId", parameter.GroupId.Value);
            }

            if (parameter.ThresholdTypeId.HasValue)
            {
                queryBuilder.Append(" AND ThresholdTypeId = @ThresholdTypeId ");
                parameters.Add("@ThresholdTypeId", parameter.ThresholdTypeId.Value);
            }

            thresholds = await connection.QueryAsync<Threshold>(queryBuilder.ToString(), parameters);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while trying to retrieve the thresholds", ex);
        }
        
        return thresholds;
    }

    public async void UpsertThresholds(IEnumerable<Threshold> thresholds)
    {
        var existingThresholds = thresholds.Where(t => t.Id != 0);
        var newThresholds = thresholds.Where(t => t.Id == 0);
        var existingThresholdIds = existingThresholds.Select(t => t.Id);

        var createThresholdQuery = @" INSERT INTO [dbo].[Thresholds] (
                                                    GroupId,
                                                    ThresholdTypeId,
                                                    CreatedBy,
                                                    CreatedDate,
                                                    UpdatedBy,
                                                    UpdatedDate,
                                                    ApprovedBy,
                                                    AtRiskThreshold,
                                                    NoDetectionThreshold,
                                                    InWarningThreshold,
                                                    FullThreshold )
                                          VALUES (  @GroupId,
                                                    @ThresholdTypeId,
                                                    @CreatedBy,
                                                    @CreatedDate,
                                                    @UpdatedBy,
                                                    @UpdatedDate,
                                                    @ApprovedBy,
                                                    @AtRiskThreshold,
                                                    @NoDetectionThreshold,
                                                    @InWarningThreshold,
                                                    @FullThreshold ); ";

        var updateThresholdQuery = @" UPDATE [dbo].[Thresholds]
                                          SET
                                                GroupId = @GroupId,
                                                ThresholdTypeId = @ThresholdTypeId,
                                                CreatedBy = @CreatedBy,
                                                CreatedDate = @CreatedDate,
                                                UpdatedBy = @UpdatedBy,
                                                UpdatedDate = @UpdatedDate,
                                                ApprovedBy = @ApprovedBy,
                                                AtRiskThreshold = @AtRiskThreshold,
                                                NoDetectionThreshold = @NoDetectionThreshold,
                                                InWarningThreshold = @InWarningThreshold,
                                                FullThreshold = @FullThreshold
                                          WHERE Id = @Id";
        
        var deleteThresholdsQuery = " DELETE FROM [dbo].[Thresholds] WHERE Id NOT IN @existingThresholdIds ";
        
        using var connection = _configurationsContext.CreateConnection();

        try
        {
            foreach (var threshold in existingThresholds)
            {
                await connection.ExecuteAsync(updateThresholdQuery, new
                {
                    threshold.Id,
                    threshold.GroupId,
                    threshold.ThresholdTypeId,
                    threshold.CreatedBy,
                    threshold.CreatedDate,
                    threshold.UpdatedBy,
                    threshold.UpdatedDate,
                    threshold.ApprovedBy,
                    threshold.AtRiskThreshold,
                    threshold.NoDetectionThreshold,
                    threshold.InWarningThreshold,
                    threshold.FullThreshold
                });
            }
            
            await connection.ExecuteAsync(deleteThresholdsQuery, new { existingThresholdIds });
            
            foreach (var threshold in newThresholds)
            {
                await connection.ExecuteAsync(createThresholdQuery, new
                {
                    threshold.GroupId,
                    threshold.ThresholdTypeId,
                    threshold.CreatedBy,
                    threshold.CreatedDate,
                    threshold.UpdatedBy,
                    threshold.UpdatedDate,
                    threshold.ApprovedBy,
                    threshold.AtRiskThreshold,
                    threshold.NoDetectionThreshold,
                    threshold.InWarningThreshold,
                    threshold.FullThreshold
                });
            }
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while trying to upsert the thresholds", ex);
        }
    }

}
