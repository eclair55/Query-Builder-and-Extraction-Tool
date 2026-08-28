using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;

namespace GISDataPlatform.Infrastructure.Services;

public class DatabaseProviderFactory
{
    private readonly IEnumerable<IDatabaseProvider> _providers;

    public DatabaseProviderFactory(IEnumerable<IDatabaseProvider> providers)
    {
        _providers = providers;
    }

    public IDatabaseProvider GetProvider(string providerType)
    {
        var provider = _providers.FirstOrDefault(p => p.ProviderType.Equals(providerType, StringComparison.OrdinalIgnoreCase));
        if (provider == null)
        {
            throw new NotSupportedException($"Database provider type '{providerType}' is not supported.");
        }
        return provider;
    }
}

public class QueryValidator : IQueryValidator
{
    private readonly ILayerPermissionService _permissionService;

    public QueryValidator(ILayerPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public Task ValidateQueryAsync(QueryDefinition query, Guid userId)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));
        if (query.Source == null) throw new ArgumentException("Query source table must be specified.");

        // Validate table identifier format
        ValidateIdentifier(query.Source.Schema, "Schema");
        ValidateIdentifier(query.Source.Table, "Table");

        // Validate column identifier formats
        foreach (var col in query.Columns)
        {
            ValidateIdentifier(col, "Column");
        }

        // Validate filter operators and columns
        var allowedOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "=", "!=", "<>", ">", "<", ">=", "<=", "LIKE", "IN", "IS NULL", "IS NOT NULL"
        };

        foreach (var filter in query.Filters)
        {
            ValidateIdentifier(filter.Column, "Filter Column");
            if (!allowedOperators.Contains(filter.Operator.Trim()))
            {
                throw new InvalidOperationException($"Operator '{filter.Operator}' is not allowed.");
            }
        }

        return Task.CompletedTask;
    }

    private void ValidateIdentifier(string name, string label)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_\.]+$"))
        {
            throw new ArgumentException($"Invalid characters in {label}: {name}");
        }
    }
}

public class LayerPermissionService : ILayerPermissionService
{
    private readonly List<LayerPermission> _permissions = new();

    public LayerPermissionService()
    {
        // Seed default permissions for demo / dev
        _permissions.Add(new LayerPermission
        {
            RoleId = Guid.Empty,
            LayerId = Guid.Empty,
            CanView = true,
            CanQuery = true,
            CanExtract = true,
            CanSpatialAnalysis = true,
            CanDownload = true
        });
    }

    public Task<bool> HasPermissionAsync(Guid userId, Guid layerId, string action)
    {
        // Admins/Devs have all permissions by default
        return Task.FromResult(true);
    }

    public Task<IEnumerable<LayerPermission>> GetUserLayerPermissionsAsync(Guid userId)
    {
        return Task.FromResult<IEnumerable<LayerPermission>>(_permissions);
    }
}

public class DatabaseCatalog : IDatabaseCatalog
{
    private readonly List<DatabaseConnectionConfig> _dbConfigs = new()
    {
        new DatabaseConnectionConfig
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Oracle Telecom GIS",
            ProviderType = "Oracle",
            ConnectionString = "DATA SOURCE=localhost:1521/XEPDB1;USER ID=PPGIS;PASSWORD=PPGIS",
            Schema = "PPGIS"
        },
        new DatabaseConnectionConfig
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "PostgreSQL PostGIS",
            ProviderType = "PostgreSQL",
            ConnectionString = "Host=localhost;Database=gisdb;Username=postgres;Password=postgres",
            Schema = "public"
        },
        new DatabaseConnectionConfig
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "SQLite Memory DB",
            ProviderType = "Sqlite",
            ConnectionString = "Data Source=:memory:",
            Schema = "main"
        }
    };

    public DatabaseConnectionConfig GetDbConfig(Guid dbId) =>
        _dbConfigs.FirstOrDefault(d => d.Id == dbId) ?? GetDefaultDbConfig();

    public DatabaseConnectionConfig GetDefaultDbConfig() =>
        new DatabaseConnectionConfig { ProviderType = "Sqlite", ConnectionString = "Data Source=:memory:", Schema = "main" };

    public IEnumerable<DatabaseConnectionConfig> GetAllDbConfigs() => _dbConfigs;

    public void AddDbConfig(DatabaseConnectionConfig config) => _dbConfigs.Add(config);
}

public class LayerCatalog : ILayerCatalog
{
    private readonly List<LayerMetadata> _layers = new()
    {
        new LayerMetadata
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            DatabaseId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SchemaName = "PPGIS",
            TableName = "ODN_CONT_GEOM",
            LayerName = "ODN Facility Points (OLT/NAP)",
            Title = "OLT & NAP Facilities",
            Abstract = "ODN Optical Network Equipment Facilities",
            GeometryColumn = "GEOM",
            GeometryType = "Point",
            SRID = 4326,
            GeoServerWorkspace = "PPGIS",
            GeoServerLayerName = "ODN_CONT_GEOM"
        },
        new LayerMetadata
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            DatabaseId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SchemaName = "PPGIS",
            TableName = "FIB_CABLE_SHEATH_GEOM",
            LayerName = "Fiber Cable Sheaths",
            Title = "Fiber Optic Lines",
            Abstract = "Fiber Optic Cable Infrastructure",
            GeometryColumn = "GEOM",
            GeometryType = "LineString",
            SRID = 4326,
            GeoServerWorkspace = "PPGIS",
            GeoServerLayerName = "FIB_CABLE_SHEATH_GEOM"
        },
        new LayerMetadata
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            DatabaseId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            SchemaName = "PPGIS",
            TableName = "LOTS_GEOM",
            LayerName = "Cadastral Parcels",
            Title = "Land Parcels",
            Abstract = "Land Lot Boundaries",
            GeometryColumn = "GEOM",
            GeometryType = "Polygon",
            SRID = 4326,
            GeoServerWorkspace = "PPGIS",
            GeoServerLayerName = "LOTS_GEOM"
        }
    };

    public IEnumerable<LayerMetadata> GetAllLayers() => _layers;

    public LayerMetadata? GetLayerMetadataByTable(string? schema, string? table) =>
        _layers.FirstOrDefault(l => string.Equals(l.TableName, table, StringComparison.OrdinalIgnoreCase));
}
