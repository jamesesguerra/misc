using Dapper;
using System.Text;
using eqd.Context;
using eqd.Models;
using eqd.Interfaces;
using Microsoft.Data.SqlClient;

namespace eqd.Repositories;

public enum ThresholdTypes { DetectionSpecialist, Group }

public class ConfigurationsRepository : IConfigurationsRepository
{
    private readonly ConfigurationsContext _configurationsContext;
    private readonly IEmailSenderService _emailSenderService;

    public ConfigurationsRepository(ConfigurationsContext configurationsContext, IEmailSenderService emailSenderService)
    {
        _configurationsContext = configurationsContext;
        _emailSenderService = emailSenderService;
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

            var filterConditions = CreateFilterConditions(parameter);
            foreach (var condition in filterConditions)
            {
                queryBuilder.Append(condition);
            }
            
            var dynamicParameters = CreateDynamicParameters(parameter);
            thresholds = await connection.QueryAsync<Threshold>(queryBuilder.ToString(), dynamicParameters);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while trying to retrieve the thresholds", ex);
        }

        return thresholds;
    }

    public async Task<IEnumerable<Threshold>> UpsertThresholds(IEnumerable<Threshold> thresholds)
    {
        var existingThresholds = thresholds.Where(t => t.Id != 0).ToList();
        var newThresholds = thresholds.Where(t => t.Id == 0).ToList();
        var updatedThresholds = new List<Threshold>();

        var existingThresholdIds = string.Join(',', existingThresholds.Select(t => t.Id));
        var deleteThresholdsQuery = $"DELETE FROM [dbo].[Thresholds] WHERE Id NOT IN ({existingThresholdIds})";

        using (var connection = _configurationsContext.CreateConnection())
        {
            await connection.OpenAsync();
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    await UpdateExistingThresholds(existingThresholds, connection, transaction, updatedThresholds);
                    await ExecuteInsertOrUpdateCommand(deleteThresholdsQuery, connection, transaction, Array.Empty<SqlParameter>());
                    await CreateNewThresholds(newThresholds, connection, transaction, updatedThresholds);
                
                    transaction.Commit();
                    return updatedThresholds.AsEnumerable();
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    throw new ApplicationException("An error occurred while trying to upsert the thresholds", ex);
                }
            }
        }
    }

    #region private methods
    private async Task UpdateExistingThresholds(IEnumerable<Threshold> existingThresholds, SqlConnection connection, SqlTransaction transaction, ICollection<Threshold> updatedThresholds)
    {
        var updateThresholdQuery = @"
            UPDATE [dbo].[Thresholds]
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
    
        foreach (var threshold in existingThresholds)
        {
            var currentThreshold = await connection.QueryFirstOrDefaultAsync<Threshold>(
                "SELECT * FROM [dbo].[Thresholds] WHERE Id = @Id", new { threshold.Id }, transaction);
    
            // skip updating the threshold if no fields were actually changed
            if (currentThreshold is null || !IsThresholdUpdated(threshold, currentThreshold)) continue;
    
            updatedThresholds.Add(threshold);
            await ExecuteInsertOrUpdateCommand(updateThresholdQuery, connection, transaction, GetSqlParameters(threshold, true));
        }
    }

    private async Task CreateNewThresholds(
        IEnumerable<Threshold> newThresholds,
        SqlConnection connection, 
        SqlTransaction transaction,
        ICollection<Threshold> updatedThresholds)
    {
        var createThresholdQuery = @"
            INSERT INTO [dbo].[Thresholds] (
                GroupId,
                ThresholdTypeId,
                CreatedBy,
                CreatedDate,
                ApprovedBy,
                AtRiskThreshold,
                NoDetectionThreshold,
                InWarningThreshold,
                FullThreshold)
            VALUES (
                @GroupId,
                @ThresholdTypeId,
                @CreatedBy,
                @CreatedDate,
                @ApprovedBy,
                @AtRiskThreshold,
                @NoDetectionThreshold,
                @InWarningThreshold,
                @FullThreshold);";
    
        foreach (var threshold in newThresholds)
        {
            updatedThresholds.Add(threshold);
            await ExecuteInsertOrUpdateCommand(createThresholdQuery, connection, transaction, GetSqlParameters(threshold, false));
        }
    }

    private SqlParameter[] GetSqlParameters(Threshold threshold, bool isUpdate)
    {
        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@GroupId", threshold.GroupId),
            new SqlParameter("@ThresholdTypeId", threshold.ThresholdTypeId),
            new SqlParameter("@ApprovedBy", threshold.ApprovedBy),
            new SqlParameter("@AtRiskThreshold", threshold.AtRiskThreshold),
            new SqlParameter("@NoDetectionThreshold", threshold.NoDetectionThreshold),
            new SqlParameter("@InWarningThreshold", threshold.InWarningThreshold),
            new SqlParameter("@FullThreshold", threshold.FullThreshold)
        };
    
        if (isUpdate)
        {
            parameters.Add(new SqlParameter("@Id", threshold.Id));
            parameters.Add(new SqlParameter("@UpdatedBy", threshold.UpdatedBy));
            parameters.Add(new SqlParameter("@UpdatedDate", DateTime.UtcNow));
        }
        else
        {
            parameters.Add(new SqlParameter("@CreatedBy", threshold.CreatedBy));
            parameters.Add(new SqlParameter("@CreatedDate", DateTime.UtcNow));
        }
    
        return parameters.ToArray();
    }
    
    private List<string> CreateFilterConditions(ThresholdParameter parameter)
    {
        List<string> conditions = new List<string>();
        
        if (parameter.Id != 0) conditions.Add(" AND Id = @Id ");
        if (parameter.GroupId.HasValue) conditions.Add(" AND GroupId = @GroupId ");
        if (parameter.ThresholdTypeId.HasValue) conditions.Add(" AND ThresholdTypeId = @ThresholdTypeId ");

        return conditions;
    }

    private DynamicParameters CreateDynamicParameters(ThresholdParameter parameter)
    {
        var parameters = new Dictionary<string, object>();
        
        if (parameter.Id != 0) parameters.Add("@Id", parameter.Id);
        if (parameter.GroupId.HasValue) parameters.Add("@GroupId", parameter.GroupId.Value);
        if (parameter.ThresholdTypeId.HasValue) parameters.Add("@ThresholdTypeId", parameter.ThresholdTypeId.Value);
        
        return new DynamicParameters(parameters);
    }

    private bool IsThresholdUpdated(Threshold newThreshold, Threshold? currentThreshold = null)
    {
        if (currentThreshold == null) return true;
        if (newThreshold.GroupId != currentThreshold.GroupId) return true;
        
        if (!string.Equals(newThreshold.ApprovedBy, currentThreshold.ApprovedBy, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        
        if (newThreshold.ThresholdTypeId == (int)ThresholdTypes.DetectionSpecialist)
        {
            if (newThreshold.InWarningThreshold != currentThreshold.InWarningThreshold) return true;
            if (newThreshold.FullThreshold != currentThreshold.FullThreshold) return true;
        }
        else if (newThreshold.ThresholdTypeId == (int)ThresholdTypes.Group)
        {
            if (newThreshold.AtRiskThreshold != currentThreshold.AtRiskThreshold) return true;
            if (newThreshold.NoDetectionThreshold != currentThreshold.NoDetectionThreshold) return true;
        }

        return false;
    }
    
    private async Task ExecuteInsertOrUpdateCommand(string query, SqlConnection connection, SqlTransaction transaction, SqlParameter[] parameters)
    {
        using (var command = new SqlCommand(query, connection, transaction))
        {
            command.Parameters.AddRange(parameters);
            await command.ExecuteNonQueryAsync();
        }
    }
    #endregion
}
