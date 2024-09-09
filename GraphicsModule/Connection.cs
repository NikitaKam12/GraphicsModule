using Npgsql;
using System;
using System.Data;
using GraphicsModule.Properties;
using System.Configuration.Assemblies;
using System.Configuration;
using GraphicsModule;

public class Connection
{
    private readonly string _connectionString;
    public Connection() : this(ConnectionSettings.ConnectionString) { }

    public Connection(string connectionString)
    {
        _connectionString = connectionString;
    }

    public NpgsqlConnection GetConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public DataTable ExecuteQuery(string query)
    {
        using (var connection = GetConnection())
        {
            using (var command = new NpgsqlCommand(query, connection))
            {
                using (var adapter = new NpgsqlDataAdapter(command))
                {
                    DataTable result = new DataTable();
                    adapter.Fill(result);
                    return result;
                }
            }
        }
    }
}
