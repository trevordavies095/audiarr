using Microsoft.EntityFrameworkCore;
using MusicServer.Data;
using MusicServer.Services;  // Add this namespace to access LibraryScanner

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Register the MusicDbContext with InMemory database (or use a different database provider)
builder.Services.AddDbContext<MusicDbContext>(options =>
    options.UseInMemoryDatabase("MusicDb")  // Or replace with a real database like SQL Server
);

// Register LibraryScanner service
builder.Services.AddScoped<LibraryScanner>();  // Change from AddSingleton to AddScoped

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();
