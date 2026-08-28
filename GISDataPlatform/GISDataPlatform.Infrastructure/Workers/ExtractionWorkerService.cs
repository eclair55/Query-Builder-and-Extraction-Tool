using System.Text.Json;
using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using GISDataPlatform.Infrastructure.DatabaseProviders;
using GISDataPlatform.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GISDataPlatform.Infrastructure.Workers;

public class ExtractionWorkerService : BackgroundService
{
    private readonly ExtractionJobQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExtractionWorkerService> _logger;

    public ExtractionWorkerService(
        ExtractionJobQueue queue,
        IServiceProvider serviceProvider,
        ILogger<ExtractionWorkerService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Extraction Background Worker Service Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_queue.TryDequeue(out var job) && job != null)
            {
                try
                {
                    job.Status = "PROCESSING";
                    _logger.LogInformation($"Processing extraction job {job.JobId}");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var factory = scope.ServiceProvider.GetRequiredService<DatabaseProviderFactory>();
                        var dbCatalog = scope.ServiceProvider.GetRequiredService<IDatabaseCatalog>();
                        var layerCatalog = scope.ServiceProvider.GetRequiredService<ILayerCatalog>();
                        var exportEngine = scope.ServiceProvider.GetRequiredService<IExportEngine>();

                        var queryDef = JsonSerializer.Deserialize<QueryDefinition>(job.QueryDefinitionJson);
                        if (queryDef != null)
                        {
                            var dbConfig = dbCatalog.GetDbConfig(queryDef.Source.DatabaseId);
                            var provider = factory.GetProvider(dbConfig.ProviderType);
                            var layerMetadata = layerCatalog.GetLayerMetadataByTable(queryDef.Source.Schema, queryDef.Source.Table);

                            QueryResult queryResult;
                            try
                            {
                                queryResult = await provider.ExecuteQueryAsync(dbConfig, queryDef, layerMetadata);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Execution on provider {dbConfig.ProviderType} failed, falling back to Sqlite for job {job.JobId}");
                                var sqliteProvider = factory.GetProvider("Sqlite");
                                var sqliteConfig = dbCatalog.GetDefaultDbConfig();
                                queryResult = await sqliteProvider.ExecuteQueryAsync(sqliteConfig, queryDef, layerMetadata);
                            }

                            var exportBytes = await exportEngine.ExportAsync(queryResult, layerMetadata, job.Format, job.IncludeGeometry);

                            var tempPath = Path.Combine(Path.GetTempPath(), $"{job.JobId}.{job.Format.ToLower()}");
                            await File.WriteAllBytesAsync(tempPath, exportBytes, stoppingToken);

                            job.FilePath = tempPath;
                            job.RecordCount = queryResult.TotalCount;
                            job.Status = "COMPLETED";
                            job.CompletedAt = DateTime.UtcNow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed job {job.JobId}");
                    job.Status = "FAILED";
                    job.ErrorMessage = ex.Message;
                }
            }
            else
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
