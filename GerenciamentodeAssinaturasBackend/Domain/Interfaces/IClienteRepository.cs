using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;

namespace GerenciamentodeAssinaturasBackend.Domain.Interfaces
{
    public interface IClienteRepository
    {
        Task<IEnumerable<ClienteComTotais>> ListarComTotaisAsync(DateTime hoje, int? clienteId = null);
        Task<IEnumerable<(int ClienteId, decimal ValorMensalPlano, string TipoRecorrencia)>> ListarAssinaturasAtivasAsync(int? clienteId = null);
        Task<IEnumerable<AssinaturaDoClienteDto>> ListarAssinaturasAsync(int clienteId);

        Task<Cliente?> ObterPorIdAsync(int id);
        Task<Cliente?> ObterPorDocumentoAsync(string documento);

        Task<int> CriarAsync(Cliente cliente);
        Task AtualizarAsync(Cliente cliente);
        Task<bool> PossuiAssinaturasAsync(int id);
        Task ExcluirAsync(int id);
    }
}
