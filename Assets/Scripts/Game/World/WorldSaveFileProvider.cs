#region

using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using Extensions;
using Jobs;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Game.World
{
    public class WorldSaveFileProvider : IDisposable
    {
        private const int MAXIMUM_QUERY_RETRIES = 5;

        private const string DEFAULT_CREATION_SQL_QUERY =
            @"
                 BEGIN TRANSACTION;

                 CREATE TABLE IF NOT EXISTS world_data (coordinates TEXT NOT NULL PRIMARY KEY, chunk_data BLOB);

                 COMMIT;
            ";

        private static readonly JobQueue QueryExecutionQueue;

        public static string WorldSaveFileDirectory { get; }

        static WorldSaveFileProvider()
        {
            WorldSaveFileDirectory = $@"{Application.persistentDataPath}\Saves\\";

            QueryExecutionQueue = new JobQueue(200, () => ThreadingMode.Single);
            QueryExecutionQueue.Start();
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

        public bool CheckEntryExistsForPosition(Vector3 position)
        {
            using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
            {
                connection.Open();

                using (SQLiteCommand command = new SQLiteCommand(connection))
                {
                    command.Parameters.AddWithValue("@position", position.ToString());
                    command.CommandText = @"SELECT EXISTS (SELECT 1 FROM world_data WHERE coordinates=@position);";
                    object result = command.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
            }
        }

        public bool TryGetSavedDataFromPosition(Vector3 position, out byte[] data)
        {
            data = null;

            try
            {
                using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                {
                    connection.Open();

                    using (SQLiteCommand command = new SQLiteCommand(connection))
                    {
                        command.Parameters.AddWithValue("@position", position.ToString());
                        command.CommandText = @"SELECT chunk_data FROM world_data WHERE coordinates=@position;";

                        using (SQLiteDataReader reader = command.ExecuteReader(CommandBehavior.KeyInfo))
                        {
                            if (reader.Read())
                            {
                                SQLiteBlob blob = reader.GetBlob(0, true);

                                int count = blob.GetCount();
                                data = new byte[count];

                                blob.Read(data, count, 0);
                                return true;
                            }

                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to retrieve chunk data at position {position}: {ex.Message}");
            }

            return false;
        }


        #region COMPRESS / COMMIT

        public void CompressAndCommit(Vector3 position, byte[] data)
        {
            Commit(position, data.Compress());
        }

        public void CompressAndCommitThreaded(Vector3 position, byte[] data)
        {
            // todo cache the jobs
            QueryExecutionQueue.QueueJob(new GeneralExecutionJob(() => CompressAndCommit(position, data)));
        }

        public void Commit(Vector3 position, byte[] chunkData)
        {
            int tries = 0;

            while (tries < (MAXIMUM_QUERY_RETRIES + 1))
            {
                try
                {
                    using (SQLiteConnection connection = new SQLiteConnection(ConnectionString))
                    {
                        connection.Open();

                        using (SQLiteCommand command = new SQLiteCommand(connection))
                        {
                            command.Parameters.AddWithValue("@position", position.ToString());
                            command.Parameters.AddWithValue("@chunkData", chunkData);

                            command.CommandText =
                                @"INSERT OR IGNORE INTO world_data (coordinates, chunk_data) VALUES (@position, '');";
                            command.ExecuteNonQuery();

                            command.CommandText =
                                @"UPDATE world_data SET chunk_data=@chunkData WHERE coordinates=@position";
                            command.ExecuteNonQuery();

                            tries = int.MaxValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    tries += 1;

                    if (tries == MAXIMUM_QUERY_RETRIES)
                    {
                        EventLog.Logger.Log(LogLevel.Warn, $"Failed to commit chunk data: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        public static void ApplicationQuit()
        {
            QueryExecutionQueue?.Abort();
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
