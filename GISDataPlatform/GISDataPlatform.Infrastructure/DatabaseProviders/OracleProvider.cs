using System.Data;

using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using Oracle.ManagedDataAccess.Client;

namespace GISDataPlatform.Infrastructure.DatabaseProviders;

public class OracleProvider : BaseDatabaseProvider
{
    public override string ProviderType => "Oracle";

    public override async Task<bool> TestConnectionAsync(DatabaseConnectionConfig config)
    {
        try
        {
            using var conn = new OracleConnection(config.ConnectionString);
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
        using var conn = new OracleConnection(config.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = "SELECT username FROM all_users ORDER BY username";
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
        using var conn = new OracleConnection(config.ConnectionString);
        await conn.OpenAsync();

        // Query all_tables & SDO_GEOM_METADATA
        string sql = @"
            SELECT t.table_name,
                   m.column_name as geom_col,
                   m.srid
            FROM all_tables t
            LEFT JOIN all_sdo_geom_metadata m
              ON t.owner = m.owner AND t.table_name = m.table_name
            WHERE t.owner = :schema
            ORDER BY t.table_name";

        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("schema", schema.ToUpperInvariant()));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var geomCol = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            int srid = 4326;
            if (!reader.IsDBNull(2))
            {
                int.TryParse(reader.GetValue(2).ToString(), out srid);
            }

            tables.Add(new TableMetadataDto
            {
                Schema = schema,
                Name = tableName,
                GeometryColumn = geomCol,
                GeometryType = string.IsNullOrEmpty(geomCol) ? "" : "GEOMETRY",
                SRID = srid == 0 ? 4326 : srid
            });
        }

        return tables;
    }

    public override async Task<IEnumerable<ColumnMetadata>> GetColumnsAsync(DatabaseConnectionConfig config, string schema, string table)
    {
        var columns = new List<ColumnMetadata>();
        using var conn = new OracleConnection(config.ConnectionString);
        await conn.OpenAsync();

        string sql = @"
            SELECT column_name, data_type
            FROM all_tab_columns
            WHERE owner = :schema AND table_name = :table_name
            ORDER BY column_id";

        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = sql;
        cmd.Parameters.Add(new OracleParameter("schema", schema.ToUpperInvariant()));
        cmd.Parameters.Add(new OracleParameter("table_name", table.ToUpperInvariant()));

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var colName = reader.GetString(0);
            var dataType = reader.GetString(1);
            columns.Add(new ColumnMetadata
            {
                Name = colName,
                DataType = dataType,
                IsGeometry = dataType.Contains("SDO_GEOMETRY", StringComparison.OrdinalIgnoreCase)
            });
        }

