using Microsoft.EntityFrameworkCore;

namespace z_planner_bot.Controllers
{
    internal class AppDbContext : DbContext
    {
        public DbSet<Models.Task> Tasks { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
