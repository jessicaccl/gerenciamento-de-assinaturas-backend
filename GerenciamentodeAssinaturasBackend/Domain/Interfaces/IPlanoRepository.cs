using GerenciamentodeAssinaturasBackend.Domain.Entities;

namespace GerenciamentodeAssinaturasBackend.Domain.Interfaces
{
    public interface IPlanoRepository
    {        
        Task<IEnumerable<Plano>> GetAllAsync(); 
        Task<Plano> GetByIdAsync(int id);       
        Task<int> CreateAsync(Plano plano);     
        Task<int> UpdateAsync(Plano plano);     
        Task<int> DeleteAsync(int id);         
    }
}