using Dapper;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly DapperContext _context;

        public UsuarioRepository(DapperContext context)
        {
            _context = context;
        }

        public async Task<Usuario> GetUsuarioByEmailAsync(string email)
        {
            var query = "SELECT * FROM Usuarios WHERE Email = @Email";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<Usuario>(query, new { Email = email });
            }
        }

        public async Task<IEnumerable<Usuario>> GetAllAsync()
        {
            var query = "SELECT * FROM Usuarios";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QueryAsync<Usuario>(query);
            }
        }

        public async Task<Usuario> GetByIdAsync(int id)
        {
            var query = "SELECT * FROM Usuarios WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleOrDefaultAsync<Usuario>(query, new { Id = id });
            }
        }
               

        public async Task<int> CreateAsync(Usuario usuario)
        {
            var query = @"
                INSERT INTO Usuarios (
                    Nome, Email, SenhaHash, CPF, Cargo, DataCriacao, 
                    EmailConfirmado, TokenConfirmacao
                ) 
                VALUES (
                    @Nome, @Email, @SenhaHash, @CPF, @Cargo, @DataCriacao, 
                    @EmailConfirmado, @TokenConfirmacao
                ) 
                RETURNING Id";

            var parameters = new DynamicParameters();
            parameters.Add("@Nome", usuario.Nome);
            parameters.Add("@Email", usuario.Email);
            parameters.Add("@SenhaHash", usuario.SenhaHash);
            parameters.Add("@CPF", usuario.CPF);
            parameters.Add("@Cargo", usuario.Cargo);
            parameters.Add("@DataCriacao", usuario.DataCriacao);

            parameters.Add("@EmailConfirmado", usuario.EmailConfirmado);            
            parameters.Add("@TokenConfirmacao", usuario.TokenConfirmacao);

            using (var connection = _context.CreateConnection())
            {
                return await connection.QuerySingleAsync<int>(query, parameters);
            }
        }

        public async Task UpdateAsync(Usuario usuario)
        {
            var query = @"UPDATE Usuarios 
                          SET Nome = @Nome, 
                              Email = @Email, 
                              SenhaHash = @SenhaHash, 
                              Cargo = @Cargo,
                              EmailConfirmado = @EmailConfirmado
                          WHERE Id = @Id";

            using (var connection = _context.CreateConnection())
            {
                await connection.ExecuteAsync(query, usuario);
            }
        }

        public async Task DeleteAsync(int id)
        {
            var query = "DELETE FROM Usuarios WHERE Id = @Id";
            using (var connection = _context.CreateConnection())
            {
                await connection.ExecuteAsync(query, new { Id = id });
            }
        }

        public Task<Usuario?> GetByTokenAsync(string token)
        {
            throw new NotImplementedException();
        }
    }
}