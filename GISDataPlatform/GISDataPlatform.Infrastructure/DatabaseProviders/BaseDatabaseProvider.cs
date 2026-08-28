using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GISDataPlatform.Infrastructure.DatabaseProviders;

public abstract class BaseDatabaseProvider : IDatabaseProvider
{
    public abstract string ProviderType { get; }

    public abstract Task<bool> TestConnectionAsync(DatabaseConnectionConfig config);
    public abstract Task<IEnumerable<string>> GetSchemasAsync(DatabaseConnectionConfig config);
    public abstract Task<IEnumerable<TableMetadataDto>> GetTablesAsync(DatabaseConnectionConfig config, string schema);
    public abstract Task<IEnumerable<ColumnMetadata>> GetColumnsAsync(DatabaseConnectionConfig config, string schema, string table);
    public abstract Task<QueryResult> ExecuteQueryAsync(DatabaseConnectionConfig config, QueryDefinition query, LayerMetadata? layerMetadata);
    public abstract string BuildSpatialPredicate(SpatialQueryDefinition spatial, string geomColumn, Dictionary<string, object> parameters, int sourceSrid);

    protected string FormatColumnName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Sanitize column names/identifiers to prevent injection
        if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9_\.\*]+$"))
        {
            return name;
        }
        throw new ArgumentException($"Invalid column or identifier: {name}");
    }

    protected string BuildJoinsSql(List<JoinDefinition> joins, Func<string, string> quoteFunc)
    {
        if (joins == null || joins.Count == 0) return string.Empty;
        var joinClauses = new List<string>();
        foreach (var join in joins)
        {
            string joinType = join.JoinType?.ToUpperInvariant() switch
            {
                "LEFT" => "LEFT JOIN",
                "RIGHT" => "RIGHT JOIN",
                "FULL" => "FULL JOIN",
                _ => "INNER JOIN"
            };

            string joinSchema = FormatColumnName(join.Schema);
            string joinTable = FormatColumnName(join.Table);
            string tableRef = string.IsNullOrEmpty(joinSchema)
                ? quoteFunc(joinTable)
                : $"{quoteFunc(joinSchema)}.{quoteFunc(joinTable)}";

            if (!string.IsNullOrWhiteSpace(join.Alias))
            {
                tableRef += $" AS {quoteFunc(FormatColumnName(join.Alias))}";
            }

            string leftCol = FormatColumnName(join.LeftColumn);
            string rightCol = FormatColumnName(join.RightColumn);

            string onClause = $"{QuoteColRef(leftCol, quoteFunc)} = {QuoteColRef(rightCol, quoteFunc)}";
            joinClauses.Add($"{joinType} {tableRef} ON {onClause}");
        }
        return string.Join(" ", joinClauses);
    }

    protected string BuildGroupBySql(List<string> groupBy, Func<string, string> quoteFunc)
    {
        if (groupBy == null || groupBy.Count == 0) return string.Empty;
        var cols = groupBy.Select(g => QuoteColRef(FormatColumnName(g), quoteFunc));
        return "GROUP BY " + string.Join(", ", cols);
    }

    protected string BuildOrderBySql(List<OrderDefinition> orderBy, Func<string, string> quoteFunc)
    {
        if (orderBy == null || orderBy.Count == 0) return string.Empty;
        var cols = orderBy.Select(o => $"{QuoteColRef(FormatColumnName(o.Column), quoteFunc)} {(o.Descending ? "DESC" : "ASC")}");
        return "ORDER BY " + string.Join(", ", cols);
    }

    private string QuoteColRef(string colName, Func<string, string> quoteFunc)
    {
        if (string.IsNullOrEmpty(colName)) return colName;
        if (colName.Contains('.'))
        {
            var parts = colName.Split('.');
            return string.Join(".", parts.Select(p => p == "*" ? "*" : quoteFunc(p)));
        }
        return colName == "*" ? "*" : quoteFunc(colName);
    }

    protected double ConvertToMeters(double distance, string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "kilometers" or "km" => distance * 1000.0,
            "feet" or "ft" => distance * 0.3048,
            "miles" or "mi" => distance * 1609.344,
            _ => distance // Default meters
        };
    }

    protected Geometry? ParseWkt(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt)) return null;
        var reader = new WKTReader();
        return reader.Read(wkt);
    }
}
