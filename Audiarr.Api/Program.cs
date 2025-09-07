using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Audiarr.Api.Data;
using Audiarr.Api.Endpoints;
using Audiarr.Api.Models.Configuration;
using Audiarr.Api.Services;
using Audiarr.Api.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHealthChecks();

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
    options.UseSqlite(connectionString));

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

// Add Blazor authentication
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, Audiarr.Api.Services.BlazorAuthStateProvider>();
builder.Services.AddAuthorizationCore();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files from wwwroot
app.UseStaticFiles();

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
app.MapDiagnosticEndpoints();
app.MapDataCleanupEndpoints();

// Map Blazor Server endpoints
app.MapBlazorHub();
app.MapHub<Audiarr.Api.Hubs.ScanHub>("/hubs/scan");
app.MapFallbackToPage("/admin/{*catchall}", "/_Host");

app.Run();
