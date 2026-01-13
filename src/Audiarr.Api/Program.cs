using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Audiarr.Data.Context;
using Audiarr.Api.Endpoints;
using Audiarr.Core.Configuration;
using Audiarr.Services.Auth;
using Audiarr.Services.Library;
using Audiarr.Services.Background;
using Audiarr.Core.Interfaces;
using Audiarr.Services.Users;
using Audiarr.Core.Services;
using Audiarr.Services;
using Serilog;
using Serilog.Events;
using Audiarr.Core.Entities;
using Audiarr.Core.DTOs;

// Configure Serilog - Docker-first approach with fallback for tests
var logPath = "/data/logs/audiarr-.log";
// Check if running in Docker or if /data is accessible
if (!Directory.Exists("/data") || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    // Use temp directory for non-Docker environments (tests, local dev)
    logPath = Path.Combine(Path.GetTempPath(), "audiarr-logs", "audiarr-.log");
}
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

    // Configure Multi-Valued Tags Settings
    builder.Services.Configure<MultiValuedTagsOptions>(builder.Configuration.GetSection("MultiValuedTags"));

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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy =>
            policy.RequireRole("admin"));
    });

    // Register services
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<ILibraryScanner, LibraryScanner>();
    builder.Services.AddScoped<IUserManagementService, UserManagementService>();
    builder.Services.AddScoped<IQueueService, QueueService>();
    builder.Services.AddSingleton<ScannerBackgroundService>();
    builder.Services.AddHostedService(provider => provider.GetRequiredService<ScannerBackgroundService>());

    // Configure SQLite database - Docker-first approach with fallback for tests
    var dataPath = "/data";
    // Check if running in Docker or if /data is accessible
    if (!Directory.Exists("/data") || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    {
        // Use temp directory for non-Docker environments (tests, local dev)
        dataPath = Path.Combine(Path.GetTempPath(), "audiarr-data");
    }
    Directory.CreateDirectory(dataPath);
    var connectionString = $"Data Source={Path.Combine(dataPath, "audiarr.db")}";
    Console.WriteLine($"Database location: {Path.Combine(dataPath, "audiarr.db")}");

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

    // Add HttpClient factory for dependency injection
    builder.Services.AddHttpClient();

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
    builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, BlazorAuthStateProvider>();
    builder.Services.AddAuthorizationCore();

    // Add memory cache for performance
    builder.Services.AddMemoryCache();

    var app = builder.Build();

    // Apply database migrations
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<AudiarrContext>();
            await context.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully");

            // Run data migration for genres (complex C# parsing logic)
            // This handles delimiter-separated genre strings that couldn't be handled in SQL
            // The SQL migration only handles single-value genres (no delimiters)
            try
            {
                // Check if there are tracks/albums with delimiter-separated genres that need migration
                // The SQL migration handles single-value genres, but delimiter-separated genres
                // (like "Rock/Metal") require C# parsing logic
                // Note: We filter out delimiter-only strings (e.g., "/", ";", ",") that would
                // produce no results after parsing, to prevent infinite migration loops
                var tracksWithDelimitedGenres = await context.Tracks
                    .Where(t => !string.IsNullOrWhiteSpace(t.Genre) && 
                        (t.Genre!.Contains("/") || t.Genre!.Contains(";") || t.Genre!.Contains(",")))
                    .ToListAsync();
                var tracksWithValidDelimitedGenres = tracksWithDelimitedGenres
                    .Count(t => Audiarr.Data.Migrations.DataMigrationHelper.WouldProduceGenres(t.Genre!));
                
                var albumsWithDelimitedGenres = await context.Albums
                    .Where(a => !string.IsNullOrWhiteSpace(a.Genre) && 
                        (a.Genre!.Contains("/") || a.Genre!.Contains(";") || a.Genre!.Contains(",")))
                    .ToListAsync();
                var albumsWithValidDelimitedGenres = albumsWithDelimitedGenres
                    .Count(a => Audiarr.Data.Migrations.DataMigrationHelper.WouldProduceGenres(a.Genre!));
                
                // Also check if there are any tracks/albums with genres that haven't been migrated at all
                // (in case the SQL migration didn't run or handle all single-value genres)
                // We need to check for genres that would actually produce results (not delimiter-only strings)
                var allTracksWithGenres = await context.Tracks
                    .Where(t => !string.IsNullOrWhiteSpace(t.Genre))
                    .ToListAsync();
                var tracksWithValidGenres = allTracksWithGenres
                    .Count(t => Audiarr.Data.Migrations.DataMigrationHelper.WouldProduceGenres(t.Genre!));
                
                var allAlbumsWithGenres = await context.Albums
                    .Where(a => !string.IsNullOrWhiteSpace(a.Genre))
                    .ToListAsync();
                var albumsWithValidGenres = allAlbumsWithGenres
                    .Count(a => Audiarr.Data.Migrations.DataMigrationHelper.WouldProduceGenres(a.Genre!));
                
                var trackGenresCount = await context.TrackGenres.CountAsync();
                var albumGenresCount = await context.AlbumGenres.CountAsync();
                
                // Run migration if:
                // 1. There are delimiter-separated genres that need C# parsing, OR
                // 2. There are valid genres (that would produce results) but no join table entries yet (initial migration case)
                // The helper is idempotent, so it's safe to run even if some genres are already migrated
                // Note: We check for valid genres (not delimiter-only strings) to prevent infinite loops
                bool needsMigration = (tracksWithValidDelimitedGenres > 0 || albumsWithValidDelimitedGenres > 0) ||
                    (tracksWithValidGenres > 0 && trackGenresCount == 0) ||
                    (albumsWithValidGenres > 0 && albumGenresCount == 0);
                
                if (needsMigration)
                {
                    Log.Information("Starting genre data migration for {TrackCount} tracks ({DelimitedTrackCount} with delimiters) and {AlbumCount} albums ({DelimitedAlbumCount} with delimiters)...", 
                        tracksWithValidGenres, tracksWithValidDelimitedGenres, albumsWithValidGenres, albumsWithValidDelimitedGenres);
                    Audiarr.Data.Migrations.DataMigrationHelper.MigrateGenres(context, "Tracks", "TrackGenres", "TrackId");
                    Audiarr.Data.Migrations.DataMigrationHelper.MigrateGenres(context, "Albums", "AlbumGenres", "AlbumId");
                    Log.Information("Genre data migration completed successfully");
                }
                else
                {
                    Log.Debug("Genre data migration skipped - genres already migrated");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error during genre data migration - this is non-fatal, migration will continue");
                // Don't throw - allow app to start even if genre migration fails
            }

            // Ensure admin user exists
            var userService = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var adminExists = await context.Users.AnyAsync(u => u.Username == "admin");
            if (!adminExists)
            {
                await userService.CreateUserAsync(new CreateUserRequest(
                    Username: "admin",
                    Email: "admin@audiarr.local",
                    Password: "admin",
                    Role: "admin"
                ));
                Log.Information("Default admin user created");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations");
            throw;
        }
    }

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
    app.MapUserEndpoints();
    app.MapScannerEndpoints();
    app.MapArtistEndpoints();
    app.MapAlbumEndpoints();
    app.MapTrackEndpoints();
    app.MapPlaylistEndpoints();
    app.MapQueueEndpoints();
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

// Make Program accessible for testing
public partial class Program { }
