using System.Data;
using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using Microsoft.Data.SqlClient;

namespace GISDataPlatform.Infrastructure.DatabaseProviders;

public class SqlServerProvider : BaseDatabaseProvider
{
    public override string ProviderType => "SqlServer";

    public override async Task<bool> TestConnectionAsync(DatabaseConnectionConfig config)
    {
        try
        {
            using var conn = new SqlConnection(config.ConnectionString);
            await conn.OpenAsync();
            return conn.State == ConnectionState.Open;
        }
        catch
        {
            return false;
        }
    }

    public override async Task<IEnumerable<string>> GetSchemasAsync(DatabaseConnectionConfig config)
    {
        var schemas = new List<string>();
        using var conn = new SqlConnection(config.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sys.schemas WHERE name NOT IN ('guest', 'INFORMATION_SCHEMA', 'sys', 'db_owner', 'db_accessadmin', 'db_securityadmin', 'db_ddladmin', 'db_datareader', 'db_datawriter', 'db_denydatareader', 'db_denydatawriter') ORDER BY name";
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            schemas.Add(reader.GetString(0));
        }
        return schemas;
    }

    public override async Task<IEnumerable<TableMetadataDto>> GetTablesAsync(DatabaseConnectionConfig config, string schema)
    {
        var tables = new List<TableMetadataDto>();
        using var conn = new SqlConnection(config.ConnectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT t.name AS table_name,
                   c.name AS geom_col,
                   type_name(c.user_type_id) AS geom_type
            FROM sys.tables t
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            LEFT JOIN sys.columns c ON t.object_id = c.object_id AND type_name(c.user_type_id) IN ('geometry', 'geography')
            WHERE s.name = @schema
            ORDER BY t.name";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var geomCol = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var geomType = reader.IsDBNull(2) ? (string.IsNullOrEmpty(geomCol) ? "" : "GEOMETRY") : reader.GetString(2);

            tables.Add(new TableMetadataDto
            {
                Schema = schema,
                Name = tableName,
                GeometryColumn = geomCol,
                GeometryType = geomType,
                SRID = 4326
            });
        }

        return tables;
    }

    public override async Task<IEnumerable<ColumnMetadata>> GetColumnsAsync(DatabaseConnectionConfig config, string schema, string table)
    {
        var columns = new List<ColumnMetadata>();
        using var conn = new SqlConnection(config.ConnectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT c.name, type_name(c.user_type_id) AS data_type
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
            WHERE s.name = @schema AND t.name = @table
            ORDER BY c.column_id";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            bool isGeom = dataType.Equals("geometry", StringComparison.OrdinalIgnoreCase) || dataType.Equals("geography", StringComparison.OrdinalIgnoreCase);

            columns.Add(new ColumnMetadata
            {
                Name = colName,
                DataType = dataType,
                IsGeometry = isGeom
            });
        }

        return columns;
    }

    public override string BuildSpatialPredicate(SpatialQueryDefinition spatial, string geomColumn, Dictionary<string, object> parameters, int sourceSrid)
    {
        string pWkt = "@p_spatial_wkt";
        parameters[pWkt] = spatial.TargetGeometryWkt;
        int targetSrid = spatial.SRID == 0 ? (sourceSrid == 0 ? 4326 : sourceSrid) : spatial.SRID;

        string targetGeomSql = $"geometry::STGeomFromText({pWkt}, {targetSrid})";

        var op = spatial.Operation.ToUpperInvariant();
        switch (op)
        {
            case "INTERSECTS":
                return $"{geomColumn}.STIntersects({targetGeomSql}) = 1";
            case "WITHIN":
                return $"{geomColumn}.STWithin({targetGeomSql}) = 1";
            case "CONTAINS":
                return $"{geomColumn}.STContains({targetGeomSql}) = 1";
            case "OVERLAPS":
                return $"{geomColumn}.STOverlaps({targetGeomSql}) = 1";
            case "TOUCHES":
                return $"{geomColumn}.STTouches({targetGeomSql}) = 1";
            case "CROSSES":
                return $"{geomColumn}.STCrosses({targetGeomSql}) = 1";
            case "DISJOINT":
                return $"{geomColumn}.STDisjoint({targetGeomSql}) = 1";
            case "WITHIN_DISTANCE":
                double distMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters["@p_dist"] = distMeters;
                return $"{geomColumn}.STDistance({targetGeomSql}) <= @p_dist";
            case "BUFFER":
                double bufMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters["@p_buf"] = bufMeters;
                return $"{geomColumn}.STIntersects({targetGeomSql}.STBuffer(@p_buf)) = 1";
            default:
                return $"{geomColumn}.STIntersects({targetGeomSql}) = 1";
        }
    }

