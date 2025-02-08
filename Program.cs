using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;  // Add this namespace to access LibraryScanner
using Microsoft.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
var musicLibraryPath = Environment.GetEnvironmentVariable("MUSIC_LIBRARY_PATH");


if (string.IsNullOrEmpty(musicLibraryPath))
{
    Console.WriteLine("Warning: MUSIC_LIBRARY_PATH is not set. Using default path (/music).");
    musicLibraryPath = "/Volumes/Backup/Media/Music"; // Default path (modify as needed)
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Authentication & Authorization
builder.Services.AddAuthentication(); 
builder.Services.AddAuthorization();

builder.Services.AddDbContext<MusicDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<LibraryScanner>(); // Ensure LibraryScanner is registered
builder.Services.AddLogging();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// ** Apply Migrations Automatically **
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<MusicDbContext>();
        dbContext.Database.Migrate();  // 🚀 Ensures migrations are applied
        Console.WriteLine("✅ Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error applying migrations: {ex.Message}");
    }
}

// Then in the middleware pipeline:
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();  // Ensure authentication middleware is before authorization
app.UseAuthorization(); 

app.MapControllers();

// Log the configured music library path
Console.WriteLine($"Music Library Path: {musicLibraryPath}");

app.Run();