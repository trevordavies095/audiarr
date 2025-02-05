using Microsoft.EntityFrameworkCore;
using MusicServer.Models;

namespace MusicServer.Data
{
    public class MusicDbContext : DbContext
    {
        public MusicDbContext(DbContextOptions<MusicDbContext> options) : base(options) { }

        public DbSet<Track> Tracks { get; set; }
    }
}

