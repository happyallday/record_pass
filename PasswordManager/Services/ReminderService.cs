using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PasswordManager.Models;
using Timer = System.Timers.Timer;

namespace PasswordManager.Services
{
    public class ReminderService
    {
        private readonly DatabaseService _databaseService;
        private Timer? _reminderTimer;
        private readonly List<Action<PasswordEntry>> _reminderCallbacks = new();

        public ReminderService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public void Start()
        {
            if (_reminderTimer != null) return;

            _reminderTimer = new Timer
            {
                Interval = TimeSpan.FromMinutes(30).TotalMilliseconds, // Check every 30 minutes
                AutoReset = true
            };

            _reminderTimer.Elapsed += OnReminderCheck;
            _reminderTimer.Start();
        }

        public void Stop()
        {
            _reminderTimer?.Stop();
            _reminderTimer?.Dispose();
            _reminderTimer = null;
        }

        private void OnReminderCheck(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Task.Run(() => CheckForReminders());
        }

        public void RegisterReminderCallback(Action<PasswordEntry> callback)
        {
            _reminderCallbacks.Add(callback);
        }

        private void CheckForReminders()
        {
            try
            {
                var passwordsNeedingReminder = _databaseService.GetPasswordsNeedingReminder();

                foreach (var passwordEntry in passwordsNeedingReminder)
                {
                    // Notify registered callbacks
                    foreach (var callback in _reminderCallbacks)
                    {
                        Application.Current.Dispatcher.Invoke(() => callback(passwordEntry));
                    }

                    // Update last reminder sent time
                    _databaseService.UpdateLastReminderSent(passwordEntry.Id, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reminder check failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public DateTime CalculateNextReminderDate(PasswordEntry entry)
        {
            return entry.UpdatedAt.AddDays(entry.ReminderDays);
        }

        public TimeSpan GetTimeUntilNextReminder(PasswordEntry entry)
        {
            var nextReminder = CalculateNextReminderDate(entry);
            return nextReminder - DateTime.Now;
        }

        public string GetReminderStatus(PasswordEntry entry)
        {
            var timeUntil = GetTimeUntilNextReminder(entry);

            if (timeUntil <= TimeSpan.Zero)
                return "需要立即修改密码";
            else if (timeUntil <= TimeSpan.FromDays(1))
                return $"需要修改密码 (剩余 {timeUntil.Hours} 小时)";
            else if (timeUntil <= TimeSpan.FromDays(7))
                return $"建议修改密码 (剩余 {timeUntil.Days} 天)";
            else
                return $"下次提醒: {CalculateNextReminderDate(entry):yyyy-MM-dd}";
        }

        public List<PasswordEntry> GetPasswordsNeedingAttention()
        {
            var allPasswords = _databaseService.GetAllPasswordEntries();
            return allPasswords
                .Where(p => GetTimeUntilNextReminder(p) <= TimeSpan.FromDays(7))
                .OrderBy(p => GetTimeUntilNextReminder(p))
                .ToList();
        }
    }
}