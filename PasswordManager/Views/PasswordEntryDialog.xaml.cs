using System;
using System.Windows;
using System.Windows.Controls;
using PasswordManager.Models;
using PasswordManager.Utils;

namespace PasswordManager.Views
{
    public partial class PasswordEntryDialog : Window
    {
        public PasswordEntry? PasswordEntry { get; private set; }
        private PasswordEntry? _originalEntry;
        private bool _isPasswordVisible = false;

        public string WindowTitle => _originalEntry == null ? "添加密码" : "编辑密码";
        public string WindowSubtitle => _originalEntry == null ? "创建新的密码条目" : $"编辑密码: {_originalEntry.Website}";

        public string Website { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ReminderDays { get; set; } = 90;
        public string CreatedDate { get; private set; } = string.Empty;
        public string UpdateDate { get; private set; } = string.Empty;

        public PasswordEntryDialog()
        {
            InitializeComponent();
            DataContext = this;
            SetDefaultDates();
            InitializePasswordGenerator();
        }

        public PasswordEntryDialog(PasswordEntry entry) : this()
        {
            _originalEntry = entry;
            LoadEntryData(entry);
            IsEditMode = true;
        }

        private bool IsEditMode { get; set; } = false;

        private void SetDefaultDates()
        {
            var now = DateTime.Now;
            CreatedDate = now.ToString("yyyy-MM-dd HH:mm");
            UpdateDate = now.ToString("yyyy-MM-dd HH:mm");
        }

        private void LoadEntryData(PasswordEntry entry)
        {
            Website = entry.Website;
            Username = entry.Username;
            Description = entry.Description;
            ReminderDays = entry.ReminderDays;
            CreatedDate = entry.CreatedAt.ToString("yyyy-MM-dd HH:mm");
            UpdateDate = entry.UpdatedAt.ToString("yyyy-MM-dd HH:mm");

            try
            {
                var decryptedPassword = App.EncryptionService.DecryptPassword(entry.Password, App.CurrentMasterPassword);
                PasswordTextBox.Password = decryptedPassword;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"密码解密失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializePasswordGenerator()
        {
            PasswordLengthSlider.ValueChanged += (s, e) =>
            {
                PasswordLengthValue.Text = ((int)e.NewValue).ToString();
            };
        }

        private void GeneratePassword_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var options = new PasswordGenerator.PasswordGeneratorOptions
                {
                    Length = (int)PasswordLengthSlider.Value,
                    IncludeUppercase = IncludeUpperCheckBox.IsChecked ?? true,
                    IncludeLowercase = IncludeLowerCheckBox.IsChecked ?? true,
                    IncludeDigits = IncludeDigitCheckBox.IsChecked ?? true,
                    IncludeSpecialChars = IncludeSpecialCheckBox.IsChecked ?? true
                };

                var generatedPassword = PasswordGenerator.GeneratePassword(options);
                PasswordTextBox.Password = generatedPassword;

                if (_isPasswordVisible)
                {
                    if (_plainPasswordTextBox != null)
                    {
                        _plainPasswordTextBox.Text = generatedPassword;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"密码生成失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Windows.Controls.TextBox? _plainPasswordTextBox;

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            var parentGrid = PasswordTextBox.Parent as System.Windows.Controls.Grid;
            if (parentGrid == null) return;

            if (!_isPasswordVisible)
            {
                _plainPasswordTextBox = new System.Windows.Controls.TextBox
                {
                    Text = PasswordTextBox.Password,
                    VerticalContentAlignment = System.Windows.VerticalAlignment.Center
                };

                var columnIndex = System.Windows.Controls.Grid.GetColumn(PasswordTextBox);
                System.Windows.Controls.Grid.SetColumn(_plainPasswordTextBox, columnIndex);
                parentGrid.Children.Remove(PasswordTextBox);
                parentGrid.Children.Add(_plainPasswordTextBox);

                TogglePasswordButton.Content = "🔒";
                _isPasswordVisible = true;
            }
            else
            {
                if (_plainPasswordTextBox != null)
                {
                    PasswordTextBox.Password = _plainPasswordTextBox.Text;
                    var columnIndex = System.Windows.Controls.Grid.GetColumn(_plainPasswordTextBox);
                    System.Windows.Controls.Grid.SetColumn(PasswordTextBox, columnIndex);
                    parentGrid.Children.Remove(_plainPasswordTextBox);
                    parentGrid.Children.Add(PasswordTextBox);
                }

                TogglePasswordButton.Content = "👁";
                _isPasswordVisible = false;
            }
        }

        private void PasswordTextBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isPasswordVisible && _plainPasswordTextBox != null)
            {
                _plainPasswordTextBox.Text = PasswordTextBox.Password;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                var password = PasswordTextBox.Password;

                if (!PasswordGenerator.ValidatePasswordStrength(password, out string validationMessage))
                {
                    var result = MessageBox.Show(
                        $"密码强度不符合要求：{validationMessage}\n\n是否仍然保存？",
                        "密码强度警告",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                if (_originalEntry != null)
                {
                    PasswordEntry = new PasswordEntry
                    {
                        Id = _originalEntry.Id,
                        Website = Website,
                        Username = Username,
                        Password = password,
                        Description = Description,
                        CreatedAt = _originalEntry.CreatedAt,
                        UpdatedAt = DateTime.Now,
                        ReminderDays = ReminderDays,
                        LastReminderSent = _originalEntry.LastReminderSent
                    };
                }
                else
                {
                    PasswordEntry = new PasswordEntry
                    {
                        Website = Website,
                        Username = Username,
                        Password = password,
                        Description = Description,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        ReminderDays = ReminderDays
                    };
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(Website))
            {
                MessageBox.Show("请输入网站名称", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                WebsiteTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show("请输入用户名", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                UsernameTextBox.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(PasswordTextBox.Password))
            {
                MessageBox.Show("请输入密码", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                PasswordTextBox.Focus();
                return false;
            }

            if (!int.TryParse(ReminderDaysTextBox.Text, out int reminderDays) || reminderDays <= 0)
            {
                MessageBox.Show("请输入有效的提醒天数", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                ReminderDaysTextBox.Focus();
                return false;
            }

            return true;
        }

        private void QuickReminderSet_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            
            var menuItems = new[]
            {
                new { Text = "30天", Value = 30 },
                new { Text = "60天", Value = 60 },
                new { Text = "90天", Value = 90 },
                new { Text = "120天", Value = 120 },
                new { Text = "180天", Value = 180 },
                new { Text = "365天", Value = 365 }
            };

            foreach (var item in menuItems)
            {
                var menuItem = new MenuItem
                {
                    Header = item.Text
                };
                menuItem.Click += (s, args) => ReminderDaysTextBox.Text = item.Value.ToString();
                menu.Items.Add(menuItem);
            }

            menu.IsOpen = true;
        }
    }
}