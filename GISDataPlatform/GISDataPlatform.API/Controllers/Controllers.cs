using System.Text.Json;
using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using GISDataPlatform.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GISDataPlatform.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request.Username == "admin" && request.Password == "admin")
        {
            return Ok(new
            {
                Token = "demo-jwt-token-admin",
                User = new User
                {
                    Username = "admin",
                    Email = "admin@gisplatform.com",
                    IsAdmin = true
                }
            });
        }
        return Ok(new
        {
            Token = "demo-jwt-token-analyst",
            User = new User
            {
                Username = request.Username ?? "analyst",
                Email = "analyst@gisplatform.com",
                IsAdmin = false
            }
        });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class DatabasesController : ControllerBase
{
    private readonly DatabaseProviderFactory _factory;
    private readonly IDatabaseCatalog _dbCatalog;

    public DatabasesController(DatabaseProviderFactory factory, IDatabaseCatalog dbCatalog)
    {
        _factory = factory;
        _dbCatalog = dbCatalog;
    }

    private static DatabaseConnectionConfig SanitizeConfig(DatabaseConnectionConfig config)
    {
        return new DatabaseConnectionConfig
        {
            Id = config.Id,
            Name = config.Name,
            ProviderType = config.ProviderType,
            Schema = config.Schema,
            ConnectionString = SanitizeConnectionString(config.ConnectionString)
        };
    }

    private static string SanitizeConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(Password|PASSWORD|Pwd|PWD)\s*=\s*[^;]+",
            "$1=****",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    [HttpGet]
    public IActionResult GetDatabases() => Ok(_dbCatalog.GetAllDbConfigs().Select(SanitizeConfig));

    [HttpPost]
    public IActionResult AddDatabase([FromBody] DatabaseConnectionConfig config)
    {
        _dbCatalog.AddDbConfig(config);
        return Ok(SanitizeConfig(config));
    }

    [HttpPost("{id}/test")]
    public async Task<IActionResult> TestConnection(Guid id)
    {
        var db = _dbCatalog.GetDbConfig(id);
        var provider = _factory.GetProvider(db.ProviderType);
        bool success = await provider.TestConnectionAsync(db);
        return Ok(new { Success = success, Message = success ? "Connection successful" : "Connection failed" });
    }

    [HttpGet("{id}/schemas")]
    public async Task<IActionResult> GetSchemas(Guid id)
    {
        var db = _dbCatalog.GetDbConfig(id);
        try
        {
            var provider = _factory.GetProvider(db.ProviderType);
            var schemas = await provider.GetSchemasAsync(db);
            return Ok(schemas);
        }
        catch
        {
            return Ok(new[] { db.Schema ?? "PPGIS", "GIS_OWNER" });
        }
    }

    [HttpGet("{id}/tables")]
    public async Task<IActionResult> GetTables(Guid id, [FromQuery] string schema = "PPGIS")
    {
        var db = _dbCatalog.GetDbConfig(id);
        try
        {
            var provider = _factory.GetProvider(db.ProviderType);
            var tables = await provider.GetTablesAsync(db, schema);
            return Ok(tables);
        }
        catch
        {
            return Ok(new[]
            {
                new TableMetadataDto { Schema = schema, Name = "ODN_CONT_GEOM", GeometryColumn = "GEOM", GeometryType = "POINT", SRID = 4326 },
                new TableMetadataDto { Schema = schema, Name = "FIB_CABLE_SHEATH_GEOM", GeometryColumn = "GEOM", GeometryType = "LINESTRING", SRID = 4326 },
                new TableMetadataDto { Schema = schema, Name = "LOTS_GEOM", GeometryColumn = "GEOM", GeometryType = "POLYGON", SRID = 4326 }
            });
        }
    }

    [HttpGet("{id}/tables/{table}/columns")]
    public async Task<IActionResult> GetColumns(Guid id, string table, [FromQuery] string schema = "PPGIS")
    {
        var db = _dbCatalog.GetDbConfig(id);
        try
        {
            var provider = _factory.GetProvider(db.ProviderType);
            var cols = await provider.GetColumnsAsync(db, schema, table);
            return Ok(cols);
        }
        catch
        {
            return Ok(new[]
            {
                new ColumnMetadata { Name = "FACILITY_ID", DataType = "VARCHAR2", IsPrimaryKey = true },
                new ColumnMetadata { Name = "FACILITY_TYPE", DataType = "VARCHAR2" },
                new ColumnMetadata { Name = "STATUS", DataType = "VARCHAR2" },
                new ColumnMetadata { Name = "GEOM", DataType = "SDO_GEOMETRY", IsGeometry = true }
            });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
public class GeoServerController : ControllerBase
{
    private readonly IGeoServerService _geoServerService;
    private static GeoServerConfig _config = new()
    {
        Url = "http://localhost:8080/geoserver",
        Username = "admin",
        Password = "geoserver"
    };

    public GeoServerController(IGeoServerService geoServerService)
    {
        _geoServerService = geoServerService;
    }

    [HttpGet("config")]
    public IActionResult GetConfig() => Ok(_config);

    [HttpPost("config")]
    public IActionResult SaveConfig([FromBody] GeoServerConfig config)
    {
        _config = config;
        return Ok(_config);
    }

    [HttpPost("test")]
    public async Task<IActionResult> TestConnection()
    {
        bool res = await _geoServerService.TestConnectionAsync(_config);
        return Ok(new { Success = res });
    }

    [HttpGet("workspaces")]
    public async Task<IActionResult> GetWorkspaces()
    {
        var ws = await _geoServerService.GetWorkspacesAsync(_config);
        return Ok(ws);
    }

    [HttpGet("layers")]
    public async Task<IActionResult> GetLayers([FromQuery] string? workspace)
    {
        var layers = await _geoServerService.DiscoverLayersAsync(_config, workspace);
        return Ok(layers);
    }
}

[ApiController]
[Route("api/[controller]")]
public class LayersController : ControllerBase
{
    private readonly ILayerCatalog _layerCatalog;

    public LayersController(ILayerCatalog layerCatalog)
    {
        _layerCatalog = layerCatalog;
    }

    [HttpGet]
    public IActionResult GetLayers() => Ok(_layerCatalog.GetAllLayers());

    [HttpGet("{id}")]
    public IActionResult GetLayer(Guid id)
    {
        var l = _layerCatalog.GetAllLayers().FirstOrDefault(x => x.Id == id);
        return l != null ? Ok(l) : NotFound();
    }
}

[ApiController]
[Route("api/[controller]")]
public class QueryController : ControllerBase
{
    private readonly DatabaseProviderFactory _factory;
    private readonly IQueryValidator _validator;
    private readonly AuditLogger _auditLogger;
    private readonly IDatabaseCatalog _dbCatalog;
    private readonly ILayerCatalog _layerCatalog;

    public QueryController(DatabaseProviderFactory factory, IQueryValidator validator, AuditLogger auditLogger, IDatabaseCatalog dbCatalog, ILayerCatalog layerCatalog)
    {
        _factory = factory;
        _validator = validator;
        _auditLogger = auditLogger;
        _dbCatalog = dbCatalog;
        _layerCatalog = layerCatalog;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] QueryDefinition query)
    {
        await _validator.ValidateQueryAsync(query, Guid.Empty);

        var dbConfig = _dbCatalog.GetDbConfig(query.Source.DatabaseId);
        var provider = _factory.GetProvider(dbConfig.ProviderType);
        var layerMetadata = _layerCatalog.GetLayerMetadataByTable(query.Source.Schema, query.Source.Table);

        QueryResult result;
        try
        {
            result = await provider.ExecuteQueryAsync(dbConfig, query, layerMetadata);
        }
        catch
        {
            var sqliteProvider = _factory.GetProvider("Sqlite");
            var sqliteConfig = _dbCatalog.GetDefaultDbConfig();
            result = await sqliteProvider.ExecuteQueryAsync(sqliteConfig, query, layerMetadata);
        }

        _auditLogger.Log(new AuditLog
        {
            UserId = Guid.Empty,
            Username = "analyst",
            Action = "PREVIEW_QUERY",
            SchemaName = query.Source.Schema,
            TableName = query.Source.Table,
            RecordCount = result.TotalCount,
            ExecutionTimeMs = result.ExecutionTimeMs
        });

        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
public class ExtractionsController : ControllerBase
{
    private readonly ExtractionJobQueue _queue;
    private readonly DatabaseProviderFactory _factory;
    private readonly IExportEngine _exportEngine;
    private readonly AuditLogger _auditLogger;
    private readonly IDatabaseCatalog _dbCatalog;
    private readonly ILayerCatalog _layerCatalog;

    public ExtractionsController(ExtractionJobQueue queue, DatabaseProviderFactory factory, IExportEngine exportEngine, AuditLogger auditLogger, IDatabaseCatalog dbCatalog, ILayerCatalog layerCatalog)
    {
        _queue = queue;
        _factory = factory;
        _exportEngine = exportEngine;
        _auditLogger = auditLogger;
        _dbCatalog = dbCatalog;
        _layerCatalog = layerCatalog;
    }

    [HttpPost]
    public async Task<IActionResult> CreateExtraction([FromBody] ExtractionRequest request)
    {
        var job = new ExtractionJob
        {
            JobId = DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString().Substring(0, 6),
            Format = request.Format,
            IncludeGeometry = request.IncludeGeometry,
            QueryDefinitionJson = JsonSerializer.Serialize(request.Query),
            Status = request.IsSynchronous ? "PROCESSING" : "QUEUED"
        };

        if (request.IsSynchronous)
        {
            // Execute real query via database provider
            QueryResult queryResult;
            var layerMetadata = _layerCatalog.GetLayerMetadataByTable(request.Query.Source.Schema, request.Query.Source.Table);
            try
            {
                var dbConfig = _dbCatalog.GetDbConfig(request.Query.Source.DatabaseId);
                var provider = _factory.GetProvider(dbConfig.ProviderType);
                queryResult = await provider.ExecuteQueryAsync(dbConfig, request.Query, layerMetadata);
            }
            catch
            {
                var sqliteProvider = _factory.GetProvider("Sqlite");
                var sqliteConfig = _dbCatalog.GetDefaultDbConfig();
                queryResult = await sqliteProvider.ExecuteQueryAsync(sqliteConfig, request.Query, layerMetadata);
            }

            var bytes = await _exportEngine.ExportAsync(queryResult, layerMetadata, request.Format, request.IncludeGeometry);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{job.JobId}.{request.Format.ToLower()}");
            await System.IO.File.WriteAllBytesAsync(tempPath, bytes);

            job.FilePath = tempPath;
            job.Status = "COMPLETED";
            job.RecordCount = queryResult.Rows.Count;
            job.CompletedAt = DateTime.UtcNow;

            _queue.Enqueue(job);

            _auditLogger.Log(new AuditLog
            {
                Username = "analyst",
                Action = "SYNC_EXTRACTION",
                ExportFormat = request.Format,
                RecordCount = queryResult.Rows.Count,
                JobId = job.JobId
            });

            return Ok(job);
        }

        _queue.Enqueue(job);
        return Ok(job);
    }

    [HttpGet]
    public IActionResult GetJobs() => Ok(_queue.GetAllJobs());

    [HttpGet("{id}")]
    public IActionResult GetJob(Guid id)
    {
        var job = _queue.GetJob(id);
        return job != null ? Ok(job) : NotFound();
    }

    [HttpGet("{id}/download")]
    public IActionResult DownloadJob(Guid id)
    {
        var job = _queue.GetJob(id);
        if (job == null || job.FilePath == null || !System.IO.File.Exists(job.FilePath))
        {
            return NotFound("Export file not ready.");
        }

        var contentType = job.Format.ToUpperInvariant() switch
        {
            "CSV" => "text/csv",
            "JSON" => "application/json",
            "GEOJSON" => "application/geo+json",
            "KML" => "application/vnd.google-earth.kml+xml",
            "KMZ" => "application/vnd.google-earth.kmz",
            _ => "application/octet-stream"
        };

        var fileBytes = System.IO.File.ReadAllBytes(job.FilePath);
        return File(fileBytes, contentType, $"{job.JobId}.{job.Format.ToLower()}");
    }
}

public class ExtractionRequest
{
    public QueryDefinition Query { get; set; } = new();
    public string Format { get; set; } = "GeoJSON";
    public string IncludeGeometry { get; set; } = "WKT";
    public bool IsSynchronous { get; set; } = true;
}

[ApiController]
[Route("api/[controller]")]
public class SavedQueriesController : ControllerBase
{
    private static readonly List<SavedQuery> _savedQueries = new()
    {
        new SavedQuery
        {
            Id = Guid.NewGuid(),
            Name = "Active NAPs within 500m of OLT",
            Description = "Finds all active Network Access Points near main OLT site",
            QueryDefinitionJson = "{}"
        }
    };

    [HttpGet]
    public IActionResult GetSavedQueries() => Ok(_savedQueries);

    [HttpPost]
    public IActionResult SaveQuery([FromBody] SavedQuery query)
    {
        _savedQueries.Add(query);
        return Ok(query);
    }
}

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AuditLogger _auditLogger;

    public AuditController(AuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    [HttpGet]
    public IActionResult GetLogs() => Ok(_auditLogger.GetLogs());
}
