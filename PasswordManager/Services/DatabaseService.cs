using System;
using System.IO;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using PasswordManager.Models;

namespace PasswordManager.Services
{
    public class DatabaseService
    {
        private const string DatabaseName = "passwords.db";
        private readonly string _dataDirectory;

        public DatabaseService(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            InitializeDatabase();
        }

        private string DatabasePath => Path.Combine(_dataDirectory, DatabaseName);

        private void InitializeDatabase()
        {
            if (!Directory.Exists(_dataDirectory))
                Directory.CreateDirectory(_dataDirectory);

            if (!File.Exists(DatabasePath))
                CreateDatabase();
        }

        private void CreateDatabase()
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE Passwords (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Website TEXT NOT NULL,
                    Username TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    ReminderDays INTEGER NOT NULL,
                    LastReminderSent TEXT
                );

                CREATE INDEX idx_website ON Passwords(Website);
                CREATE INDEX idx_updated ON Passwords(UpdatedAt);
            ";

            command.ExecuteNonQuery();
        }

        private SqliteConnection CreateConnection()
        {
            var connectionString = $"Data Source={DatabasePath}";
            return new SqliteConnection(connectionString);
        }

        public int AddPasswordEntry(PasswordEntry entry)
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Passwords 
                (Website, Username, Password, Description, CreatedAt, UpdatedAt, ReminderDays, LastReminderSent)
                VALUES (@Website, @Username, @Password, @Description, @CreatedAt, @UpdatedAt, @ReminderDays, @LastReminderSent);
                SELECT last_insert_rowid();
            ";

            command.Parameters.AddWithValue("@Website", entry.Website);
            command.Parameters.AddWithValue("@Username", entry.Username);
            command.Parameters.AddWithValue("@Password", entry.Password);
            command.Parameters.AddWithValue("@Description", entry.Description);
            command.Parameters.AddWithValue("@CreatedAt", entry.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@UpdatedAt", entry.UpdatedAt.ToString("o"));
            command.Parameters.AddWithValue("@ReminderDays", entry.ReminderDays);
            command.Parameters.AddWithValue("@LastReminderSent", entry.LastReminderSent == DateTime.MinValue ? default(string) : entry.LastReminderSent.ToString("o"));

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        public bool UpdatePasswordEntry(PasswordEntry entry)
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Passwords 
                SET Website = @Website, 
                    Username = @Username, 
                    Password = @Password, 
                    Description = @Description, 
                    UpdatedAt = @UpdatedAt,
                    ReminderDays = @ReminderDays,
                    LastReminderSent = @LastReminderSent
                WHERE Id = @Id
            ";

            command.Parameters.AddWithValue("@Id", entry.Id);
            command.Parameters.AddWithValue("@Website", entry.Website);
            command.Parameters.AddWithValue("@Username", entry.Username);
            command.Parameters.AddWithValue("@Password", entry.Password);
            command.Parameters.AddWithValue("@Description", entry.Description);
            command.Parameters.AddWithValue("@UpdatedAt", entry.UpdatedAt.ToString("o"));
            command.Parameters.AddWithValue("@ReminderDays", entry.ReminderDays);
            command.Parameters.AddWithValue("@LastReminderSent", entry.LastReminderSent == DateTime.MinValue ? default(string) : entry.LastReminderSent.ToString("o"));

            return command.ExecuteNonQuery() > 0;
        }

        public bool DeletePasswordEntry(int id)
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Passwords WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            return command.ExecuteNonQuery() > 0;
        }

        public PasswordEntry? GetPasswordEntry(int id)
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Passwords WHERE Id = @Id";
            command.Parameters.AddWithValue("@Id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return ReadPasswordEntry(reader);
            }
            return null;
        }

        public List<PasswordEntry> GetAllPasswordEntries()
        {
            var entries = new List<PasswordEntry>();

            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Passwords ORDER BY Website";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(ReadPasswordEntry(reader));
            }

            return entries;
        }

        public List<PasswordEntry> SearchPasswordEntries(string searchTerm)
        {
            var entries = new List<PasswordEntry>();

            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT * FROM Passwords 
                WHERE Website LIKE @SearchTerm 
                   OR Username LIKE @SearchTerm 
                   OR Description LIKE @SearchTerm
                ORDER BY Website
            ";

            var searchPattern = $"%{searchTerm}%";
            command.Parameters.AddWithValue("@SearchTerm", searchPattern);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                entries.Add(ReadPasswordEntry(reader));
            }

            return entries;
        }

        public List<PasswordEntry> GetPasswordsNeedingReminder()
        {
            var entries = new List<PasswordEntry>();
            var now = DateTime.Now;

            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Passwords";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entry = ReadPasswordEntry(reader);
                var lastUpdate = entry.UpdatedAt;
                var reminderDueDate = lastUpdate.AddDays(entry.ReminderDays);

                if (now >= reminderDueDate && 
                    (entry.LastReminderSent == DateTime.MinValue || entry.LastReminderSent < reminderDueDate))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public bool UpdateLastReminderSent(int entryId, DateTime sentDate)
        {
            using var connection = CreateConnection();
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE Passwords 
                SET LastReminderSent = @LastReminderSent
                WHERE Id = @Id
            ";

            command.Parameters.AddWithValue("@Id", entryId);
            command.Parameters.AddWithValue("@LastReminderSent", sentDate.ToString("o"));

            return command.ExecuteNonQuery() > 0;
        }

        private PasswordEntry ReadPasswordEntry(SqliteDataReader reader)
        {
            return new PasswordEntry
            {
                Id = reader.GetInt32(0),
                Website = reader.GetString(1),
                Username = reader.GetString(2),
                Password = reader.GetString(3),
                Description = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                UpdatedAt = DateTime.Parse(reader.GetString(6)),
                ReminderDays = reader.GetInt32(7),
                LastReminderSent = reader.IsDBNull(8) ? DateTime.MinValue : DateTime.Parse(reader.GetString(8))
            };
        }

        public void BackupDatabase(string backupPath)
        {
            if (File.Exists(DatabasePath))
            {
                File.Copy(DatabasePath, backupPath, true);
            }
        }

        public void RestoreDatabase(string backupPath)
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, DatabasePath, true);
            }
        }
    }
}