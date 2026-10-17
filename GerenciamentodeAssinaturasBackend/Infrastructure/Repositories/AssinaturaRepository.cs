using Dapper;
using GerenciamentoAssinaturas.Domain.Entities;
using GerenciamentoAssinaturas.Domain.Interfaces;
using GerenciamentoAssinaturas.DTOs;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;
using GerenciamentodeAssinaturasBackend.Domain.Services;
using System.Data;

namespace GerenciamentoAssinaturas.Infrastructure.Repositories
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
                (PlanoId, ClienteNome, ClienteEmail, TipoRecorrencia, FormaPagamento, DataInicio, DataExpiracao, Status)
                VALUES
                (@PlanoId, @ClienteNome, @ClienteEmail, @TipoRecorrencia, @FormaPagamento, @DataInicio, @DataExpiracao, 'Ativo')
                RETURNING Id;";

            using (var conn = CreateConnection())
            {
                return await conn.QuerySingleAsync<int>(sql, new
                {
                    dto.PlanoId,
                    dto.ClienteNome,
                    ClienteEmail = string.IsNullOrWhiteSpace(dto.ClienteEmail) ? null : dto.ClienteEmail.Trim(),
                    dto.TipoRecorrencia,
                    dto.FormaPagamento,
                    DataInicio = dataInicio,
                    DataExpiracao = dataExpiracao
                });
            }
        }

        public async Task<IEnumerable<Assinatura>> ListarTodasAsync()
        {
            var sql = "SELECT * FROM Assinaturas";
            using (var conn = CreateConnection())
            {
                return await conn.QueryAsync<Assinatura>(sql);
            }
        }

        public async Task AtualizarAssinaturaAsync(UpdateAssinaturaDto dto)
        {

            var sql = @"
                UPDATE Assinaturas 
                SET PlanoId = @PlanoId,
                ClienteEmail = NULLIF(TRIM(@ClienteEmail), ''),
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