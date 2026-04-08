using System;
using System.IO;
using System.Windows;
using PasswordManager.Services;

namespace PasswordManager
{
    public partial class App : Application
    {
        public static string DataDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PasswordManager"
        );

        public static EncryptionService EncryptionService { get; } = new();
        public static DatabaseService? DatabaseService { get; private set; }
        public static ReminderService? ReminderService { get; private set; }

        public static string CurrentMasterPassword { get; private set; } = string.Empty;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            if (!Directory.Exists(DataDirectory))
            {
                Directory.CreateDirectory(DataDirectory);
            }
        }

        public static bool InitializeDatabase(string masterPassword)
        {
            try
            {
                DatabaseService = new DatabaseService(DataDirectory);
                CurrentMasterPassword = masterPassword;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据库初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public static void StartReminderService()
        {
            if (DatabaseService != null && ReminderService == null)
            {
                ReminderService = new ReminderService(DatabaseService);
                ReminderService.Start();
            }
        }

        public static void StopReminderService()
        {
            ReminderService?.Stop();
            ReminderService = null;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StopReminderService();
            base.OnExit(e);
        }
    }
}