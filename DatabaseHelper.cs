using Microsoft.Data.Sqlite;
using System;

public static class DatabaseHelper
{
    private static string dbFile = "SecLinkApp.db";
    private static string connectionString = $"Data Source={dbFile}";

    public static void InitializeDatabase()
    {
        if (!System.IO.File.Exists(dbFile))
        {
            Console.WriteLine("Database file not found. Creating new database file.");
        }

        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
            CREATE TABLE IF NOT EXISTS UserSettings (
                Username TEXT PRIMARY KEY,
                DefaultDirectory TEXT NOT NULL DEFAULT ''
            );
            CREATE TABLE IF NOT EXISTS Files (
                HashValue TEXT PRIMARY KEY,
                EncryptedPath TEXT NOT NULL,
                IV TEXT NOT NULL,
                AuthLevel TEXT NOT NULL,
                UploadDate DATETIME DEFAULT CURRENT_TIMESTAMP,
                Timestamp TEXT NOT NULL
            );";
            command.ExecuteNonQuery();

            // Ensure Timestamp column exists in Files table
            command.CommandText = "PRAGMA table_info(Files);";
            using (var reader = command.ExecuteReader())
            {
                bool timestampExists = false;
                while (reader.Read())
                {
                    if (reader.GetString(1) == "Timestamp")
                    {
                        timestampExists = true;
                        break;
                    }
                }
                reader.Close(); // Close the reader before changing the CommandText

                if (!timestampExists)
                {
                    command.CommandText = "ALTER TABLE Files ADD COLUMN Timestamp TEXT NOT NULL DEFAULT '';";
                    command.ExecuteNonQuery();
                }
            }
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


            // Add parameters with null-check to ensure a DBNULL value is used if null
            command.Parameters.AddWithValue("$username", username ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$defaultDirectory", defaultDirectory ?? "");

            command.ExecuteNonQuery();
        }
    }


    public static void SaveFileMetadata(string hashValue, string encryptedPath, string iv, string authLevel, string timestamp)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO Files (HashValue, EncryptedPath, IV, AuthLevel, Timestamp)
            VALUES ($hashValue, $encryptedPath, $iv, $authLevel, $timestamp)
            ON CONFLICT(HashValue) DO UPDATE SET
                EncryptedPath = EXCLUDED.EncryptedPath,
                IV = EXCLUDED.IV,
                AuthLevel = EXCLUDED.AuthLevel,
                Timestamp = EXCLUDED.Timestamp;";
            command.Parameters.AddWithValue("$hashValue", hashValue);
            command.Parameters.AddWithValue("$encryptedPath", encryptedPath);
            command.Parameters.AddWithValue("$iv", iv);
            command.Parameters.AddWithValue("$authLevel", authLevel);
            command.Parameters.AddWithValue("$timestamp", timestamp);
            command.ExecuteNonQuery();
        }
    }

    public static (string EncryptedPath, string IV, string AuthLevel, string Timestamp) GetFileMetadataByHash(string hashValue)
    {
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT EncryptedPath, IV, AuthLevel, Timestamp
            FROM Files
            WHERE HashValue = $hashValue;";
            command.Parameters.AddWithValue("$hashValue", hashValue);

            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    return (
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)
                    );
                }
                else
                {
                    return (null, null, null, null);
                }
            }
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
                return null; // or return a default path if you have one
            }
            else
            {
                return (string)result;
            }
        }
    }

}
