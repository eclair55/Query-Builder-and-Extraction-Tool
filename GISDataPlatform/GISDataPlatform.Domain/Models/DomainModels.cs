namespace GISDataPlatform.Domain.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public List<Guid> RoleIds { get; set; } = new();
    public bool IsAdmin { get; set; }
}

public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SystemPermissions { get; set; } = new();
}

public static class Permissions
{
    public const string ViewMap = "VIEW_MAP";
    public const string UseQueryBuilder = "USE_QUERY_BUILDER";
    public const string ManageDatabases = "MANAGE_DATABASES";
    public const string ManageGeoServer = "MANAGE_GEOSERVER";
    public const string ManageUsers = "MANAGE_USERS";
    public const string ViewAuditLogs = "VIEW_AUDIT_LOGS";
}

public class LayerPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoleId { get; set; }
    public Guid LayerId { get; set; }
    public bool CanView { get; set; }
    public bool CanQuery { get; set; }
    public bool CanExtract { get; set; }
    public bool CanSpatialAnalysis { get; set; }
    public bool CanDownload { get; set; }
}

public class DatabaseConnectionConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty; // "Oracle", "PostgreSQL", "SqlServer", "MySQL", "Sqlite"
    public string ConnectionString { get; set; } = string.Empty;
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Schema { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool IsActive { get; set; } = true;
}

public class LayerMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatabaseId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string GeometryColumn { get; set; } = string.Empty;
    public string GeometryType { get; set; } = string.Empty; // Point, LineString, Polygon, MultiPolygon
    public int SRID { get; set; } = 4326;
    public bool IsQueryable { get; set; } = true;
    public bool IsExtractable { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public string? GeoServerWorkspace { get; set; }
    public string? GeoServerLayerName { get; set; }
    public List<ColumnMetadata> Columns { get; set; } = new();
}

public class ColumnMetadata
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsGeometry { get; set; }
    public string? Description { get; set; }
}

public class QueryDefinition
{
    public TableSource Source { get; set; } = new();
    public List<string> Columns { get; set; } = new();
    public List<FilterCondition> Filters { get; set; } = new();
    public List<JoinDefinition> Joins { get; set; } = new();
    public SpatialQueryDefinition? Spatial { get; set; }
    public List<OrderDefinition> OrderBy { get; set; } = new();
    public List<string> GroupBy { get; set; } = new();
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public class TableSource
{
    public Guid DatabaseId { get; set; }
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class FilterCondition
{
    public string Column { get; set; } = string.Empty;
    public string Operator { get; set; } = "="; // =, !=, >, <, >=, <=, LIKE, IN, IS NULL, IS NOT NULL
    public object? Value { get; set; }
    public string LogicalOperator { get; set; } = "AND"; // AND, OR
}

public class JoinDefinition
{
    public string JoinType { get; set; } = "INNER"; // INNER, LEFT, RIGHT
    public string Schema { get; set; } = string.Empty;
    public string Table { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string LeftColumn { get; set; } = string.Empty;
    public string RightColumn { get; set; } = string.Empty;
}

public class OrderDefinition
{
    public string Column { get; set; } = string.Empty;
    public bool Descending { get; set; }
}

public class SpatialQueryDefinition
{
    public string Operation { get; set; } = "INTERSECTS"; // INTERSECTS, WITHIN, CONTAINS, OVERLAPS, TOUCHES, CROSSES, DISJOINT, WITHIN_DISTANCE, NEAREST, BUFFER
    public string TargetGeometryWkt { get; set; } = string.Empty;
    public double? Distance { get; set; }
    public string Unit { get; set; } = "meters"; // meters, kilometers, feet, miles
    public int SRID { get; set; } = 4326;
    public int? NearestCount { get; set; }
}

public class QueryResult
{
    public List<string> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public long TotalCount { get; set; }
    public long ExecutionTimeMs { get; set; }
    public bool SpatialIndexUsed { get; set; }
    public string GeneratedSql { get; set; } = string.Empty;
}

public class SavedQuery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string QueryDefinitionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsTemplate { get; set; }
    public string TargetCrs { get; set; } = "EPSG:4326";
}

public class ExtractionJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string QueryDefinitionJson { get; set; } = string.Empty;
    public string Format { get; set; } = "GeoJSON"; // CSV, JSON, GeoJSON, Shapefile, KML, KMZ, GeoPackage
    public string IncludeGeometry { get; set; } = "WKT"; // None, WKT, GeoJSON
    public string Status { get; set; } = "QUEUED"; // QUEUED, PROCESSING, COMPLETED, FAILED
    public long RecordCount { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string QueryDefinitionJson { get; set; } = string.Empty;
    public string SpatialOperation { get; set; } = string.Empty;
    public long RecordCount { get; set; }
    public string ExportFormat { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public string? JobId { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public string? IpAddress { get; set; }
}

public class GeoServerConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
