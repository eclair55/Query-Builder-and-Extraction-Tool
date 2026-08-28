using GISDataPlatform.Application.Interfaces;
using GISDataPlatform.Infrastructure.DatabaseProviders;
using GISDataPlatform.Infrastructure.Export;
using GISDataPlatform.Infrastructure.GeoServer;
using GISDataPlatform.Infrastructure.Services;
using GISDataPlatform.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HTTP Client for GeoServer
builder.Services.AddHttpClient<IGeoServerService, GeoServerService>();

// Register Database Providers
builder.Services.AddSingleton<IDatabaseProvider, OracleProvider>();
builder.Services.AddSingleton<IDatabaseProvider, PostgreSqlProvider>();
builder.Services.AddSingleton<IDatabaseProvider, SqlServerProvider>();
builder.Services.AddSingleton<IDatabaseProvider, MySqlProvider>();
builder.Services.AddSingleton<IDatabaseProvider, SqliteProvider>();
builder.Services.AddSingleton<DatabaseProviderFactory>();

// Register Catalogs
builder.Services.AddSingleton<IDatabaseCatalog, DatabaseCatalog>();
builder.Services.AddSingleton<ILayerCatalog, LayerCatalog>();

// Register Services
builder.Services.AddSingleton<IQueryValidator, QueryValidator>();
builder.Services.AddSingleton<ILayerPermissionService, LayerPermissionService>();
builder.Services.AddSingleton<IExportEngine, ExportEngine>();
builder.Services.AddSingleton<ExtractionJobQueue>();
builder.Services.AddSingleton<AuditLogger>();

// Register Background Service Worker
builder.Services.AddHostedService<ExtractionWorkerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
