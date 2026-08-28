using GISDataPlatform.Domain.Models;
using GISDataPlatform.Infrastructure.DatabaseProviders;
using GISDataPlatform.Infrastructure.Export;
using GISDataPlatform.Infrastructure.Services;
using Xunit;

namespace GISDataPlatform.Tests;

public class GISPlatformTests
{
    [Fact]
    public void OracleProvider_BuildsCorrectSpatialPredicateForIntersects()
    {
        var provider = new OracleProvider();
        var spatial = new SpatialQueryDefinition
        {
            Operation = "INTERSECTS",
            TargetGeometryWkt = "POLYGON((0 0, 0 10, 10 10, 10 0, 0 0))",
            SRID = 4326
        };
        var paramsDict = new Dictionary<string, object>();

        string predicate = provider.BuildSpatialPredicate(spatial, "GEOM", paramsDict, 4326);

        Assert.Contains("SDO_RELATE", predicate);
        Assert.Contains("mask=ANYINTERACT", predicate);
        Assert.True(paramsDict.ContainsKey(":p_spatial_wkt"));
    }

    [Fact]
    public void OracleProvider_BuildsCorrectSpatialPredicateForWithinDistance()
    {
        var provider = new OracleProvider();
        var spatial = new SpatialQueryDefinition
        {
            Operation = "WITHIN_DISTANCE",
            TargetGeometryWkt = "POINT(121.0 14.5)",
            Distance = 500,
            Unit = "meters",
            SRID = 4326
        };
        var paramsDict = new Dictionary<string, object>();

        string predicate = provider.BuildSpatialPredicate(spatial, "GEOM", paramsDict, 4326);

        Assert.Contains("SDO_WITHIN_DISTANCE", predicate);
        Assert.Contains("unit=M", predicate);
        Assert.True(paramsDict.ContainsKey(":p_dist"));
        Assert.Equal(500.0, paramsDict[":p_dist"]);
    }

    [Fact]
    public void PostgreSqlProvider_BuildsCorrectSpatialPredicate()
    {
        var provider = new PostgreSqlProvider();
        var spatial = new SpatialQueryDefinition
        {
            Operation = "INTERSECTS",
            TargetGeometryWkt = "POLYGON((0 0, 0 10, 10 10, 10 0, 0 0))",
            SRID = 4326
        };
        var paramsDict = new Dictionary<string, object>();

        string predicate = provider.BuildSpatialPredicate(spatial, "geom", paramsDict, 4326);

        Assert.Contains("ST_Intersects", predicate);
        Assert.Contains("ST_GeomFromText", predicate);
    }

    [Fact]
    public async Task ExportEngine_GeneratesValidGeoJsonBytes()
    {
        var exportEngine = new ExportEngine();
        var queryResult = new QueryResult
        {
            Columns = new List<string> { "ID", "NAME", "LATITUDE", "LONGITUDE" },
            Rows = new List<Dictionary<string, object?>>
            {
                new() { { "ID", "1" }, { "NAME", "OLT Site 1" }, { "LATITUDE", 14.55 }, { "LONGITUDE", 121.02 } }
            }
        };

        var bytes = await exportEngine.ExportAsync(queryResult, null, "GeoJSON");

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        string geoJsonStr = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("FeatureCollection", geoJsonStr);
        Assert.Contains("OLT Site 1", geoJsonStr);
    }

    [Fact]
    public async Task QueryValidator_RejectsInvalidSQLIdentifiers()
    {
        var validator = new QueryValidator(new LayerPermissionService());
        var query = new QueryDefinition
        {
            Source = new TableSource
            {
                Schema = "PPGIS",
                Table = "ODN_CONT_GEOM; DROP TABLE USERS;"
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => validator.ValidateQueryAsync(query, Guid.Empty));
    }
}
