using GISDataPlatform.Domain.Models;

namespace GISDataPlatform.Application.Interfaces;

public interface IExportEngine
{
    Task<byte[]> ExportAsync(QueryResult queryResult, LayerMetadata? layerMetadata, string format, string geometryMode = "WKT", string targetCrs = "EPSG:4326");
}
