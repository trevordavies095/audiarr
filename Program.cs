using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;  // Add this namespace to access LibraryScanner

var builder = WebApplication.CreateBuilder(args);

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
// Load MusicLibraryPath from appsettings.json
var musicLibraryPath = builder.Configuration.GetValue<string>("MusicLibraryPath");

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();  // Ensure authentication middleware is before authorization
app.UseAuthorization(); 

app.MapControllers();

app.Run();