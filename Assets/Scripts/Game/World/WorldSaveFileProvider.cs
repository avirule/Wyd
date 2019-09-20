#region

using System;
using System.IO;
using System.Threading.Tasks;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Game.World
{
    public class WorldSaveFileProvider : IDisposable
    {
        private const string DEFAULT_CREATION_SQL_QUERY =
            @"
                 BEGIN TRANSACTION;

                 CREATE TABLE IF NOT EXISTS world_lookups (guid TEXT NOT NULL PRIMARY KEY, coordinates TEXT NOT NULL);
                 CREATE TABLE IF NOT EXISTS world_chunks (guid TEXT NOT NULL PRIMARY KEY, chunk_data TEXT NOT NULL);

                 COMMIT;
            ";

        public static string WorldSaveFileDirectory { get; }

        static WorldSaveFileProvider()
        {
            WorldSaveFileDirectory = $@"{Application.persistentDataPath}\Saves\\";
        }

        private string WorldFilePath { get; }
        private string ConnectionString { get; }

        public string WorldName { get; set; }


        public WorldSaveFileProvider(string worldName)
        {
            WorldName = worldName;

            WorldFilePath = $"{WorldSaveFileDirectory}\\{WorldName}.db";
            ConnectionString = $"Data Source={WorldFilePath};Version=3;";
        }

        public async Task<bool> Initialise()
        {
            try
            {
                if (!Directory.Exists(WorldSaveFileDirectory))
                {
                    Directory.CreateDirectory(WorldSaveFileDirectory);
                }

                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    await connection.OpenAsync();

                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.CommandText = DEFAULT_CREATION_SQL_QUERY;
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.Logger.Log(LogLevel.Warn, $"Failed to load world save: {ex.Message}");
                return false;
            }

            return true;
        }

        public void Dispose()
        {
            using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
            {
                connection.OpenAsync();

                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.CommandText = "VACUUM;";
                    command.ExecuteNonQuery();
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
