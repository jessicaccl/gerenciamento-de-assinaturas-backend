using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Entities;
using AssinaMais.Api.Domain.Interfaces;
using AssinaMais.Api.Domain.Services;

namespace AssinaMais.Api.Application.Services.Clientes
{
    public class ServicoDeClientes
    {
        private readonly IClienteRepository _clienteRepository;
        private readonly ICobrancaRepository _cobrancaRepository;

        public ServicoDeClientes(IClienteRepository clienteRepository, ICobrancaRepository cobrancaRepository)
        {
            _clienteRepository = clienteRepository;
            _cobrancaRepository = cobrancaRepository;
        }

        public async Task<PaginaDe<ClienteResumoDto>> ListarAsync(DateTime hoje, string? busca, string? situacao, PaginacaoDto paginacao)
        {
            var pagina = await _clienteRepository.ListarComTotaisAsync(hoje, new FiltroClientesDto { Busca = busca, Situacao = situacao }, paginacao);

            // O MRR só é calculado para os clientes da página
            var ativas = (await _clienteRepository.ListarAssinaturasAtivasAsync(pagina.Itens.Select(c => c.Id))).ToList();

            return new PaginaDe<ClienteResumoDto>(
                pagina.Itens.Select(t => MontarResumo(t, ativas, new ClienteResumoDto())).ToList(),
                pagina.Total);
        }

        public async Task<FichaClienteDto?> ObterFichaAsync(int id, DateTime hoje)
        {
            var totais = (await _clienteRepository.ListarComTotaisAsync(hoje, new FiltroClientesDto { ClienteId = id }, new PaginacaoDto())).Itens.SingleOrDefault();
            if (totais == null) return null;

            var ativas = (await _clienteRepository.ListarAssinaturasAtivasAsync(new[] { id })).ToList();
            var ficha = MontarResumo(totais, ativas, new FichaClienteDto());

            ficha.Documento = string.IsNullOrEmpty(totais.Documento) ? null : Documento.Formatar(totais.Documento);
            ficha.Assinaturas = (await _clienteRepository.ListarAssinaturasAsync(id)).ToList();
            ficha.Cobrancas = (await _cobrancaRepository.ListarAsync(new FiltroCobrancasDto { ClienteId = id }, hoje))
                .Where(c => c.Status != StatusCobranca.Cancelada)
                .ToList();

            return ficha;
        }

        public static T MontarResumo<T>(
            ClienteComTotais t,
            IEnumerable<(int ClienteId, decimal ValorMensalPlano, string TipoRecorrencia)> assinaturasAtivas,
            T destino) where T : ClienteResumoDto
        {
            var doCliente = assinaturasAtivas
                .Where(a => a.ClienteId == t.Id && RegrasDeCobranca.EhRecorrenciaValida(a.TipoRecorrencia))
                .ToList();

            destino.Id = t.Id;
            destino.Nome = t.Nome;
            destino.Email = t.Email;
            destino.TipoDocumento = Documento.Tipo(t.Documento);
            destino.DocumentoMascarado = Documento.Mascarar(t.Documento);
            destino.Telefone = t.Telefone;
            destino.DataCadastro = t.DataCadastro;
            destino.Mrr = doCliente.Sum(a => RegrasDeCobranca.ValorMensalEquivalente(a.ValorMensalPlano, a.TipoRecorrencia));
            destino.EmAberto = t.ValorPendente + t.ValorVencido;
            destino.Vencido = t.ValorVencido;
            destino.AssinaturasAtivas = doCliente.Count;
            destino.Situacao = SituacaoCliente.Calcular(t.QuantidadeVencida, t.QuantidadePendente);
            return destino;
        }
    }
}
