using Dapper;
using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;
using System.Text;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Repositories
{
    public class CobrancaRepository : ICobrancaRepository
    {
        private readonly DapperContext _context;

        public CobrancaRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CobrancaResponseDto>> ListarAsync(FiltroCobrancasDto filtro, DateTime hoje)
        {
            // O status "Vencida" é calculado na consulta, então a listagem fica correta mesmo antes do worker rodar
            var sql = new StringBuilder(@"
                SELECT * FROM (
                    SELECT c.Id, c.AssinaturaId, a.ClienteId, cl.Nome AS ClienteNome, cl.Email AS ClienteEmail, p.Nome AS PlanoNome, a.TipoRecorrencia,
                           c.PeriodoInicio, c.PeriodoFim, c.DataVencimento, c.Valor,
                           CASE WHEN c.Status = 'Pendente' AND c.DataVencimento < @Hoje THEN 'Vencida' ELSE c.Status END AS Status,
                           c.DataPagamento, c.FormaPagamento,
                           (SELECT MAX(l.DataEnvio) FROM LembretesCobranca l WHERE l.CobrancaId = c.Id) AS UltimoLembrete
                    FROM Cobrancas c
                    JOIN Assinaturas a ON a.Id = c.AssinaturaId
                    JOIN Clientes cl ON cl.Id = a.ClienteId
                    JOIN Planos p ON p.Id = a.PlanoId
                ) AS cobrancas
                WHERE 1 = 1");

            if (!string.IsNullOrWhiteSpace(filtro.Status)) sql.Append(" AND Status = @Status");
            if (filtro.AssinaturaId.HasValue) sql.Append(" AND AssinaturaId = @AssinaturaId");
            if (filtro.ClienteId.HasValue) sql.Append(" AND ClienteId = @ClienteId");
            if (filtro.VencimentoDe.HasValue) sql.Append(" AND DataVencimento >= @VencimentoDe");
            if (filtro.VencimentoAte.HasValue) sql.Append(" AND DataVencimento <= @VencimentoAte");
            sql.Append(" ORDER BY DataVencimento DESC, Id DESC");

            using var conn = _context.CreateConnection();
            var cobrancas = await conn.QueryAsync<CobrancaResponseDto>(sql.ToString(), new
            {
                Hoje = hoje.Date,
                filtro.Status,
                filtro.AssinaturaId,
                filtro.ClienteId,
                VencimentoDe = filtro.VencimentoDe?.Date,
                VencimentoAte = filtro.VencimentoAte?.Date
            });

            foreach (var c in cobrancas.Where(c => c.Status == StatusCobranca.Vencida))
                c.DiasEmAtraso = (hoje.Date - c.DataVencimento.Date).Days;

            return cobrancas;
        }

        public async Task<Cobranca?> ObterPorIdAsync(int id)
        {
            using var conn = _context.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<Cobranca>("SELECT * FROM Cobrancas WHERE Id = @Id", new { Id = id });
        }

        public async Task<IEnumerable<AssinaturaParaFaturamento>> ListarAssinaturasParaFaturamentoAsync(int? assinaturaId = null)
        {
            var sql = @"
                SELECT a.Id AS AssinaturaId, a.TipoRecorrencia, a.DataInicio, p.Valor AS ValorMensalPlano,
                       (SELECT MAX(c.PeriodoFim) FROM Cobrancas c WHERE c.AssinaturaId = a.Id) AS UltimoPeriodoFim
                FROM Assinaturas a
                JOIN Planos p ON p.Id = a.PlanoId
                WHERE a.Status = 'Ativo'";

            if (assinaturaId.HasValue) sql += " AND a.Id = @AssinaturaId";

            using var conn = _context.CreateConnection();
            return await conn.QueryAsync<AssinaturaParaFaturamento>(sql, new { AssinaturaId = assinaturaId });
        }

        public async Task<int?> CriarSeNaoExistirAsync(Cobranca cobranca)
        {
            // A restrição única (AssinaturaId, PeriodoInicio) torna a geração idempotente, mesmo com execuções simultâneas
            const string sql = @"
                INSERT INTO Cobrancas (AssinaturaId, PeriodoInicio, PeriodoFim, DataVencimento, Valor, Status)
                VALUES (@AssinaturaId, @PeriodoInicio, @PeriodoFim, @DataVencimento, @Valor, @Status)
                ON CONFLICT (AssinaturaId, PeriodoInicio) DO NOTHING
                RETURNING Id";

            using var conn = _context.CreateConnection();
            return await conn.QuerySingleOrDefaultAsync<int?>(sql, cobranca);
        }

        public async Task AtualizarExpiracaoDaAssinaturaAsync(int assinaturaId)
        {
            const string sql = @"
                UPDATE Assinaturas
                SET DataExpiracao = (SELECT MAX(PeriodoFim) FROM Cobrancas WHERE AssinaturaId = @AssinaturaId AND Status <> 'Cancelada')
                WHERE Id = @AssinaturaId
                  AND EXISTS (SELECT 1 FROM Cobrancas WHERE AssinaturaId = @AssinaturaId AND Status <> 'Cancelada')";

            using var conn = _context.CreateConnection();
            await conn.ExecuteAsync(sql, new { AssinaturaId = assinaturaId });
        }

        public async Task<int> MarcarVencidasAsync(DateTime hoje)
        {
            using var conn = _context.CreateConnection();
            return await conn.ExecuteAsync(
                "UPDATE Cobrancas SET Status = 'Vencida' WHERE Status = 'Pendente' AND DataVencimento < @Hoje",
                new { Hoje = hoje.Date });
        }

        public async Task<bool> RegistrarPagamentoAsync(int id, DateTime dataPagamento, string? formaPagamento)
        {
            // Só paga o que está em aberto; a forma de pagamento padrão é a da assinatura
            const string sql = @"
                UPDATE Cobrancas c
                SET Status = 'Paga',
                    DataPagamento = @DataPagamento,
                    FormaPagamento = COALESCE(@FormaPagamento, a.FormaPagamento)
                FROM Assinaturas a
                WHERE c.Id = @Id AND a.Id = c.AssinaturaId AND c.Status IN ('Pendente', 'Vencida')";

            using var conn = _context.CreateConnection();
            return await conn.ExecuteAsync(sql, new { Id = id, DataPagamento = dataPagamento, FormaPagamento = formaPagamento }) > 0;
        }

        public async Task<bool> RegistrarLembreteAsync(int cobrancaId, string tipo)
        {
            // Lembretes automáticos são únicos por cobrança e tipo (índice parcial); os manuais podem se repetir
            const string sql = @"
                INSERT INTO LembretesCobranca (CobrancaId, Tipo)
                VALUES (@CobrancaId, @Tipo)
                ON CONFLICT (CobrancaId, Tipo) WHERE Tipo <> 'MANUAL' DO NOTHING";

            using var conn = _context.CreateConnection();
            return await conn.ExecuteAsync(sql, new { CobrancaId = cobrancaId, Tipo = tipo }) > 0;
        }

        public async Task<bool> CancelarAsync(int id)
        {
            using var conn = _context.CreateConnection();
            return await conn.ExecuteAsync(
                "UPDATE Cobrancas SET Status = 'Cancelada' WHERE Id = @Id AND Status IN ('Pendente', 'Vencida')",
                new { Id = id }) > 0;
        }

        public async Task<int> CancelarFuturasDaAssinaturaAsync(int assinaturaId, DateTime hoje)
        {
            // Ao cancelar uma assinatura, só caem as cobranças que ainda não venceram; dívidas já vencidas continuam valendo
            using var conn = _context.CreateConnection();
            return await conn.ExecuteAsync(
                "UPDATE Cobrancas SET Status = 'Cancelada' WHERE AssinaturaId = @AssinaturaId AND Status = 'Pendente' AND DataVencimento > @Hoje",
                new { AssinaturaId = assinaturaId, Hoje = hoje.Date });
        }
    }
}
