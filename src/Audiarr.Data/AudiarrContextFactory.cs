using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Audiarr.Data.Context;

namespace Audiarr.Data;

public class AudiarrContextFactory : IDesignTimeDbContextFactory<AudiarrContext>
{
    public AudiarrContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AudiarrContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Data Source=audiarr.db";
        
        optionsBuilder.UseSqlite(connectionString);

        return new AudiarrContext(optionsBuilder.Options);
    }
}