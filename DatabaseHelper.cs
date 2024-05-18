using Microsoft.Data.Sqlite;
using System;
using System.IO;

public static class DatabaseHelper
{
    private static string dbFile = "SecLinkApp.db";
    private static string connectionString = $"Data Source={dbFile}";
    private static SqliteConnection connection;
    private static SqliteTransaction transaction;
    private static SqliteCommand command;
    private static SqliteDataReader reader;

    public static void ResetDatabase()
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM UserSettings;";
            command.ExecuteNonQuery();
        }
    }

    public static void InitializeDatabase()
    {
        // Ensure the database file exists. If not, it will be created automatically.
        if (!File.Exists(dbFile))
        {
            
            Console.WriteLine("Database file not found. Creating new database file.");
        }

        // Open a connection to the SQLite database
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            // Create a command to execute the SQL statement for creating the UserSettings table
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS UserSettings (
                    Username TEXT PRIMARY KEY,
                    DefaultDirectory TEXT NOT NULL DEFAULT ''
                );";

            // Execute the command
            command.ExecuteNonQuery();
        }
    }

    public static void SaveUserSettings(string username, string defaultDirectory)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
        INSERT INTO UserSettings (Username, DefaultDirectory)
        VALUES ($username, $defaultDirectory)
        ON CONFLICT(Username) DO UPDATE SET
            DefaultDirectory = EXCLUDED.DefaultDirectory;";

            
            command.Parameters.AddWithValue("$username", username ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$defaultDirectory", defaultDirectory ?? "");

            command.ExecuteNonQuery();
        }
    }

    // Retrieve the username from the database
    public static string GetUsername()
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Username FROM UserSettings LIMIT 1;";

            return (string)command.ExecuteScalar();
        }
    }

    // Retrieve the default directory from the database
    public static string GetDefaultDirectory()
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT DefaultDirectory FROM UserSettings LIMIT 1;";

            var result = command.ExecuteScalar();
            if (Convert.IsDBNull(result))
            {
                return null; // or return a default path (we can add if we want)
            }
            else
            {
                return (string)result;
            }
        }
    }


}
