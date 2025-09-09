using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Audiarr.Data.Context;

namespace Audiarr.Data;

public class AudiarrContextFactory : IDesignTimeDbContextFactory<AudiarrContext>
{
    public AudiarrContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AudiarrContext>();
        
        // Find the project root directory (where the .sln file is)
        var currentDirectory = Directory.GetCurrentDirectory();
        var projectRoot = currentDirectory;
        
        // Walk up the directory tree to find the solution root
        while (!File.Exists(Path.Combine(projectRoot, "Audiarr.sln")) && projectRoot != Path.GetPathRoot(projectRoot))
        {
            var parent = Directory.GetParent(projectRoot);
            if (parent == null) break;
            projectRoot = parent.FullName;
        }
        
        // If we couldn't find the solution file, use current directory
        if (!File.Exists(Path.Combine(projectRoot, "Audiarr.sln")))
        {
            projectRoot = currentDirectory;
        }
        
        // Create Data directory at project root if it doesn't exist
        var dataPath = Path.Combine(projectRoot, "Data");
        Directory.CreateDirectory(dataPath);
        
        // Use consistent database path
        var connectionString = $"Data Source={Path.Combine(dataPath, "audiarr.db")}";
        Console.WriteLine($"Design-time DbContext using database at: {Path.Combine(dataPath, "audiarr.db")}");
        
        optionsBuilder.UseSqlite(connectionString);

        return new AudiarrContext(optionsBuilder.Options);
    }
}