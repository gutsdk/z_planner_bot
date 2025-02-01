using System.ComponentModel.DataAnnotations;

namespace z_planner_bot.Models
{
    internal class Task
    {
        [Key]
        public int Id { get; set; }
        public required string Title { get; set; }
        public string? Description { get; set; }
        public bool IsCompleted { get; set; } = false;
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public long UserId { get; set; }
    }
}
