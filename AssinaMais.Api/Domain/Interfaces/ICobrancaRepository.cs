using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Entities;

namespace AssinaMais.Api.Domain.Interfaces
{
    public interface ICobrancaRepository
    {
        Task<IEnumerable<CobrancaResponseDto>> ListarAsync(FiltroCobrancasDto filtro, DateTime hoje);
        Task<PaginaDe<CobrancaResponseDto>> ListarPaginadoAsync(FiltroCobrancasDto filtro, DateTime hoje, PaginacaoDto paginacao);
        Task<ResumoCobrancasDto> ObterResumoAsync(DateTime hoje);
        Task<Cobranca?> ObterPorIdAsync(int id);

        Task<IEnumerable<AssinaturaParaFaturamento>> ListarAssinaturasParaFaturamentoAsync(int? assinaturaId = null);

        // Retorna o Id criado, ou null se a cobrança do período já existia
        Task<int?> CriarSeNaoExistirAsync(Cobranca cobranca);
        Task AtualizarExpiracaoDaAssinaturaAsync(int assinaturaId);

        Task<int> MarcarVencidasAsync(DateTime hoje);
        Task<bool> RegistrarPagamentoAsync(int id, DateTime dataPagamento, string? formaPagamento);
        Task<bool> CancelarAsync(int id);

        // Retorna false quando o lembrete automático daquele tipo já tinha sido registrado
        Task<bool> RegistrarLembreteAsync(int cobrancaId, string tipo);
        Task<int> CancelarFuturasDaAssinaturaAsync(int assinaturaId, DateTime hoje);
    }
}
