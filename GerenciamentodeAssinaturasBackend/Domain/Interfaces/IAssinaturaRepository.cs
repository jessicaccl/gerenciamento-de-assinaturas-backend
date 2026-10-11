using GerenciamentoAssinaturas.Domain.Entities;
using GerenciamentoAssinaturas.DTOs; 
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GerenciamentoAssinaturas.Domain.Interfaces
{
    public interface IAssinaturaRepository
    {
        Task<int> CriarAssinaturaAsync(CreateAssinaturaDto assinaturaDto);

        Task<IEnumerable<Assinatura>> ListarTodasAsync();

        Task AtualizarAssinaturaAsync(UpdateAssinaturaDto dto); 
        Task InativarAssinaturaAsync(int id);
        Task ReativarAssinaturaAsync(int id);
        Task<bool> ExisteAssinaturaParaPlanoAsync(int planoId);
    }
}