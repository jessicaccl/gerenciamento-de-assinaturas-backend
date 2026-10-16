using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Provisoes
{
    // Monta os números do painel a partir das cobranças e das assinaturas ativas.
    // Função pura (recebe "hoje" como parâmetro), testada isoladamente em CalculadoraDoPainelTests.
    public static class CalculadoraDoPainel
    {
        public const int MesesDeHistorico = 2;
        private const int DiasDoHorizonteAReceber = 30;

        public static PainelDeProvisoesDto Calcular(
            IReadOnlyCollection<CobrancaResponseDto> cobrancas,
            IReadOnlyCollection<AssinaturaParaFaturamento> assinaturasAtivas,
            DateTime hoje,
            int mesesFuturos)
        {
            hoje = hoje.Date;
            var inicioDaJanela = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-MesesDeHistorico);
            var fimDaJanela = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(mesesFuturos).AddDays(-1);
            var limiteAReceber = hoje.AddDays(DiasDoHorizonteAReceber);

            var pendentes = cobrancas.Where(c => c.Status == StatusCobranca.Pendente).ToList();
            var vencidas = cobrancas.Where(c => c.Status == StatusCobranca.Vencida).ToList();
            var pagas = cobrancas.Where(c => c.Status == StatusCobranca.Paga && c.DataPagamento.HasValue).ToList();
            var projetadas = ProjetarCobrancas(assinaturasAtivas, hoje, fimDaJanela);

            var mrr = assinaturasAtivas.Sum(a => RegrasDeCobranca.ValorMensalEquivalente(a.ValorMensalPlano, a.TipoRecorrencia));

            var aReceberReais = pendentes.Where(c => c.DataVencimento.Date <= limiteAReceber).ToList();
            var aReceberProjetadas = projetadas.Where(p => p.Vencimento <= limiteAReceber).ToList();

            var aging = RegrasDeProvisao.Faixas.Select(faixa =>
            {
                var naFaixa = vencidas.Where(c => RegrasDeProvisao.Classificar(c.DiasEmAtraso) == faixa).ToList();
                return new FaixaDeAgingDto
                {
                    Faixa = faixa.Nome,
                    PercentualDeRisco = faixa.PercentualDeRisco,
                    Valor = naFaixa.Sum(c => c.Valor),
                    Quantidade = naFaixa.Count,
                    Provisao = naFaixa.Sum(c => RegrasDeProvisao.CalcularProvisao(c.Valor, c.DiasEmAtraso))
                };
            }).ToList();

            var vencido = vencidas.Sum(c => c.Valor);
            var provisao = aging.Sum(f => f.Provisao);

            return new PainelDeProvisoesDto
            {
                DataReferencia = hoje,
                Mrr = mrr,
                AssinaturasAtivas = assinaturasAtivas.Count,
                TicketMedio = assinaturasAtivas.Count == 0 ? 0 : Math.Round(mrr / assinaturasAtivas.Count, 2, MidpointRounding.AwayFromZero),
                AReceber30Dias = aReceberReais.Sum(c => c.Valor) + aReceberProjetadas.Sum(p => p.Valor),
                QuantidadeAReceber30Dias = aReceberReais.Count + aReceberProjetadas.Count,
                Vencido = vencido,
                QuantidadeVencida = vencidas.Count,
                ProvisaoInadimplencia = provisao,
                PercentualProvisionado = vencido == 0 ? 0 : Math.Round(provisao / vencido * 100, 1, MidpointRounding.AwayFromZero),
                RecebidoMesAtual = pagas.Where(c => MesmoMes(c.DataPagamento!.Value, hoje)).Sum(c => c.Valor),
                Aging = aging,
                Previsao = MontarPrevisao(inicioDaJanela, MesesDeHistorico + mesesFuturos, pendentes, vencidas, pagas, projetadas),
                ProximosVencimentos = pendentes
                    .OrderBy(c => c.DataVencimento).ThenBy(c => c.ClienteNome)
                    .Take(6)
                    .Select(c => new ProximoVencimentoDto
                    {
                        CobrancaId = c.Id,
                        ClienteNome = c.ClienteNome,
                        PlanoNome = c.PlanoNome,
                        DataVencimento = c.DataVencimento,
                        Valor = c.Valor
                    }).ToList()
            };
        }

        // Cobranças que ainda não existem no banco, mas vão existir: os próximos ciclos das assinaturas ativas
        public static IReadOnlyList<(DateTime Vencimento, decimal Valor)> ProjetarCobrancas(
            IEnumerable<AssinaturaParaFaturamento> assinaturasAtivas, DateTime hoje, DateTime ate)
        {
            var projetadas = new List<(DateTime, decimal)>();

            foreach (var a in assinaturasAtivas.Where(a => RegrasDeCobranca.EhRecorrenciaValida(a.TipoRecorrencia)))
            {
                var proximoInicio = a.UltimoPeriodoFim ?? a.DataInicio.Date;
                var valor = RegrasDeCobranca.CalcularValor(a.ValorMensalPlano, a.TipoRecorrencia);

                projetadas.AddRange(RegrasDeCobranca
                    .PeriodosAGerar(proximoInicio, a.TipoRecorrencia, ate, diaAncora: a.DataInicio.Day)
                    .Where(p => p.Inicio >= hoje.Date)
                    .Select(p => (p.Inicio, valor)));
            }

            return projetadas;
        }

        private static List<PrevisaoMensalDto> MontarPrevisao(
            DateTime inicio, int quantidadeDeMeses,
            List<CobrancaResponseDto> pendentes, List<CobrancaResponseDto> vencidas, List<CobrancaResponseDto> pagas,
            IReadOnlyList<(DateTime Vencimento, decimal Valor)> projetadas)
        {
            return Enumerable.Range(0, quantidadeDeMeses).Select(i =>
            {
                var mes = inicio.AddMonths(i);
                return new PrevisaoMensalDto
                {
                    Mes = mes.ToString("yyyy-MM"),
                    Recebido = pagas.Where(c => MesmoMes(c.DataPagamento!.Value, mes)).Sum(c => c.Valor),
                    Previsto = pendentes.Where(c => MesmoMes(c.DataVencimento, mes)).Sum(c => c.Valor)
                             + projetadas.Where(p => MesmoMes(p.Vencimento, mes)).Sum(p => p.Valor),
                    EmRisco = vencidas.Where(c => MesmoMes(c.DataVencimento, mes)).Sum(c => c.Valor)
                };
            }).ToList();
        }

        private static bool MesmoMes(DateTime data, DateTime mes) => data.Year == mes.Year && data.Month == mes.Month;
    }
}
