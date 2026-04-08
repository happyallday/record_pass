using System;

namespace PasswordManager.Models
{
    public class PasswordEntry
    {
        public int Id { get; set; }
        public string Website { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int ReminderDays { get; set; } = 90;
        public DateTime LastReminderSent { get; set; } = DateTime.MinValue;
    }
}