using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Audiarr.Data.Context;
using Audiarr.Core.Configuration;
using Audiarr.Core.Entities;
using Audiarr.Services.Background;
using BCrypt.Net;

namespace Audiarr.Tests.Api;

public class AudiarrWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "JwtSettings:SecretKey", "TestSecretKeyThatIsAtLeast32CharactersLong!" },
                { "JwtSettings:Issuer", "AudiarrAPI" },
                { "JwtSettings:Audience", "AudiarrClient" },
                { "JwtSettings:AccessTokenExpirationMinutes", "60" },
                { "JwtSettings:RefreshTokenExpirationDays", "7" },
                { "MultiValuedTags:Delimiter", "/" },
                { "MultiValuedTags:EnableDelimiterParsing", "true" },
                { "MultiValuedTags:PreferredDelimiters:0", "/" },
                { "MultiValuedTags:PreferredDelimiters:1", ";" },
                { "MultiValuedTags:PreferredDelimiters:2", "," }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AudiarrContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AudiarrContext));
            if (dbDescriptor != null)
            {
                services.Remove(dbDescriptor);
            }

            // Add in-memory database
            services.AddDbContext<AudiarrContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString());
                options.EnableServiceProviderCaching(false);
            });

            // Remove background services
            var scannerService = services.FirstOrDefault(s => s.ServiceType == typeof(ScannerBackgroundService));
            if (scannerService != null)
            {
                services.Remove(scannerService);
            }

            var hostedServices = services.Where(s => s.ServiceType == typeof(IHostedService)).ToList();
            foreach (var service in hostedServices)
            {
                services.Remove(service);
            }

            // Build service provider to get context and seed test data
            var sp = services.BuildServiceProvider();
            using (var scope = sp.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AudiarrContext>();
                context.Database.EnsureCreated();
                
                // Seed a test user for authentication
                SeedTestUser(context);
            }
        });
    }

    private void SeedTestUser(AudiarrContext context)
    {
        if (!context.Users.Any(u => u.Username == "testuser"))
        {
            var testUser = new User
            {
                Username = "testuser",
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("testpassword"),
                Role = "user",
                IsActive = true
            };
            context.Users.Add(testUser);
            context.SaveChanges();
        }
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        
        // Login to get a token
        var loginResponse = client.PostAsJsonAsync("/api/v2/auth/login", new
        {
            username = "testuser",
            password = "testpassword"
        }).GetAwaiter().GetResult();

        if (loginResponse.IsSuccessStatusCode)
        {
            var loginResult = loginResponse.Content.ReadFromJsonAsync<LoginResponse>().GetAwaiter().GetResult();
            if (loginResult != null)
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.AccessToken);
            }
        }

        return client;
    }

    public DbContextScope GetDbContext()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AudiarrContext>();
        return new DbContextScope(scope, context);
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}

/// <summary>
/// Wrapper class that holds both an IServiceScope and the DbContext it provides.
/// Disposes the scope when this wrapper is disposed, preventing resource leaks.
/// </summary>
public class DbContextScope : IDisposable
{
    private readonly IServiceScope _scope;
    private bool _disposed;

    public AudiarrContext Context { get; }

    public DbContextScope(IServiceScope scope, AudiarrContext context)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _scope?.Dispose();
            _disposed = true;
        }
    }
}