    public override async Task<QueryResult> ExecuteQueryAsync(DatabaseConnectionConfig config, QueryDefinition query, LayerMetadata? layerMetadata)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new QueryResult();
        var parameters = new Dictionary<string, object>();

        string schema = FormatColumnName(query.Source.Schema);
        string table = FormatColumnName(query.Source.Table);
        string sourceTableRef = string.IsNullOrEmpty(schema) ? $"[{table}]" : $"[{schema}].[{table}]";

        var selectCols = query.Columns.Count > 0
            ? string.Join(", ", query.Columns.Select(c => $"[{FormatColumnName(c)}]"))
            : "*";

        if (layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string geomCol = layerMetadata.GeometryColumn;
            if (query.Columns.Count > 0 && query.Columns.Contains(geomCol, StringComparer.OrdinalIgnoreCase))
            {
                var nonGeomCols = query.Columns.Where(c => !c.Equals(geomCol, StringComparison.OrdinalIgnoreCase)).Select(c => $"[{FormatColumnName(c)}]").ToList();
                nonGeomCols.Add($"[{geomCol}].STAsText() AS [{geomCol}_WKT]");
                selectCols = string.Join(", ", nonGeomCols);
            }
            else if (query.Columns.Count == 0)
            {
                selectCols = $"*, [{geomCol}].STAsText() AS [{geomCol}_WKT]";
            }
        }

        var whereClauses = new List<string>();

        int paramIdx = 0;
        foreach (var filter in query.Filters)
        {
            string pName = $"@p_filter_{paramIdx++}";
            parameters[pName] = filter.Value ?? DBNull.Value;
            whereClauses.Add($"[{FormatColumnName(filter.Column)}] {filter.Operator} {pName}");
        }

        if (query.Spatial != null && layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string spatialWhere = BuildSpatialPredicate(query.Spatial, $"[{layerMetadata.GeometryColumn}]", parameters, layerMetadata.SRID);
            whereClauses.Add(spatialWhere);
        }

        string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        Func<string, string> quote = s => $"[{s}]";
        string joinsSql = BuildJoinsSql(query.Joins, quote);
        string groupBySql = BuildGroupBySql(query.GroupBy, quote);
        string orderBySql = BuildOrderBySql(query.OrderBy, quote);

        string joinsClause = string.IsNullOrEmpty(joinsSql) ? "" : " " + joinsSql;
        string groupByClause = string.IsNullOrEmpty(groupBySql) ? "" : " " + groupBySql;
        string orderByClause = string.IsNullOrEmpty(orderBySql) ? "" : " " + orderBySql;

        string topClause = query.Limit.HasValue ? $"TOP ({query.Limit.Value}) " : "";

        string sql = $"SELECT {topClause}{selectCols} FROM {sourceTableRef}{joinsClause} {whereSql}{groupByClause}{orderByClause}";

        result.GeneratedSql = sql;

        using var conn = new SqlConnection(config.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        foreach (var kvp in parameters)
        {
            cmd.Parameters.AddWithValue(kvp.Key, kvp.Value);
        }

        using var reader = await cmd.ExecuteReaderAsync();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
        }

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            result.Rows.Add(row);
        }

        sw.Stop();
        result.ExecutionTimeMs = sw.ElapsedMilliseconds;
        result.TotalCount = result.Rows.Count;
        return result;
    }
}
