using Microsoft.EntityFrameworkCore;
using Models;

namespace Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options)
            : base(options)
        {
        }

        // Your table
        public DbSet<User> Users { get; set; } = null!;
        
        // Optional: override table names or relationships here
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Ensure Username is unique (already key)
            modelBuilder.Entity<User>()
                        .HasIndex(u => u.Username)
                        .IsUnique();
        }
    }
}
