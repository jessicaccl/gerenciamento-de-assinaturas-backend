using AssinaMais.Api.Domain.Entities;
using AssinaMais.Api.Application.DTOs; 
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AssinaMais.Api.Domain.Interfaces
{
    public interface IAssinaturaRepository
    {
        Task<int> CriarAssinaturaAsync(CreateAssinaturaDto assinaturaDto);

        Task<PaginaDe<Assinatura>> ListarAsync(string? status, int? planoId, PaginacaoDto paginacao);

        Task AtualizarAssinaturaAsync(UpdateAssinaturaDto dto); 
        Task InativarAssinaturaAsync(int id);
        Task ReativarAssinaturaAsync(int id);
        Task<bool> ExisteAssinaturaParaPlanoAsync(int planoId);
    }
}