using AssinaMais.Api.Domain.Entities;

namespace AssinaMais.Api.Domain.Interfaces
{
    public interface IUsuarioRepository
    {
        Task<IEnumerable<Usuario>> GetAllAsync();
        Task<Usuario> GetByIdAsync(int id);

        Task<Usuario> GetUsuarioByEmailAsync(string email);

        Task<int> CreateAsync(Usuario usuario);
        Task UpdateAsync(Usuario usuario);
        Task DeleteAsync(int id);
        Task<Usuario?> GetByTokenAsync(string token); 
    }
}