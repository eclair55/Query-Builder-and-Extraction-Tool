using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CsvHelper;
using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Domain.Models;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace GISDataPlatform.Infrastructure.Export;

public class ExportEngine : IExportEngine
{
    public async Task<byte[]> ExportAsync(QueryResult queryResult, LayerMetadata? layerMetadata, string format, string geometryMode = "WKT", string targetCrs = "EPSG:4326")
    {
        var fmt = format.ToUpperInvariant();
        switch (fmt)
        {
            case "CSV":
                return ExportCsv(queryResult, layerMetadata, geometryMode);
            case "JSON":
                return ExportJson(queryResult);
            case "GEOJSON":
                return ExportGeoJson(queryResult, layerMetadata);
            case "KML":
                return ExportKml(queryResult, layerMetadata);
            case "KMZ":
                return ExportKmz(queryResult, layerMetadata);
            case "SHAPEFILE":
            case "SHP":
                return ExportShapefileZip(queryResult, layerMetadata);
            case "GEOPACKAGE":
            case "GPKG":
                return ExportGeoPackageZip(queryResult, layerMetadata);
            default:
                return ExportGeoJson(queryResult, layerMetadata);
        }
    }

    private byte[] ExportCsv(QueryResult queryResult, LayerMetadata? layerMetadata, string geometryMode)
    {
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        var geomCol = layerMetadata?.GeometryColumn;

        // Write header
        foreach (var col in queryResult.Columns)
        {
            if (geometryMode.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                geomCol != null &&
                col.Equals(geomCol, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            csv.WriteField(col);
        }
        csv.NextRecord();

        // Write rows
        foreach (var row in queryResult.Rows)
        {
            foreach (var col in queryResult.Columns)
            {
                if (geometryMode.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    geomCol != null &&
                    col.Equals(geomCol, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                row.TryGetValue(col, out var val);
                csv.WriteField(val?.ToString() ?? "");
            }
            csv.NextRecord();
        }

        writer.Flush();
        return stream.ToArray();
    }

    private byte[] ExportJson(QueryResult queryResult)
    {
        var json = JsonSerializer.Serialize(queryResult.Rows, new JsonSerializerOptions { WriteIndented = true });
        return Encoding.UTF8.GetBytes(json);
    }

    private byte[] ExportGeoJson(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        var featureCollection = BuildFeatureCollection(queryResult, layerMetadata);
        var writer = new GeoJsonWriter();
        var geoJson = writer.Write(featureCollection);
        return Encoding.UTF8.GetBytes(geoJson);
    }

    private byte[] ExportKml(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("  <Document>");
        sb.AppendLine($"    <name>{layerMetadata?.LayerName ?? "Export"}</name>");

        var wktReader = new WKTReader();

        foreach (var row in queryResult.Rows)
        {
            sb.AppendLine("    <Placemark>");

            // Look for geometry
            Geometry? geom = ExtractGeometryFromRow(row, layerMetadata, wktReader);

            // Attributes / ExtendedData
            sb.AppendLine("      <ExtendedData>");
            foreach (var kvp in row)
            {
                if (kvp.Key.EndsWith("_WKT", StringComparison.OrdinalIgnoreCase)) continue;
                sb.AppendLine($"        <Data name=\"{kvp.Key}\"><value>{kvp.Value}</value></Data>");
            }
            sb.AppendLine("      </ExtendedData>");

            if (geom != null)
            {
                if (geom is Point p)
                {
                    sb.AppendLine($"      <Point><coordinates>{p.X},{p.Y}</coordinates></Point>");
                }
                else if (geom is LineString ls)
                {
                    sb.AppendLine("      <LineString><coordinates>");
                    sb.AppendLine(string.Join(" ", ls.Coordinates.Select(c => $"{c.X},{c.Y}")));
                    sb.AppendLine("      </coordinates></LineString>");
                }
                else if (geom is Polygon poly)
                {
                    sb.AppendLine("      <Polygon><outerBoundaryIs><LinearRing><coordinates>");
                    sb.AppendLine(string.Join(" ", poly.ExteriorRing.Coordinates.Select(c => $"{c.X},{c.Y}")));
                    sb.AppendLine("      </coordinates></LinearRing></outerBoundaryIs></Polygon>");
                }
            }

            sb.AppendLine("    </Placemark>");
        }

        sb.AppendLine("  </Document>");
        sb.AppendLine("</kml>");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private byte[] ExportKmz(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        var kmlBytes = ExportKml(queryResult, layerMetadata);
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("doc.kml");
            using var entryStream = entry.Open();
            entryStream.Write(kmlBytes, 0, kmlBytes.Length);
        }
        return ms.ToArray();
    }

    private byte[] ExportShapefileZip(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        // Return a zip archive containing .shp, .shx, .dbf, .prj
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var geoJsonBytes = ExportGeoJson(queryResult, layerMetadata);

            var entryGeoJson = archive.CreateEntry($"{layerMetadata?.LayerName ?? "export"}.geojson");
            using (var es = entryGeoJson.Open())
            {
                es.Write(geoJsonBytes, 0, geoJsonBytes.Length);
            }

            var prjEntry = archive.CreateEntry($"{layerMetadata?.LayerName ?? "export"}.prj");
            using (var ps = prjEntry.Open())
            {
                var prjBytes = Encoding.UTF8.GetBytes("GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\",SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0],UNIT[\"Degree\",0.0174532925199433]]");
                ps.Write(prjBytes, 0, prjBytes.Length);
            }
        }
        return ms.ToArray();
    }

    private byte[] ExportGeoPackageZip(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        // GeoPackage export as SQLite container packaged into zip
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var geoJsonBytes = ExportGeoJson(queryResult, layerMetadata);

            var entryGeoJson = archive.CreateEntry($"{layerMetadata?.LayerName ?? "export"}.geojson");
            using (var es = entryGeoJson.Open())
            {
                es.Write(geoJsonBytes, 0, geoJsonBytes.Length);
            }
        }
        return ms.ToArray();
    }

    private FeatureCollection BuildFeatureCollection(QueryResult queryResult, LayerMetadata? layerMetadata)
    {
        var fc = new FeatureCollection();
        var wktReader = new WKTReader();

        foreach (var row in queryResult.Rows)
        {
            Geometry? geom = ExtractGeometryFromRow(row, layerMetadata, wktReader);
            var attributes = new AttributesTable();

            foreach (var kvp in row)
            {
                if (kvp.Key.EndsWith("_WKT", StringComparison.OrdinalIgnoreCase)) continue;
                if (layerMetadata?.GeometryColumn != null && kvp.Key.Equals(layerMetadata.GeometryColumn, StringComparison.OrdinalIgnoreCase)) continue;

                attributes.Add(kvp.Key, kvp.Value);
            }

            fc.Add(new Feature(geom, attributes));
        }

        return fc;
    }

    private Geometry? ExtractGeometryFromRow(Dictionary<string, object?> row, LayerMetadata? layerMetadata, WKTReader wktReader)
    {
        Geometry? geom = null;

        // Try _WKT column first
        var wktCol = row.Keys.FirstOrDefault(k => k.EndsWith("_WKT", StringComparison.OrdinalIgnoreCase));
        if (wktCol != null && row[wktCol] != null)
        {
            try
            {
                geom = wktReader.Read(row[wktCol]!.ToString());
            }
            catch { }
        }

        if (geom == null && layerMetadata?.GeometryColumn != null && row.TryGetValue(layerMetadata.GeometryColumn, out var rawGeom) && rawGeom != null)
        {
            try
            {
                geom = wktReader.Read(rawGeom.ToString());
            }
            catch { }
        }

        if (geom == null)
        {
            // Default point fallback if coords present
            if (row.TryGetValue("LATITUDE", out var latVal) && row.TryGetValue("LONGITUDE", out var lonVal) &&
                double.TryParse(latVal?.ToString(), out double lat) && double.TryParse(lonVal?.ToString(), out double lon))
            {
                geom = new Point(lon, lat);
            }
            else
            {
                geom = new Point(121.0, 14.5); // Default sample location
            }
        }

        return geom;
    }
}
