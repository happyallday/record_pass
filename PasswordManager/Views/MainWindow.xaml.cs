using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PasswordManager.Models;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    public partial class MainWindow : Window
    {
        private List<PasswordEntry> _allPasswords = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadPasswords();
            RegisterReminderCallback();
        }

        private void LoadPasswords()
        {
            try
            {
                _allPasswords = App.DatabaseService?.GetAllPasswordEntries() ?? new List<PasswordEntry>();
                PasswordsDataGrid.ItemsSource = _allPasswords;
                UpdateStatus($" loaded {_allPasswords.Count} password entries");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载密码数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }

        private void AddPassword_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PasswordEntryDialog();
            if (dialog.ShowDialog() == true && dialog.PasswordEntry != null)
            {
                try
                {
                    var entry = dialog.PasswordEntry;
                    entry.CreatedAt = DateTime.Now;
                    entry.UpdatedAt = DateTime.Now;

                    UpdateStatus($"正在添加密码: {entry.Website}...");

                    var encryptedPassword = App.EncryptionService.EncryptPassword(entry.Password, App.CurrentMasterPassword);
                    entry.Password = encryptedPassword;

                    if (App.DatabaseService == null)
                    {
                        MessageBox.Show("数据库服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var id = App.DatabaseService.AddPasswordEntry(entry);
                    if (id > 0)
                    {
                        entry.Id = id;
                        _allPasswords.Add(entry);
                        RefreshDataGrid();
                        UpdateStatus($"已添加密码: {entry.Website}");
                        MessageBox.Show("密码添加成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("添加密码失败：返回的ID无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加密码失败: {ex.Message}\n\n详细信息: {ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatus($"添加密码失败: {ex.Message}");
                }
            }
        }

        private void EditPassword_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordsDataGrid.SelectedItem is not PasswordEntry selectedEntry)
            {
                MessageBox.Show("请选择要编辑的密码条目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new PasswordEntryDialog(selectedEntry);
            if (dialog.ShowDialog() == true && dialog.PasswordEntry != null)
            {
                var entry = dialog.PasswordEntry;
                entry.UpdatedAt = DateTime.Now;

                var encryptedPassword = App.EncryptionService.EncryptPassword(entry.Password, App.CurrentMasterPassword);
                entry.Password = encryptedPassword;

                try
                {
                    var success = App.DatabaseService?.UpdatePasswordEntry(entry) ?? false;
                    if (success)
                    {
                        var index = _allPasswords.FindIndex(p => p.Id == entry.Id);
                        if (index >= 0)
                        {
                            _allPasswords[index] = entry;
                        }
                        RefreshDataGrid();
                        UpdateStatus($"已更新密码: {entry.Website}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新密码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeletePassword_Click(object sender, RoutedEventArgs e)
        {
            if (PasswordsDataGrid.SelectedItem is not PasswordEntry selectedEntry)
            {
                MessageBox.Show("请选择要删除的密码条目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除密码 '{selectedEntry.Website}' 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var success = App.DatabaseService?.DeletePasswordEntry(selectedEntry.Id) ?? false;
                    if (success)
                    {
                        _allPasswords.Remove(selectedEntry);
                        RefreshDataGrid();
                        UpdateStatus($"已删除密码: {selectedEntry.Website}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除密码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadPasswords();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search_Click(sender, e);
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            var searchTerm = SearchTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                PasswordsDataGrid.ItemsSource = _allPasswords;
                UpdateStatus($"显示 {_allPasswords.Count} 个密码条目");
                return;
            }

            try
            {
                var results = App.DatabaseService?.SearchPasswordEntries(searchTerm) ?? new List<PasswordEntry>();
                PasswordsDataGrid.ItemsSource = results;
                UpdateStatus($"搜索到 {results.Count} 个结果");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PasswordsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PasswordsDataGrid.SelectedItem is PasswordEntry selectedEntry)
            {
                var decryptedPassword = App.EncryptionService.DecryptPassword(selectedEntry.Password, App.CurrentMasterPassword);
                
                var detailsWindow = new PasswordDetailsWindow(selectedEntry, decryptedPassword);
                detailsWindow.ShowDialog();
            }
        }

        private void ViewReminders_Click(object sender, RoutedEventArgs e)
        {
            if (App.ReminderService != null)
            {
                var passwordsNeedingAttention = App.ReminderService.GetPasswordsNeedingAttention();
                
                var remindersWindow = new RemindersWindow(passwordsNeedingAttention);
                remindersWindow.ShowDialog();
            }
        }

        private void ExportBackup_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Database Backup (*.db)|*.db|All Files (*.*)|*.*",
                DefaultExt = "db",
                FileName = $"passwords_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    App.DatabaseService?.BackupDatabase(saveFileDialog.FileName);
                    MessageBox.Show("备份成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"备份失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportBackup_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Database Backup (*.db)|*.db|All Files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var result = MessageBox.Show(
                    "导入备份将覆盖现有数据，确定要继续吗？",
                    "警告",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        App.DatabaseService?.RestoreDatabase(openFileDialog.FileName);
                        LoadPasswords();
                        MessageBox.Show("导入成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "密码管理器 v1.0\n\n一个安全的桌面密码管理工具\n\n特性：\n" +
                "• 安全的密码存储（AES-256）\n" +
                "• 密码强度检测和生成\n" +
                "• 定期密码修改提醒\n" +
                "• 数据备份和恢复",
                "关于",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void RefreshDataGrid()
        {
            var searchTerm = SearchTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                PasswordsDataGrid.ItemsSource = _allPasswords;
            }
            else
            {
                var results = _allPasswords
                    .Where(p => p.Website.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                               p.Username.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                               p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                PasswordsDataGrid.ItemsSource = results;
            }
        }

        private void RegisterReminderCallback()
        {
            if (App.ReminderService != null)
            {
                App.ReminderService.RegisterReminderCallback(OnPasswordReminder);
            }
        }

        private void OnPasswordReminder(PasswordEntry entry)
        {
            MessageBox.Show(
                $"密码 '{entry.Website}' 需要修改！\n" +
                $"上次修改时间: {entry.UpdatedAt:yyyy-MM-dd}\n" +
                $"建议修改周期: {entry.ReminderDays}天",
                "密码修改提醒",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );

            HighlightPasswordEntry(entry);
        }

        private void HighlightPasswordEntry(PasswordEntry entry)
        {
            var item = PasswordsDataGrid.Items.Cast<PasswordEntry>()
                .FirstOrDefault(p => p.Id == entry.Id);
            
            if (item != null)
            {
                PasswordsDataGrid.SelectedItem = item;
                PasswordsDataGrid.ScrollIntoView(item);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            App.StopReminderService();
            base.OnClosed(e);
        }
    }
}