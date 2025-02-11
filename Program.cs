using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using MusicServer.Data;
using MusicServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Get the music library path from the environment or default to "/music"
var musicLibraryPath = Environment.GetEnvironmentVariable("MUSIC_LIBRARY_PATH") ?? "/music";
if (musicLibraryPath == "/music")
{
    Console.WriteLine("Warning: MUSIC_LIBRARY_PATH is not set. Using default path (/music).");
}

// Configure services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer(); // Only one call is needed
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddDbContext<MusicDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<LibraryScanner>();
builder.Services.AddLogging();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", cors =>
    {
        cors.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Audiarr API", Version = "v1" });
});

var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
        dbContext.Database.Migrate();
        Console.WriteLine("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error applying migrations: {ex.Message}");
    }
}

// Configure the middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Audiarr API v1");
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine($"Music Library Path: {musicLibraryPath}");
app.Run();
