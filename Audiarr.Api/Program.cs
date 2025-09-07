using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Audiarr.Api.Data;
using Audiarr.Api.Endpoints;
using Audiarr.Api.Models.Configuration;
using Audiarr.Api.Services;
using Audiarr.Api.Services.Interfaces;
using Serilog;
using Serilog.Events;

// Configure Serilog
var logPath = Path.Combine(
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" 
        ? Path.Combine(Directory.GetCurrentDirectory(), "Logs")
        : "/data/logs",
    "audiarr-.log");

Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10485760, // 10MB
        rollOnFileSizeLimit: true)
    .CreateLogger();

try
{
    Log.Information("Starting Audiarr API");
    
    var builder = WebApplication.CreateBuilder(args);
    
    // Add Serilog to the container
    builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddHealthChecks();

// Add response compression for better performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Optimal;
});

// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() 
    ?? throw new InvalidOperationException("JwtSettings not configured");

// Add Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILibraryScanner, LibraryScanner>();
builder.Services.AddSingleton<ScannerBackgroundService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<ScannerBackgroundService>());

// Configure SQLite database
var dataPath = builder.Environment.IsDevelopment() 
    ? Path.Combine(Directory.GetCurrentDirectory(), "Data")
    : "/data";
Directory.CreateDirectory(dataPath);
var connectionString = $"Data Source={Path.Combine(dataPath, "audiarr.db")}";

builder.Services.AddDbContext<AudiarrContext>(options =>
{
    options.UseSqlite(connectionString, sqliteOptions =>
    {
        sqliteOptions.CommandTimeout(30);
    });
    
    // Performance optimizations
    options.UseQueryTrackingBehavior(Microsoft.EntityFrameworkCore.QueryTrackingBehavior.NoTrackingWithIdentityResolution);
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
    options.EnableServiceProviderCaching();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Audiarr API", 
        Version = "v2.0.0",
        Description = "A self-hosted music streaming server API"
    });
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Blazor Server services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add HttpClient for Blazor components
builder.Services.AddScoped<HttpClient>(sp =>
{
    var navigationManager = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient
    {
        BaseAddress = new Uri(navigationManager.BaseUri)
    };
});

// Add Blazor authentication
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, Audiarr.Api.Services.BlazorAuthStateProvider>();
builder.Services.AddAuthorizationCore();

// Add memory cache for performance
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Add response compression middleware
app.UseResponseCompression();

// Add Serilog request logging (moved here for proper ordering)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.GetLevel = (httpContext, elapsed, ex) => ex != null 
        ? LogEventLevel.Error 
        : httpContext.Response.StatusCode > 499 
            ? LogEventLevel.Error 
            : LogEventLevel.Information;
});

// Serve static files from wwwroot with caching headers
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 1 year
        const int durationInSeconds = 60 * 60 * 24 * 365;
        ctx.Context.Response.Headers.Append(
            "Cache-Control", $"public, max-age={durationInSeconds}");
    }
});

// Add authentication & authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Map health checks with detailed response
app.MapHealthChecks("/health");

// Root endpoint
app.MapGet("/", () => "Audiarr 2.0 API")
   .WithName("Root")
   .WithOpenApi()
   .ExcludeFromDescription();

// API info endpoint
app.MapGet("/api/info", () => Results.Ok(new
{
    name = "Audiarr API",
    version = "2.0.0",
    description = "Self-hosted music streaming server",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}))
.WithName("ApiInfo")
.WithOpenApi()
.WithSummary("Get API information")
.WithDescription("Returns basic information about the Audiarr API");

// Map API endpoints
app.MapAuthEndpoints();
app.MapScannerEndpoints();
app.MapArtistEndpoints();
app.MapAlbumEndpoints();
app.MapTrackEndpoints();
app.MapSearchEndpoints();
app.MapStreamEndpoints();
app.MapDiagnosticEndpoints();
app.MapDataCleanupEndpoints();

// Map Blazor Server endpoints
app.MapBlazorHub();
app.MapHub<Audiarr.Api.Hubs.ScanHub>("/hubs/scan");
app.MapFallbackToPage("/admin/{*catchall}", "/_Host");

    Log.Information("Audiarr API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
