using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

using Microsoft.Data.Sqlite;

using TimmyTools.Core.Configurations;

namespace TimmyTools.WpfUi.Services;

public class DatabaseBackupService(DatabaseConfiguration databaseConfiguration)
{
    private const int MaxBackups = 3;
    private static readonly TimeSpan BackupInterval = TimeSpan.FromMinutes(30);

    private readonly DatabaseConfiguration _databaseConfiguration = databaseConfiguration;
    private readonly DispatcherTimer _backupTimer = new() { Interval = BackupInterval };

    private int _currentSlot;

    public void Start()
    {
        CreateBackup();

        _backupTimer.Tick += OnBackupTimerTick;
        _backupTimer.Start();
    }

    public void Stop()
    {
        _backupTimer.Stop();
        _backupTimer.Tick -= OnBackupTimerTick;
    }

    private void OnBackupTimerTick(object? sender, EventArgs e)
    {
        CreateBackup();
    }

    private void CreateBackup()
    {
        try
        {
            string backupDir = Path.Combine(_databaseConfiguration.DataPath, "backups");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            string backupPath = Path.Combine(backupDir, $"timmy_tools_backup_{_currentSlot + 1}.sqlite");

            if (File.Exists(backupPath))
                File.Delete(backupPath);

            using SqliteConnection connection = new(_databaseConfiguration.ConnectionString);
            connection.Open();

            using SqliteCommand command = new($"VACUUM INTO @backupPath", connection);
            command.Parameters.AddWithValue("@backupPath", backupPath);
            command.ExecuteNonQuery();

            _currentSlot = (_currentSlot + 1) % MaxBackups;

            Debug.WriteLine($"Database backup created: {backupPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Database backup failed: {ex.Message}");
        }
    }
}
