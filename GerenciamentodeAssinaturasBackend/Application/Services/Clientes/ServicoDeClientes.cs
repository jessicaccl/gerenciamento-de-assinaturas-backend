using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Clientes
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

        public async Task<IEnumerable<ClienteResumoDto>> ListarAsync(DateTime hoje, string? busca, string? situacao)
        {
            var totais = await _clienteRepository.ListarComTotaisAsync(hoje);
            var ativas = (await _clienteRepository.ListarAssinaturasAtivasAsync()).ToList();

            var resumos = totais.Select(t => MontarResumo(t, ativas, new ClienteResumoDto()));

            if (!string.IsNullOrWhiteSpace(busca))
                resumos = resumos.Where(r => CorrespondeABusca(r, totais.First(t => t.Id == r.Id), busca));

            if (!string.IsNullOrWhiteSpace(situacao))
                resumos = resumos.Where(r => r.Situacao == situacao);

            return resumos.ToList();
        }

        public async Task<FichaClienteDto?> ObterFichaAsync(int id, DateTime hoje)
        {
            var totais = (await _clienteRepository.ListarComTotaisAsync(hoje, id)).SingleOrDefault();
            if (totais == null) return null;

            var ativas = (await _clienteRepository.ListarAssinaturasAtivasAsync(id)).ToList();
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

        // Busca por nome ou e-mail (sem diferenciar maiúsculas) ou pelos dígitos do documento
        private static bool CorrespondeABusca(ClienteResumoDto resumo, ClienteComTotais dados, string busca)
        {
            var termo = busca.Trim();
            var digitos = Documento.ApenasDigitos(termo);

            return resumo.Nome.Contains(termo, StringComparison.OrdinalIgnoreCase)
                || (resumo.Email?.Contains(termo, StringComparison.OrdinalIgnoreCase) ?? false)
                || (digitos.Length >= 3 && (dados.Documento?.Contains(digitos) ?? false));
        }
    }
}
