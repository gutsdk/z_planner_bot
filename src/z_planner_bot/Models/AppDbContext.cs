﻿using Microsoft.EntityFrameworkCore;

namespace z_planner_bot.Models
{
    internal class AppDbContext : DbContext
    {
        public DbSet<Task> Tasks { get; set; } = null!;

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Task>(entity =>
            {
                entity.HasIndex(t => t.UserId);
                entity.Property(t => t.Title).IsRequired();
            });
        }
    }
}
