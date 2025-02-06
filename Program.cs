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
    musicLibraryPath = "/music"; // Default path (modify as needed)
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




// Load MusicLibraryPath from appsettings.json
var musicLibraryPath = builder.Configuration.GetValue<string>("MusicLibraryPath");

var app = builder.Build();

// Then in the middleware pipeline:
app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();  // Ensure authentication middleware is before authorization
app.UseAuthorization(); 

app.MapControllers();

// Log the configured music library path
Console.WriteLine($"Music Library Path: {musicLibraryPath}");

app.Run();