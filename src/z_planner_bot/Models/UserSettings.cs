using System.ComponentModel.DataAnnotations;

namespace z_planner_bot.Models
{
    internal class UserSettings
    {
        [Key]
        public int Id { get; set; }
        public SortType SortType { get; set; } = SortType.ByStatus;         // значение по умолчанию
        public bool IsNotificationsEnabled { get; set; }

        public long UserId { get; set; }
    }

    public enum SortType
    {
        ByDate,
        ByStatus,
        ByTitle
    }
}
