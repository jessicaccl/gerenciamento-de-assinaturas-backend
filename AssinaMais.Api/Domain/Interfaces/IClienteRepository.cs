using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Entities;

namespace AssinaMais.Api.Domain.Interfaces
{
    public interface IClienteRepository
    {
        Task<PaginaDe<ClienteComTotais>> ListarComTotaisAsync(DateTime hoje, FiltroClientesDto filtro, PaginacaoDto paginacao);
        Task<IEnumerable<(int ClienteId, decimal ValorMensalPlano, string TipoRecorrencia)>> ListarAssinaturasAtivasAsync(IEnumerable<int> clienteIds);
        Task<IEnumerable<AssinaturaDoClienteDto>> ListarAssinaturasAsync(int clienteId);

        Task<Cliente?> ObterPorIdAsync(int id);
        Task<Cliente?> ObterPorDocumentoAsync(string documento);

        Task<int> CriarAsync(Cliente cliente);
        Task AtualizarAsync(Cliente cliente);
        Task<bool> PossuiAssinaturasAsync(int id);
        Task ExcluirAsync(int id);
    }
}
