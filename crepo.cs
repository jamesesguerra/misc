using Dapper;
using System.Text;
using SM.Detection.API.Context;
using SM.Detection.API.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SM.Detection.API.Repositories;

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

    public async Task<bool> UpsertThresholds(IEnumerable<Threshold> thresholds)
    {
        var existingThresholds = thresholds.Where(t => t.Id != 0);
        var newThresholds = thresholds.Where(t => t.Id == 0);
        var existingThresholdIds = string.Join(',', existingThresholds.Select(t => t.Id));

        var createThresholdQuery = @" INSERT INTO [dbo].[Thresholds] (
                                                   GroupId,
                                                   ThresholdTypeId,
                                                   CreatedBy,
                                                   CreatedDate,
                                                   ApprovedBy,
                                                   AtRiskThreshold,
                                                   NoDetectionThreshold,
                                                   InWarningThreshold,
                                                   FullThreshold )
                                          VALUES ( @GroupId,
                                                   @ThresholdTypeId,
                                                   @CreatedBy,
                                                   @CreatedDate,
                                                   @ApprovedBy,
                                                   @AtRiskThreshold,
                                                   @NoDetectionThreshold,
                                                   @InWarningThreshold,
                                                   @FullThreshold ); ";

        var updateThresholdQuery = @" UPDATE [dbo].[Thresholds]
                                          SET
                                                GroupId = @GroupId,
                                                ThresholdTypeId = @ThresholdTypeId,
                                                UpdatedBy = @UpdatedBy,
                                                UpdatedDate = @UpdatedDate,
                                                ApprovedBy = @ApprovedBy,
                                                AtRiskThreshold = @AtRiskThreshold,
                                                NoDetectionThreshold = @NoDetectionThreshold,
                                                InWarningThreshold = @InWarningThreshold,
                                                FullThreshold = @FullThreshold
                                          WHERE Id = @Id";

        var deleteThresholdsQuery = $" DELETE FROM [dbo].[Thresholds] WHERE Id NOT IN ({existingThresholdIds}) ";

        using (var connection = _configurationsContext.CreateConnection())
        {
            await connection.OpenAsync();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    foreach (var threshold in existingThresholds)
                    {
                        await ExecuteInsertOrUpdateCommand(updateThresholdQuery, connection, transaction, new SqlParameter[]
                        {
                            new SqlParameter("@Id", threshold.Id),
                            new SqlParameter("@GroupId", threshold.GroupId),
                            new SqlParameter("@ThresholdTypeId", threshold.ThresholdTypeId),
                            new SqlParameter("@UpdatedBy", threshold.UpdatedBy),
                            new SqlParameter("@UpdatedDate", DateTime.UtcNow),
                            new SqlParameter("@ApprovedBy", threshold.ApprovedBy),
                            new SqlParameter("@AtRiskThreshold", threshold.AtRiskThreshold),
                            new SqlParameter("@NoDetectionThreshold", threshold.NoDetectionThreshold),
                            new SqlParameter("@InWarningThreshold", threshold.InWarningThreshold),
                            new SqlParameter("@FullThreshold", threshold.FullThreshold)
                        });
                    }

                    await ExecuteInsertOrUpdateCommand(deleteThresholdsQuery, connection, transaction, new SqlParameter[] {});

                    foreach (var threshold in newThresholds)
                    {
                        await ExecuteInsertOrUpdateCommand(createThresholdQuery, connection, transaction, new SqlParameter[]
                        {
                            new SqlParameter("@GroupId", threshold.GroupId),
                            new SqlParameter("@ThresholdTypeId", threshold.ThresholdTypeId),
                            new SqlParameter("@CreatedBy", threshold.CreatedBy),
                            new SqlParameter("@CreatedDate", DateTime.UtcNow),
                            new SqlParameter("@ApprovedBy", threshold.ApprovedBy),
                            new SqlParameter("@AtRiskThreshold", threshold.AtRiskThreshold),
                            new SqlParameter("@NoDetectionThreshold", threshold.NoDetectionThreshold),
                            new SqlParameter("@InWarningThreshold", threshold.InWarningThreshold),
                            new SqlParameter("@FullThreshold", threshold.FullThreshold),
                        });
                    }

                    transaction.Commit();
                    return true;
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new ApplicationException("An error occurred while trying to upsert the thresholds", ex);
                }
            }
        }
    }

    private async Task ExecuteInsertOrUpdateCommand(string query, SqlConnection connection, SqlTransaction transaction, SqlParameter[] parameters)
    {
        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }
    }
}
