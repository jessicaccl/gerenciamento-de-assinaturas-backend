using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas
{
    public class GeradorDeCobrancas
    {
        private readonly ICobrancaRepository _cobrancaRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<GeradorDeCobrancas> _logger;
        private readonly int _diasDeAntecedencia;

        public GeradorDeCobrancas(
            ICobrancaRepository cobrancaRepository,
            IRabbitMQService rabbitMQService,
            ILogger<GeradorDeCobrancas> logger,
            IConfiguration configuration)
        {
            _cobrancaRepository = cobrancaRepository;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
            // A cobrança do próximo ciclo é emitida alguns dias antes do vencimento
            _diasDeAntecedencia = configuration.GetValue("Cobrancas:DiasDeAntecedencia", 10);
        }

        // Gera as cobranças de todas as assinaturas ativas e marca como vencidas as que passaram da data
        public async Task<int> ProcessarAsync(DateTime hoje)
        {
            var geradas = 0;
            foreach (var assinatura in await _cobrancaRepository.ListarAssinaturasParaFaturamentoAsync())
                geradas += await GerarAsync(assinatura, hoje, recomecarEm: null);

            var vencidas = await _cobrancaRepository.MarcarVencidasAsync(hoje);

            if (geradas > 0 || vencidas > 0)
                _logger.LogInformation("[Cobranças] {Geradas} geradas, {Vencidas} marcadas como vencidas.", geradas, vencidas);

            return geradas;
        }

        // recomecarEm: usado na reativação, para não cobrar o período em que a assinatura ficou inativa
        public async Task<int> GerarParaAssinaturaAsync(int assinaturaId, DateTime hoje, DateTime? recomecarEm = null)
        {
            var assinatura = (await _cobrancaRepository.ListarAssinaturasParaFaturamentoAsync(assinaturaId)).SingleOrDefault();
            return assinatura == null ? 0 : await GerarAsync(assinatura, hoje, recomecarEm);
        }

        private async Task<int> GerarAsync(AssinaturaParaFaturamento assinatura, DateTime hoje, DateTime? recomecarEm)
        {
            if (!RegrasDeCobranca.EhRecorrenciaValida(assinatura.TipoRecorrencia))
            {
                _logger.LogWarning("[Cobranças] Assinatura {Id} com recorrência inválida: {Recorrencia}", assinatura.AssinaturaId, assinatura.TipoRecorrencia);
                return 0;
            }

            var proximoInicio = assinatura.UltimoPeriodoFim ?? assinatura.DataInicio.Date;
            if (recomecarEm.HasValue && proximoInicio < recomecarEm.Value.Date)
                proximoInicio = recomecarEm.Value.Date;

            var periodos = RegrasDeCobranca.PeriodosAGerar(
                proximoInicio, assinatura.TipoRecorrencia, hoje.AddDays(_diasDeAntecedencia), diaAncora: assinatura.DataInicio.Day);
            var valor = RegrasDeCobranca.CalcularValor(assinatura.ValorMensalPlano, assinatura.TipoRecorrencia);
            var geradas = 0;

            foreach (var periodo in periodos)
            {
                var id = await _cobrancaRepository.CriarSeNaoExistirAsync(new Cobranca
                {
                    AssinaturaId = assinatura.AssinaturaId,
                    PeriodoInicio = periodo.Inicio,
                    PeriodoFim = periodo.Fim,
                    DataVencimento = periodo.Inicio,
                    Valor = valor,
                    Status = StatusCobranca.Pendente
                });

                if (id == null) continue;
                geradas++;

                await _rabbitMQService.EnviarMensagemAsync(new
                {
                    Operacao = "GERADA",
                    Entidade = "Cobranca",
                    Id = id,
                    assinatura.AssinaturaId,
                    Valor = valor,
                    Vencimento = periodo.Inicio
                }, "cobrancas_eventos");
            }

            if (geradas > 0)
                await _cobrancaRepository.AtualizarExpiracaoDaAssinaturaAsync(assinatura.AssinaturaId);

            return geradas;
        }
    }
}
