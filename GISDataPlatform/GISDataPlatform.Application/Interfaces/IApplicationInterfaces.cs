using GISDataPlatform.Domain.Models;

namespace GISDataPlatform.Application.Interfaces;

public interface IDatabaseProvider
{
    string ProviderType { get; }
    Task<bool> TestConnectionAsync(DatabaseConnectionConfig config);
    Task<IEnumerable<string>> GetSchemasAsync(DatabaseConnectionConfig config);
    Task<IEnumerable<TableMetadataDto>> GetTablesAsync(DatabaseConnectionConfig config, string schema);
    Task<IEnumerable<ColumnMetadata>> GetColumnsAsync(DatabaseConnectionConfig config, string schema, string table);
    Task<QueryResult> ExecuteQueryAsync(DatabaseConnectionConfig config, QueryDefinition query, LayerMetadata? layerMetadata);
    string BuildSpatialPredicate(SpatialQueryDefinition spatial, string geomColumn, Dictionary<string, object> parameters, int sourceSrid);
}

public class TableMetadataDto
{
    public string Schema { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PrimaryKeyColumn { get; set; } = string.Empty;
    public string GeometryColumn { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty;
    public int SRID { get; set; } = 4326;
}

public interface IQueryValidator
{
    Task ValidateQueryAsync(QueryDefinition query, Guid userId);
}

public interface ILayerPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, Guid layerId, string action); // VIEW, QUERY, EXTRACT, SPATIAL_ANALYSIS, DOWNLOAD
    Task<IEnumerable<LayerPermission>> GetUserLayerPermissionsAsync(Guid userId);
}

public interface IDatabaseCatalog
{
    DatabaseConnectionConfig GetDbConfig(Guid dbId);
    DatabaseConnectionConfig GetDefaultDbConfig();
    IEnumerable<DatabaseConnectionConfig> GetAllDbConfigs();
    void AddDbConfig(DatabaseConnectionConfig config);
}

public interface ILayerCatalog
{
    IEnumerable<LayerMetadata> GetAllLayers();
    LayerMetadata? GetLayerMetadataByTable(string? schema, string? table);
}

public interface IGeoServerService
{
    Task<bool> TestConnectionAsync(GeoServerConfig config);
    Task<IEnumerable<string>> GetWorkspacesAsync(GeoServerConfig config);
    Task<IEnumerable<GeoServerLayerDto>> DiscoverLayersAsync(GeoServerConfig config, string? workspace = null);
}

public class GeoServerLayerDto
{
    public string Workspace { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string Crs { get; set; } = string.Empty;
    public string WmsUrl { get; set; } = string.Empty;
    public string WfsUrl { get; set; } = string.Empty;
    public double[]? BoundingBox { get; set; }
}
