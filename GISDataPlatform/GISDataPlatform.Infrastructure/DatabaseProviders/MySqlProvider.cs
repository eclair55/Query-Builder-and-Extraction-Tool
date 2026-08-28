using System.Data;
using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using MySqlConnector;

namespace GISDataPlatform.Infrastructure.DatabaseProviders;

public class MySqlProvider : BaseDatabaseProvider
{
    public override string ProviderType => "MySQL";

    public override async Task<bool> TestConnectionAsync(DatabaseConnectionConfig config)
    {
        try
        {
            using var conn = new MySqlConnection(config.ConnectionString);
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
        using var conn = new MySqlConnection(config.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('information_schema', 'mysql', 'performance_schema', 'sys') ORDER BY schema_name";
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
        using var conn = new MySqlConnection(config.ConnectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT t.table_name, c.column_name, c.data_type
            FROM information_schema.tables t
            LEFT JOIN information_schema.columns c ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND c.data_type IN ('geometry', 'point', 'linestring', 'polygon', 'multipoint', 'multilinestring', 'multipolygon', 'geometrycollection')
            WHERE t.table_schema = @schema AND t.table_type = 'BASE TABLE'
            ORDER BY t.table_name";

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
        using var conn = new MySqlConnection(config.ConnectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position";

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@table", table);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            bool isGeom = new[] { "geometry", "point", "linestring", "polygon", "multipoint", "multilinestring", "multipolygon" }.Contains(dataType.ToLower());

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

        string targetGeomSql = $"ST_GeomFromText({pWkt}, {targetSrid})";

        var op = spatial.Operation.ToUpperInvariant();
        switch (op)
        {
            case "INTERSECTS":
                return $"ST_Intersects({geomColumn}, {targetGeomSql})";
            case "WITHIN":
                return $"ST_Within({geomColumn}, {targetGeomSql})";
            case "CONTAINS":
                return $"ST_Contains({geomColumn}, {targetGeomSql})";
            case "OVERLAPS":
                return $"ST_Overlaps({geomColumn}, {targetGeomSql})";
            case "TOUCHES":
                return $"ST_Touches({geomColumn}, {targetGeomSql})";
            case "CROSSES":
                return $"ST_Crosses({geomColumn}, {targetGeomSql})";
            case "DISJOINT":
                return $"ST_Disjoint({geomColumn}, {targetGeomSql})";
            case "WITHIN_DISTANCE":
                double distMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters["@p_dist"] = distMeters;
                return $"ST_Distance({geomColumn}, {targetGeomSql}) <= @p_dist";
            case "BUFFER":
                double bufMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters["@p_buf"] = bufMeters;
                return $"ST_Intersects({geomColumn}, ST_Buffer({targetGeomSql}, @p_buf))";
            default:
                return $"ST_Intersects({geomColumn}, {targetGeomSql})";
        }
    }

    public override async Task<QueryResult> ExecuteQueryAsync(DatabaseConnectionConfig config, QueryDefinition query, LayerMetadata? layerMetadata)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new QueryResult();
        var parameters = new Dictionary<string, object>();

        string schema = FormatColumnName(query.Source.Schema);
        string table = FormatColumnName(query.Source.Table);
        string sourceTableRef = string.IsNullOrEmpty(schema) ? $"`{table}`" : $"`{schema}`.`{table}`";

        var selectCols = query.Columns.Count > 0
            ? string.Join(", ", query.Columns.Select(c => $"`{FormatColumnName(c)}`"))
            : "*";

        if (layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string geomCol = layerMetadata.GeometryColumn;
            if (query.Columns.Count > 0 && query.Columns.Contains(geomCol, StringComparer.OrdinalIgnoreCase))
            {
                var nonGeomCols = query.Columns.Where(c => !c.Equals(geomCol, StringComparison.OrdinalIgnoreCase)).Select(c => $"`{FormatColumnName(c)}`").ToList();
                nonGeomCols.Add($"ST_AsText(`{geomCol}`) AS `{geomCol}_WKT`");
                selectCols = string.Join(", ", nonGeomCols);
            }
            else if (query.Columns.Count == 0)
            {
                selectCols = $"*, ST_AsText(`{geomCol}`) AS `{geomCol}_WKT`";
            }
        }

        var whereClauses = new List<string>();

        int paramIdx = 0;
        foreach (var filter in query.Filters)
        {
            string pName = $"@p_filter_{paramIdx++}";
            parameters[pName] = filter.Value ?? DBNull.Value;
            whereClauses.Add($"`{FormatColumnName(filter.Column)}` {filter.Operator} {pName}");
        }

        if (query.Spatial != null && layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string spatialWhere = BuildSpatialPredicate(query.Spatial, $"`{layerMetadata.GeometryColumn}`", parameters, layerMetadata.SRID);
            whereClauses.Add(spatialWhere);
        }

        string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        Func<string, string> quote = s => $"`{s}`";
        string joinsSql = BuildJoinsSql(query.Joins, quote);
        string groupBySql = BuildGroupBySql(query.GroupBy, quote);
        string orderBySql = BuildOrderBySql(query.OrderBy, quote);

        string joinsClause = string.IsNullOrEmpty(joinsSql) ? "" : " " + joinsSql;
        string groupByClause = string.IsNullOrEmpty(groupBySql) ? "" : " " + groupBySql;
        string orderByClause = string.IsNullOrEmpty(orderBySql) ? "" : " " + orderBySql;

        string sql = $"SELECT {selectCols} FROM {sourceTableRef}{joinsClause} {whereSql}{groupByClause}{orderByClause}";

        if (query.Limit.HasValue)
        {
            sql += $" LIMIT {query.Limit.Value}";
        }

        result.GeneratedSql = sql;

        using var conn = new MySqlConnection(config.ConnectionString);
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
