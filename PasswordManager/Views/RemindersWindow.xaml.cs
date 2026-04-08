using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using PasswordManager.Models;
using PasswordManager.Services;
using System.Windows.Media;

namespace PasswordManager.Views
{
    public class ReminderItem
    {
        public PasswordEntry PasswordEntry { get; set; } = null!;
        public string Website => PasswordEntry.Website;
        public string Username => PasswordEntry.Username;
        public string StatusMessage { get; set; } = string.Empty;
        public string Message => StatusMessage; // Duplicate for binding convenience
        public string StatusIcon => StatusColor == Colors.Red ? "!" : StatusColor == Colors.Orange ? "⚠" : "ℹ";
        public Color StatusColor { get; set; }
        public Color MessageColor => StatusColor;
        public ICommand ViewDetailsCommand { get; set; } = null!;
        public ICommand UpdatePasswordCommand { get; set; } = null!;

        public ReminderItem(PasswordEntry entry, TimeSpan timeUntilReminder, Action<PasswordEntry> viewDetailsAction, Action<PasswordEntry> updatePasswordAction)
        {
            PasswordEntry = entry;

            if (timeUntilReminder <= TimeSpan.Zero)
            {
                StatusMessage = $"需要立即修改密码！(上次更新: {entry.UpdatedAt:yyyy-MM-dd})";
                StatusColor = Colors.Red;
            }
            else if (timeUntilReminder <= TimeSpan.FromDays(1))
            {
                StatusMessage = $"需要修改密码: 剩余 {(int)timeUntilReminder.TotalHours} 小时 (上次更新: {entry.UpdatedAt:yyyy-MM-dd})";
                StatusColor = Colors.Red;
            }
            else if (timeUntilReminder <= TimeSpan.FromDays(7))
            {
                StatusMessage = $"建议修改密码: 剩余 {timeUntilReminder.Days} 天 (上次更新: {entry.UpdatedAt:yyyy-MM-dd})";
                StatusColor = Colors.Orange;
            }
            else
            {
                StatusMessage = $"下次提醒: {entry.UpdatedAt.AddDays(entry.ReminderDays):yyyy-MM-dd}";
                StatusColor = Colors.Green;
            }

            ViewDetailsCommand = new RelayCommand(_ => viewDetailsAction(entry));
            UpdatePasswordCommand = new RelayCommand(_ => updatePasswordAction(entry));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute(parameter);
        }
    }

    public partial class RemindersWindow : Window
    {
        private readonly List<PasswordEntry> _passwordsNeedingAttention;
        private readonly List<ReminderItem> _reminderItems = new();

        public RemindersWindow(List<PasswordEntry> passwordsNeedingAttention)
        {
            InitializeComponent();
            _passwordsNeedingAttention = passwordsNeedingAttention;
            LoadReminders();
        }

        private void LoadReminders()
        {
            try
            {
                _reminderItems.Clear();
                
                if (App.ReminderService == null)
                {
                    StatusText.Text = "提醒服务未启动";
                    return;
                }

                foreach (var passwordEntry in _passwordsNeedingAttention)
                {
                    var timeUntil = App.ReminderService.GetTimeUntilNextReminder(passwordEntry);
                    var reminderItem = new ReminderItem(
                        passwordEntry,
                        timeUntil,
                        ViewPasswordDetails,
                        UpdatePassword
                    );
                    _reminderItems.Add(reminderItem);
                }

                // Sort by urgency
                _reminderItems.Sort((a, b) => 
                {
                    var timeA = App.ReminderService.GetTimeUntilNextReminder(a.PasswordEntry);
                    var timeB = App.ReminderService.GetTimeUntilNextReminder(b.PasswordEntry);
                    return timeA.CompareTo(timeB);
                });

                RemindersItemsControl.ItemsSource = _reminderItems;
                UpdateStatus($"显示了 {_reminderItems.Count} 个需要关注的密码");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载提醒信息失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewPasswordDetails(PasswordEntry entry)
        {
            try
            {
                var decryptedPassword = App.EncryptionService.DecryptPassword(entry.Password, App.CurrentMasterPassword);
                var detailsWindow = new PasswordDetailsWindow(entry, decryptedPassword);
                detailsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查看密码详情失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePassword(PasswordEntry entry)
        {
            try
            {
                var decryptedPassword = App.EncryptionService.DecryptPassword(entry.Password, App.CurrentMasterPassword);
                
                var dialog = new PasswordEntryDialog(entry);
                if (dialog.ShowDialog() == true && dialog.PasswordEntry != null)
                {
                    var updatedEntry = dialog.PasswordEntry;
                    updatedEntry.Id = entry.Id;
                    updatedEntry.CreatedAt = entry.CreatedAt;
                    updatedEntry.UpdatedAt = DateTime.Now;

                    var encryptedPassword = App.EncryptionService.EncryptPassword(updatedEntry.Password, App.CurrentMasterPassword);
                    updatedEntry.Password = encryptedPassword;

                    var success = App.DatabaseService?.UpdatePasswordEntry(updatedEntry) ?? false;
                    if (success)
                    {
                        MessageBox.Show("密码更新成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Update the local entry
                        var localEntry = _passwordsNeedingAttention.FirstOrDefault(p => p.Id == entry.Id);
                        if (localEntry != null)
                        {
                            localEntry.Website = updatedEntry.Website;
                            localEntry.Username = updatedEntry.Username;
                            localEntry.Password = encryptedPassword;
                            localEntry.Description = updatedEntry.Description;
                            localEntry.ReminderDays = updatedEntry.ReminderDays;
                            localEntry.LastReminderSent = DateTime.MinValue; // Reset reminder
                        }

                        // Reload reminders
                        LoadReminders();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新密码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReminders();
        }

        private void ExportReminders_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = "txt",
                FileName = $"password_reminders_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    var content = GenerateReminderReport();
                    File.WriteAllText(saveFileDialog.FileName, content);
                    MessageBox.Show("导出成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GenerateReminderReport()
        {
            var report = new List<string>
            {
                "== 密码修改提醒报告 ==",
                $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"总条目数: {_reminderItems.Count}",
                "",
                "提醒列表:",
                new string('-', 80)
            };

            foreach (var item in _reminderItems)
            {
                report.Add($"网站: {item.Website}");
                report.Add($"用户名: {item.Username}");
                report.Add($"提醒状态: {item.Message}");
                report.Add($"最后更新: {item.PasswordEntry.UpdatedAt:yyyy-MM-dd}");
                report.Add($"提醒周期: {item.PasswordEntry.ReminderDays} 天");
                if (!string.IsNullOrWhiteSpace(item.PasswordEntry.Description))
                {
                    report.Add($"描述: {item.PasswordEntry.Description}");
                }
                report.Add(new string('-', 80));
            }

            report.Add("");
            report.Add("== 报告结束 ==");

            return string.Join(Environment.NewLine, report);
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
    }
}