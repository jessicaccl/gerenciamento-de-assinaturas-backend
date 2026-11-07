using Dapper;
using AssinaMais.Api.Domain.Entities;
using AssinaMais.Api.Domain.Interfaces;
using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Infrastructure.Data;
using AssinaMais.Api.Domain.Services;
using System.Data;

namespace AssinaMais.Api.Infrastructure.Repositories
{
    public class AssinaturaRepository : IAssinaturaRepository
    {
        private readonly DapperContext _context;

        public AssinaturaRepository(DapperContext context)
        {
            _context = context;
        }

        private IDbConnection CreateConnection()
        {
            return _context.CreateConnection();
        }

        public async Task<int> CriarAssinaturaAsync(CreateAssinaturaDto dto)
        {
            var dataInicio = DateTime.Now;
            var dataExpiracao = RegrasDeCobranca.FimDoPeriodo(dataInicio, dto.TipoRecorrencia!);

            var sql = @"
                INSERT INTO Assinaturas 
                (PlanoId, ClienteId, TipoRecorrencia, FormaPagamento, DataInicio, DataExpiracao, Status)
                VALUES
                (@PlanoId, @ClienteId, @TipoRecorrencia, @FormaPagamento, @DataInicio, @DataExpiracao, 'Ativo')
                RETURNING Id;";

            using (var conn = CreateConnection())
            {
                return await conn.QuerySingleAsync<int>(sql, new
                {
                    dto.PlanoId,
                    dto.ClienteId,
                    dto.TipoRecorrencia,
                    dto.FormaPagamento,
                    DataInicio = dataInicio,
                    DataExpiracao = dataExpiracao
                });
            }
        }

        public async Task<PaginaDe<Assinatura>> ListarAsync(string? status, int? planoId, PaginacaoDto paginacao)
        {
            const string origem = @"
                FROM Assinaturas a
                JOIN Clientes cl ON cl.Id = a.ClienteId
                WHERE (@Status::text IS NULL OR a.Status = @Status)
                  AND (@PlanoId::int IS NULL OR a.PlanoId = @PlanoId)";

            var consulta = $@"
                SELECT COUNT(*) {origem};
                SELECT a.*, cl.Nome AS ClienteNome, cl.Email AS ClienteEmail {origem}
                ORDER BY cl.Nome, a.Id
                LIMIT @Limite OFFSET @Deslocamento;";

            using var conn = CreateConnection();
            using var resultado = await conn.QueryMultipleAsync(consulta, new
            {
                Status = string.IsNullOrWhiteSpace(status) ? null : status,
                PlanoId = planoId,
                paginacao.Limite,
                paginacao.Deslocamento
            });

            var total = await resultado.ReadSingleAsync<int>();
            return new PaginaDe<Assinatura>((await resultado.ReadAsync<Assinatura>()).ToList(), total);
        }

        public async Task AtualizarAssinaturaAsync(UpdateAssinaturaDto dto)
        {

            var sql = @"
                UPDATE Assinaturas 
                SET PlanoId = @PlanoId,
                TipoRecorrencia = @TipoRecorrencia,
                FormaPagamento = @FormaPagamento
                WHERE Id = @Id";

            using (var conn = CreateConnection())
            {
                await conn.ExecuteAsync(sql, dto);
            }
        }

        public async Task InativarAssinaturaAsync(int id)
        {
            var sql = "UPDATE Assinaturas SET Status = 'Inativo' WHERE Id = @Id";

            using (var conn = CreateConnection())
            {
                await conn.ExecuteAsync(sql, new { Id = id });
            }
        }

        public async Task ReativarAssinaturaAsync(int id)
        {
            var sql = "UPDATE Assinaturas SET Status = 'Ativo' WHERE Id = @Id";

            using (var conn = CreateConnection())
            {
                await conn.ExecuteAsync(sql, new { Id = id });
            }
        }

        public async Task<bool> ExisteAssinaturaParaPlanoAsync(int planoId)
        {
            var sql = "SELECT COUNT(1) FROM Assinaturas WHERE PlanoId = @PlanoId";

            using (var conn = CreateConnection())
            {
                var count = await conn.ExecuteScalarAsync<int>(sql, new { PlanoId = planoId });
                return count > 0;
            }
        }

    }
}