using System.Data;
using Npgsql;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Data
{
    public class DapperContext
    {
        private readonly string _connectionString;

        public DapperContext(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure 'ConnectionStrings:DefaultConnection'.");

            _connectionString = Normalizar(connectionString);
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        // Aceita também o formato URL (postgresql://usuario:senha@host:porta/banco), usado por provedores como Neon e Render
        private static string Normalizar(string connectionString)
        {
            if (!connectionString.StartsWith("postgres://") && !connectionString.StartsWith("postgresql://"))
                return connectionString;

            var uri = new Uri(connectionString);
            var credenciais = uri.UserInfo.Split(':', 2);

            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.TrimStart('/'),
                Username = Uri.UnescapeDataString(credenciais[0]),
                Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null,
                // Provedores na nuvem exigem SSL; ?sslmode=disable permite testar com um banco local
                SslMode = uri.Query.Contains("sslmode=disable", StringComparison.OrdinalIgnoreCase) ? SslMode.Disable : SslMode.Require
            };

            return builder.ConnectionString;
        }
    }
}
