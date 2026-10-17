using Dapper;
using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Repositories
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly DapperContext _context;

        public ClienteRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ClienteComTotais>> ListarComTotaisAsync(DateTime hoje, int? clienteId = null)
        {
            // Cobranças pendentes com vencimento já passado contam como vencidas (mesma regra da listagem de cobranças)
            var sql = @"
                SELECT cl.Id, cl.Nome, cl.Email, cl.Documento, cl.Telefone, cl.DataCadastro,
                       COALESCE(SUM(c.Valor) FILTER (WHERE c.Status = 'Pendente' AND c.DataVencimento >= @Hoje), 0) AS ValorPendente,
                       COALESCE(SUM(c.Valor) FILTER (WHERE c.Status = 'Vencida' OR (c.Status = 'Pendente' AND c.DataVencimento < @Hoje)), 0) AS ValorVencido,
                       COUNT(c.Id) FILTER (WHERE c.Status = 'Pendente' AND c.DataVencimento >= @Hoje) AS QuantidadePendente,
                       COUNT(c.Id) FILTER (WHERE c.Status = 'Vencida' OR (c.Status = 'Pendente' AND c.DataVencimento < @Hoje)) AS QuantidadeVencida
                FROM Clientes cl
                LEFT JOIN Assinaturas a ON a.ClienteId = cl.Id
                LEFT JOIN Cobrancas c ON c.AssinaturaId = a.Id
                WHERE (@ClienteId::int IS NULL OR cl.Id = @ClienteId)
                GROUP BY cl.Id
                ORDER BY cl.Nome";

            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<ClienteComTotais>(sql, new { Hoje = hoje.Date, ClienteId = clienteId });
        }

        public async Task<IEnumerable<(int ClienteId, decimal ValorMensalPlano, string TipoRecorrencia)>> ListarAssinaturasAtivasAsync(int? clienteId = null)
        {
            const string sql = @"
                SELECT a.ClienteId, p.Valor AS ValorMensalPlano, a.TipoRecorrencia
                FROM Assinaturas a
                JOIN Planos p ON p.Id = a.PlanoId
                WHERE a.Status = 'Ativo' AND (@ClienteId::int IS NULL OR a.ClienteId = @ClienteId)";

            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<(int, decimal, string)>(sql, new { ClienteId = clienteId });
        }

        public async Task<IEnumerable<AssinaturaDoClienteDto>> ListarAssinaturasAsync(int clienteId)
        {
            const string sql = @"
                SELECT a.Id, p.Nome AS PlanoNome, a.TipoRecorrencia, a.FormaPagamento, a.Status, a.DataInicio, a.DataExpiracao
                FROM Assinaturas a
                JOIN Planos p ON p.Id = a.PlanoId
                WHERE a.ClienteId = @ClienteId
                ORDER BY a.Status, a.DataInicio DESC";

            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<AssinaturaDoClienteDto>(sql, new { ClienteId = clienteId });
        }

        public async Task<Cliente?> ObterPorIdAsync(int id)
        {
            using var conn = _context.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Cliente>("SELECT * FROM Clientes WHERE Id = @Id", new { Id = id });
        }

        public async Task<Cliente?> ObterPorDocumentoAsync(string documento)
        {
            using var conn = _context.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Cliente>("SELECT * FROM Clientes WHERE Documento = @Documento", new { Documento = documento });
        }

        public async Task<int> CriarAsync(Cliente cliente)
        {
            const string sql = @"
                INSERT INTO Clientes (Nome, Email, Documento, Telefone)
                VALUES (@Nome, @Email, @Documento, @Telefone)
                RETURNING Id";

            using var conn = _context.CreateConnection();
            return await conn.QuerySingleAsync<int>(sql, cliente);
        }

        public async Task AtualizarAsync(Cliente cliente)
        {
            const string sql = @"
                UPDATE Clientes
                SET Nome = @Nome, Email = @Email, Documento = @Documento, Telefone = @Telefone
                WHERE Id = @Id";

            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync(sql, cliente);
        }

        public async Task<bool> PossuiAssinaturasAsync(int id)
        {
            using var conn = _context.CreateConnection();
            return await conn.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM Assinaturas WHERE ClienteId = @Id)", new { Id = id });
        }

        public async Task ExcluirAsync(int id)
        {
            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync("DELETE FROM Clientes WHERE Id = @Id", new { Id = id });
        }
    }
}
