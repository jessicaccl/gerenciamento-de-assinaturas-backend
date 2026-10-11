using Dapper;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;
using System.Data;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Repositories
{
    public class PlanoRepository : IPlanoRepository
    {
        private readonly DapperContext _context;

        public PlanoRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Plano>> GetAllAsync()
        {
            var query = "SELECT * FROM Planos";

            using (var connection = _context.CreateConnection())
            {                
                var planos = await connection.QueryAsync<Plano>(query);
                return planos.ToList();
            }
        }

        public async Task<Plano> GetByIdAsync(int id)
        {
            var query = "SELECT * FROM Planos WHERE Id = @Id";

            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<Plano>(query, new { Id = id });
            }
        }

        public async Task<int> CreateAsync(Plano plano)
        {
            var query = "INSERT INTO Planos (Nome, Valor, Descricao, Ativo) VALUES (@Nome, @Valor, @Descricao, @Ativo) RETURNING Id";

            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleAsync<int>(query, plano);
            }
        }

        public async Task<int> UpdateAsync(Plano plano)
        {
            var query = "UPDATE Planos SET Nome = @Nome, Valor = @Valor, Descricao = @Descricao, Ativo = @Ativo WHERE Id = @Id";

            using (var connection = _context.CreateConnection())
            {
                return await connection.ExecuteAsync(query, plano);
            }
        }

        public async Task<int> DeleteAsync(int id)
        {
            var query = "DELETE FROM Planos WHERE Id = @Id";

            using (var connection = _context.CreateConnection())
            {
                return await connection.ExecuteAsync(query, new { Id = id });
            }
        }
    }
}