var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHealthChecks();
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

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

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

app.Run();