        return columns;
    }

    public override string BuildSpatialPredicate(SpatialQueryDefinition spatial, string geomColumn, Dictionary<string, object> parameters, int sourceSrid)
    {
        string paramWkt = ":p_spatial_wkt";
        parameters[paramWkt] = spatial.TargetGeometryWkt;
        int targetSrid = spatial.SRID == 0 ? (sourceSrid == 0 ? 4326 : sourceSrid) : spatial.SRID;

        // Oracle SDO_GEOMETRY construction from WKT: SDO_GEOMETRY(:p_spatial_wkt, srid)
        string targetGeomSql = $"SDO_UTIL.FROM_WKTGEOMETRY({paramWkt})";

        var op = spatial.Operation.ToUpperInvariant();
        switch (op)
        {
            case "INTERSECTS":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=ANYINTERACT') = 'TRUE'";
            case "WITHIN":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=INSIDE+COVEREDBY') = 'TRUE'";
            case "CONTAINS":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=CONTAINS+COVERS') = 'TRUE'";
            case "OVERLAPS":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=OVERLAPBDYDISJOINT+OVERLAPBDYINTERSECT') = 'TRUE'";
            case "TOUCHES":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=TOUCH') = 'TRUE'";
            case "CROSSES":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=ANYINTERACT') = 'TRUE'";
            case "DISJOINT":
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=ANYINTERACT') = 'FALSE'";
            case "WITHIN_DISTANCE":
                double distMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters[":p_dist"] = distMeters;
                return $"SDO_WITHIN_DISTANCE({geomColumn}, {targetGeomSql}, 'distance=' || :p_dist || ' unit=M') = 'TRUE'";
            case "BUFFER":
                double bufMeters = ConvertToMeters(spatial.Distance ?? 0, spatial.Unit);
                parameters[":p_buf"] = bufMeters;
                return $"SDO_RELATE({geomColumn}, SDO_GEOM.SDO_BUFFER({targetGeomSql}, :p_buf, 0.05), 'mask=ANYINTERACT') = 'TRUE'";
            default:
                return $"SDO_RELATE({geomColumn}, {targetGeomSql}, 'mask=ANYINTERACT') = 'TRUE'";
        }
    }

    public override async Task<QueryResult> ExecuteQueryAsync(DatabaseConnectionConfig config, QueryDefinition query, LayerMetadata? layerMetadata)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = new QueryResult();
        var parameters = new Dictionary<string, object>();

        string schema = FormatColumnName(query.Source.Schema);
        string table = FormatColumnName(query.Source.Table);
        string sourceTableRef = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";

        var selectCols = query.Columns.Count > 0
            ? string.Join(", ", query.Columns.Select(c => FormatColumnName(c)))
            : "*";

        // Convert SDO_GEOMETRY to WKT if geometry column is selected or if * is used
        if (layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string geomCol = layerMetadata.GeometryColumn;
            if (query.Columns.Count > 0 && query.Columns.Contains(geomCol, StringComparer.OrdinalIgnoreCase))
            {
                var nonGeomCols = query.Columns.Where(c => !c.Equals(geomCol, StringComparison.OrdinalIgnoreCase)).Select(FormatColumnName).ToList();
                nonGeomCols.Add($"SDO_UTIL.TO_WKTGEOMETRY({geomCol}) AS {geomCol}_WKT");
                selectCols = string.Join(", ", nonGeomCols);
            }
            else if (query.Columns.Count == 0)
            {
                selectCols = $"*, SDO_UTIL.TO_WKTGEOMETRY({geomCol}) AS {geomCol}_WKT";
            }
        }

        var whereClauses = new List<string>();

        // Filters
        int paramIdx = 0;
        foreach (var filter in query.Filters)
        {
            string pName = $":p_filter_{paramIdx++}";
            parameters[pName] = filter.Value ?? DBNull.Value;
            whereClauses.Add($"{FormatColumnName(filter.Column)} {filter.Operator} {pName}");
        }

        // Spatial Predicate
        if (query.Spatial != null && layerMetadata != null && !string.IsNullOrEmpty(layerMetadata.GeometryColumn))
        {
            string spatialWhere = BuildSpatialPredicate(query.Spatial, layerMetadata.GeometryColumn, parameters, layerMetadata.SRID);
            whereClauses.Add(spatialWhere);
        }

        string whereSql = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        Func<string, string> quote = s => s;
        string joinsSql = BuildJoinsSql(query.Joins, quote);
        string groupBySql = BuildGroupBySql(query.GroupBy, quote);
        string orderBySql = BuildOrderBySql(query.OrderBy, quote);

        string joinsClause = string.IsNullOrEmpty(joinsSql) ? "" : " " + joinsSql;
        string groupByClause = string.IsNullOrEmpty(groupBySql) ? "" : " " + groupBySql;
        string orderByClause = string.IsNullOrEmpty(orderBySql) ? "" : " " + orderBySql;

        string sql = $"SELECT {selectCols} FROM {sourceTableRef}{joinsClause} {whereSql}{groupByClause}{orderByClause}";

        if (query.Limit.HasValue)
        {
            sql += $" FETCH FIRST {query.Limit.Value} ROWS ONLY";
        }

        result.GeneratedSql = sql;

        using var conn = new OracleConnection(config.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText = sql;

        foreach (var kvp in parameters)
        {
            cmd.Parameters.Add(new OracleParameter(kvp.Key, kvp.Value));
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
