using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Audiarr.Data.Context;

namespace Audiarr.Data;

public class AudiarrContextFactory : IDesignTimeDbContextFactory<AudiarrContext>
{
    public AudiarrContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AudiarrContext>();

        // Docker-first approach - use fixed path for design-time operations
        // This is only used for migrations and doesn't affect runtime
        var connectionString = "Data Source=/data/audiarr.db";
        Console.WriteLine($"Design-time DbContext using database at: /data/audiarr.db");

        optionsBuilder.UseSqlite(connectionString);

        return new AudiarrContext(optionsBuilder.Options);
    }
}