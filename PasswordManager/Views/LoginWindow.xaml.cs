using System;
using System.Windows;
using System.Windows.Input;
using PasswordManager.Services;

namespace PasswordManager.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            MasterPassword.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var password = MasterPassword.Password;
            
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("请输入主密码");
                return;
            }

            if (password.Length < 8)
            {
                ShowError("主密码长度至少需要8个字符");
                return;
            }

            try
            {
                var encryptionKey = App.EncryptionService.GenerateKeyFromMasterPassword(password);
                var keyExists = App.EncryptionService.TryLoadEncryptionKey(password, App.DataDirectory, out var existingKey);

                if (keyExists)
                {
                    if (App.InitializeDatabase(password))
                    {
                        App.StartReminderService();
                        OpenMainWindow();
                    }
                }
                else
                {
                    var result = MessageBox.Show(
                        "未找到密钥文件，是否创建新的主密码？",
                        "确认",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        App.EncryptionService.SaveEncryptionKey(encryptionKey, App.DataDirectory);
                        
                        if (App.InitializeDatabase(password))
                        {
                            App.StartReminderService();
                            OpenMainWindow();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"登录失败: {ex.Message}");
            }
        }

        private void MasterPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Login_Click(sender, e);
            }
        }

        private void ShowError(string message)
        {
            ErrorLabel.Content = message;
            MasterPassword.Clear();
            MasterPassword.Focus();
        }

        private void OpenMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
            this.Close();
        }
    }
}