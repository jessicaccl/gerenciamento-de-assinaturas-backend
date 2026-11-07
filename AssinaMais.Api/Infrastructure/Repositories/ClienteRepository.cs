using Dapper;
using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Entities;
using AssinaMais.Api.Domain.Interfaces;
using AssinaMais.Api.Domain.Services;
using AssinaMais.Api.Infrastructure.Data;

namespace AssinaMais.Api.Infrastructure.Repositories
{
    public class ClienteRepository : IClienteRepository
    {
        private readonly DapperContext _context;

        public ClienteRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<PaginaDe<ClienteComTotais>> ListarComTotaisAsync(DateTime hoje, FiltroClientesDto filtro, PaginacaoDto paginacao)
        {
            // Cobranças pendentes com vencimento já passado contam como vencidas (mesma regra da listagem de cobranças).
            // O filtro de situação repete em SQL a regra de SituacaoCliente.Calcular, para poder paginar no banco.
            const string cte = @"
                WITH totais AS (
                SELECT cl.Id, cl.Nome, cl.Email, cl.Documento, cl.Telefone, cl.DataCadastro,
                       COALESCE(SUM(c.Valor) FILTER (WHERE c.Status = 'Pendente' AND c.DataVencimento >= @Hoje), 0) AS ValorPendente,
                       COALESCE(SUM(c.Valor) FILTER (WHERE c.Status = 'Vencida' OR (c.Status = 'Pendente' AND c.DataVencimento < @Hoje)), 0) AS ValorVencido,
                       COUNT(c.Id) FILTER (WHERE c.Status = 'Pendente' AND c.DataVencimento >= @Hoje) AS QuantidadePendente,
                       COUNT(c.Id) FILTER (WHERE c.Status = 'Vencida' OR (c.Status = 'Pendente' AND c.DataVencimento < @Hoje)) AS QuantidadeVencida
                FROM Clientes cl
                LEFT JOIN Assinaturas a ON a.ClienteId = cl.Id
                LEFT JOIN Cobrancas c ON c.AssinaturaId = a.Id
                WHERE (@ClienteId::int IS NULL OR cl.Id = @ClienteId)
                  AND (@Termo::text IS NULL
                       OR cl.Nome ILIKE @Termo
                       OR cl.Email ILIKE @Termo
                       OR (@Digitos::text IS NOT NULL AND cl.Documento LIKE @Digitos))
                GROUP BY cl.Id
                ),
                filtrados AS (
                    SELECT * FROM totais
                    WHERE @Situacao::text IS NULL
                       OR (@Situacao = 'Inadimplente' AND QuantidadeVencida > 0)
                       OR (@Situacao = 'A vencer' AND QuantidadeVencida = 0 AND QuantidadePendente > 0)
                       OR (@Situacao = 'Em dia' AND QuantidadeVencida = 0 AND QuantidadePendente = 0)
                )";

            var consulta = $@"
                {cte} SELECT COUNT(*) FROM filtrados;
                {cte} SELECT * FROM filtrados ORDER BY Nome LIMIT @Limite OFFSET @Deslocamento;";

            var termo = string.IsNullOrWhiteSpace(filtro.Busca) ? null : $"%{EscaparLike(filtro.Busca.Trim())}%";
            var digitos = Documento.ApenasDigitos(filtro.Busca);

            using var conn = _context.CreateConnection();
            using var resultado = await conn.QueryMultipleAsync(consulta, new
            {
                Hoje = hoje.Date,
                filtro.ClienteId,
                Termo = termo,
                // Busca por documento só a partir de 3 dígitos, para "1" não trazer todo mundo
                Digitos = digitos.Length >= 3 ? $"%{digitos}%" : null,
                filtro.Situacao,
                paginacao.Limite,
                paginacao.Deslocamento
            });

            var total = await resultado.ReadSingleAsync<int>();
            return new PaginaDe<ClienteComTotais>((await resultado.ReadAsync<ClienteComTotais>()).ToList(), total);
        }

        // % e _ digitados na busca são texto, não curingas do LIKE
        private static string EscaparLike(string texto) =>
            texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        public async Task<IEnumerable<(int ClienteId, decimal ValorMensalPlano, string TipoRecorrencia)>> ListarAssinaturasAtivasAsync(IEnumerable<int> clienteIds)
        {
            const string sql = @"
                SELECT a.ClienteId, p.Valor AS ValorMensalPlano, a.TipoRecorrencia
                FROM Assinaturas a
                JOIN Planos p ON p.Id = a.PlanoId
                WHERE a.Status = 'Ativo' AND a.ClienteId = ANY(@ClienteIds)";

            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<(int, decimal, string)>(sql, new { ClienteIds = clienteIds.ToArray() });
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
