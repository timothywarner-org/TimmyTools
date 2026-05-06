namespace TimmyTools.Core.Configurations;

public class DatabaseConfiguration
{
    public const string DatabaseFileName = "timmy_tools.sqlite";
    private const string OldDatabaseFileName = "pinny_notes.sqlite";

    public readonly string DataPath;
    public readonly string ConnectionString;

    public string DatabaseFilePath => Path.Combine(DataPath, DatabaseFileName);

    public DatabaseConfiguration()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string oldAppDataDir = Path.Combine(appDataPath, "Pinny Notes");
        string newAppDataDir = Path.Combine(appDataPath, "Timmy Tools");

        // 1. Migrate AppData folder if it exists and new one doesn't
        if (Directory.Exists(oldAppDataDir) && !Directory.Exists(newAppDataDir))
        {
            try { Directory.Move(oldAppDataDir, newAppDataDir); } catch { /* Best effort */ }
        }

        // Use exe dir for database if in Debug mode or is portable.
        if (System.Diagnostics.Debugger.IsAttached || File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.txt")))
            DataPath = AppContext.BaseDirectory;
        else
            DataPath = newAppDataDir;

        if (!Path.Exists(DataPath))
            Directory.CreateDirectory(DataPath);

        // 2. Migrate database file name if old name exists (Check both DataPath and old path in case folder move failed)
        string oldDbPathInNewDir = Path.Combine(DataPath, OldDatabaseFileName);
        string oldDbPathInOldDir = Path.Combine(oldAppDataDir, OldDatabaseFileName);
        string newDbPath = Path.Combine(DataPath, DatabaseFileName);

        if (File.Exists(oldDbPathInNewDir) && !File.Exists(newDbPath))
        {
            try { File.Move(oldDbPathInNewDir, newDbPath); } catch { /* Best effort */ }
        }
        else if (File.Exists(oldDbPathInOldDir) && !File.Exists(newDbPath))
        {
            try { File.Move(oldDbPathInOldDir, newDbPath); } catch { /* Best effort */ }
        }

        // 3. Migrate backup files if they exist (Check 'backups' subfolder and root DataPath)
        MigrateBackups(DataPath);
        MigrateBackups(Path.Combine(DataPath, "backups"));
        if (Directory.Exists(oldAppDataDir))
        {
            MigrateBackups(oldAppDataDir);
            MigrateBackups(Path.Combine(oldAppDataDir, "backups"));
        }

        ConnectionString = $"Data Source={newDbPath}";
    }

    private static void MigrateBackups(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return;

            foreach (string file in Directory.GetFiles(directory, "pinny_notes_backup_*.sqlite"))
            {
                string fileName = Path.GetFileName(file);
                string newFileName = fileName.Replace("pinny_notes_backup_", "timmy_tools_backup_");
                string newPath = Path.Combine(directory, newFileName);

                if (!File.Exists(newPath))
                    File.Move(file, newPath);
            }
        }
        catch { /* Best effort */ }
    }
}
