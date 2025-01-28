using Microsoft.EntityFrameworkCore;

namespace z_planner_bot.Controllers
{
    internal class AppDbContext : DbContext
    {
        private readonly string connectionString;
        public DbSet<Models.Task> Tasks { get; set; } = null!;

        public AppDbContext(string connection)
        {
            connectionString = connection;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.Task>(entity =>
            {
                entity.HasIndex(t => t.UserId);
                entity.Property(t => t.Title).IsRequired();
            });
        }
    }
}
