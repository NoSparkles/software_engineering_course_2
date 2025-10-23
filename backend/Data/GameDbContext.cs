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

        public DbSet<User> Users { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            // Configure owned entities for invitations
            modelBuilder.Entity<User>().OwnsMany(u => u.OutcomingInviteToGameRequests, b =>
            {
                b.WithOwner();
                b.ToJson(); // Store as JSON if supported (EF Core 8+)
            });

            modelBuilder.Entity<User>().OwnsMany(u => u.IncomingInviteToGameRequests, b =>
            {
                b.WithOwner();
                b.ToJson();
            });
        }
    }
}
