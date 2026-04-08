using System;
using System.Windows;
using System.Windows.Controls;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Utils;

namespace PasswordManager.Views
{
    public partial class PasswordDetailsWindow : Window
    {
        private readonly PasswordEntry _entry;
        private readonly string _decryptedPassword;
        private bool _passwordVisible = false;

        public PasswordDetailsWindow(PasswordEntry entry, string decryptedPassword)
        {
            InitializeComponent();
            _entry = entry;
            _decryptedPassword = decryptedPassword;
            LoadEntryDetails();
        }

        private void LoadEntryDetails()
        {
            WebsiteText.Text = _entry.Website;
            UsernameText.Text = _entry.Username;
            PasswordTextBox.Password = _decryptedPassword;

            DescriptionText.Text = string.IsNullOrWhiteSpace(_entry.Description) 
                ? "无描述信息" 
                : _entry.Description;

            DatesText.Text = $"创建时间: {_entry.CreatedAt:yyyy-MM-dd HH:mm}\n" +
                           $"最后更新: {_entry.UpdatedAt:yyyy-MM-dd HH:mm}";

            if (App.ReminderService != null)
            {
                var reminderStatus = App.ReminderService.GetReminderStatus(_entry);
                var timeUntil = App.ReminderService.GetTimeUntilNextReminder(_entry);
                
                var color = timeUntil <= TimeSpan.FromDays(7) ? "#FF6B6B" : "#666";
                ReminderStatusText.Text = $"提醒状态: {reminderStatus}";
                ReminderStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color));
            }
            else
            {
                ReminderStatusText.Text = $"提醒周期: {_entry.ReminderDays}天";
            }

            UpdatePasswordStrength();
        }

        private void ShowPassword_Click(object sender, RoutedEventArgs e)
        {
            var parentGrid = PasswordTextBox.Parent as Grid;
            if (parentGrid == null) return;

            if (!_passwordVisible)
            {
                var plainPasswordTextBox = new TextBox
                {
                    Text = _decryptedPassword,
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.White,
                    VerticalContentAlignment = VerticalAlignment.Center
                };

                var columnIndex = Grid.GetColumn(PasswordTextBox);
                Grid.SetColumn(plainPasswordTextBox, columnIndex);
                parentGrid.Children.Remove(PasswordTextBox);
                parentGrid.Children.Add(plainPasswordTextBox);

                ShowPasswordToggle.Content = "🔒";
                _passwordVisible = true;
            }
            else
            {
                var plainPasswordTextBox = parentGrid.Children
                    .OfType<TextBox>()
                    .FirstOrDefault(tb => tb.IsReadOnly && tb.Text == _decryptedPassword);

                if (plainPasswordTextBox != null)
                {
                    var columnIndex = Grid.GetColumn(plainPasswordTextBox);
                    Grid.SetColumn(PasswordTextBox, columnIndex);
                    parentGrid.Children.Remove(plainPasswordTextBox);
                    parentGrid.Children.Add(PasswordTextBox);
                }

                ShowPasswordToggle.Content = "👁";
                _passwordVisible = false;
            }
        }

        private void UpdatePasswordStrength()
        {
            var isValid = PasswordGenerator.ValidatePasswordStrength(_decryptedPassword, out string message);
            
            if (isValid)
            {
                PasswordStrengthText.Text = "密码强度: 强";
                PasswordStrengthText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(76, 175, 80));
            }
            else
            {
                PasswordStrengthText.Text = $"密码强度: {message}";
                PasswordStrengthText.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 107, 107));
            }
        }

        private void CopyUsername_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_entry.Username);
            ShowCopyFeedback("用户名已复制到剪贴板");
        }

        private void CopyPassword_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(_decryptedPassword);
            ShowCopyFeedback("密码已复制到剪贴板\n将在30秒后自动清空");

            // Schedule clipboard clearing after 30 seconds
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            dispatcherTimer.Tick += (s, args) =>
            {
                try
                {
                    if (Clipboard.GetText() == _decryptedPassword)
                    {
                        Clipboard.Clear();
                    }
                }
                catch { }
                dispatcherTimer.Stop();
            };
            dispatcherTimer.Start();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowCopyFeedback(string message)
        {
            var originalText = CopyPasswordButton.Content.ToString();
            CopyPasswordButton.Content = "已复制 ✓";
            
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            dispatcherTimer.Tick += (s, args) =>
            {
                CopyPasswordButton.Content = originalText;
                dispatcherTimer.Stop();
            };
            dispatcherTimer.Start();

            MessageBox.Show(message, "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}