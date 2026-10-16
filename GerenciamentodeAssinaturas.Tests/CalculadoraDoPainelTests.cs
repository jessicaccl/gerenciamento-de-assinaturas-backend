using FluentAssertions;
using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Services.Provisoes;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturas.Tests
{
    public class RegrasDeProvisaoTests
    {
        [Theory]
        [InlineData(0, null)]
        [InlineData(1, "1 a 30 dias")]
        [InlineData(30, "1 a 30 dias")]
        [InlineData(31, "31 a 60 dias")]
        [InlineData(90, "61 a 90 dias")]
        [InlineData(400, "Mais de 90 dias")]
        public void Classificar_DeveEncaixarNaFaixaCerta(int dias, string? faixaEsperada)
        {
            RegrasDeProvisao.Classificar(dias)?.Nome.Should().Be(faixaEsperada);
        }

        [Theory]
        [InlineData(100, 10, 30)]
        [InlineData(100, 45, 50)]
        [InlineData(100, 75, 80)]
        [InlineData(100, 120, 100)]
        [InlineData(100, 0, 0)]     // em dia: sem provisão
        public void CalcularProvisao_DeveAplicarPercentualDaFaixa(decimal valor, int dias, decimal esperado)
        {
            RegrasDeProvisao.CalcularProvisao(valor, dias).Should().Be(esperado);
        }
    }

    public class CalculadoraDoPainelTests
    {
        private static readonly DateTime Hoje = new(2026, 10, 16);

        [Fact]
        public void Mrr_DeveUsarValorMensalEquivalenteComDesconto()
        {
            var assinaturas = new[]
            {
                Assinatura(valorMensal: 100m, "Mensal"),   // 100,00
                Assinatura(valorMensal: 120m, "Anual"),    // 1200 / 12 = 100,00
            };

            var painel = CalculadoraDoPainel.Calcular(Array.Empty<CobrancaResponseDto>(), assinaturas, Hoje, mesesFuturos: 3);

            painel.Mrr.Should().Be(200m);
            painel.AssinaturasAtivas.Should().Be(2);
            painel.TicketMedio.Should().Be(100m);
        }

        [Fact]
        public void Provisao_DeveSomarCadaFaixaDeAtraso()
        {
            var cobrancas = new[]
            {
                Cobranca(StatusCobranca.Vencida, 100m, Hoje.AddDays(-10), diasEmAtraso: 10),   // 30%  -> 30
                Cobranca(StatusCobranca.Vencida, 200m, Hoje.AddDays(-45), diasEmAtraso: 45),   // 50%  -> 100
                Cobranca(StatusCobranca.Vencida, 50m, Hoje.AddDays(-100), diasEmAtraso: 100),  // 100% -> 50
                Cobranca(StatusCobranca.Paga, 999m, Hoje.AddDays(-5), pagoEm: Hoje.AddDays(-5)),
            };

            var painel = CalculadoraDoPainel.Calcular(cobrancas, Array.Empty<AssinaturaParaFaturamento>(), Hoje, 3);

            painel.Vencido.Should().Be(350m);
            painel.QuantidadeVencida.Should().Be(3);
            painel.ProvisaoInadimplencia.Should().Be(180m);
            painel.PercentualProvisionado.Should().Be(51.4m);
            painel.Aging.Single(f => f.Faixa == "31 a 60 dias").Valor.Should().Be(200m);
            painel.Aging.Single(f => f.Faixa == "61 a 90 dias").Quantidade.Should().Be(0);
        }

        [Fact]
        public void AReceber30Dias_DeveSomarPendentesEProximosCiclosProjetados()
        {
            var cobrancas = new[]
            {
                Cobranca(StatusCobranca.Pendente, 50m, Hoje.AddDays(5)),
                Cobranca(StatusCobranca.Pendente, 70m, Hoje.AddDays(40)),   // fora dos 30 dias
            };
            // Mensal cujo período atual termina daqui a 20 dias: o próximo ciclo ainda não existe no banco
            var assinaturas = new[] { Assinatura(80m, "Mensal", inicio: Hoje.AddDays(-10), ultimoPeriodoFim: Hoje.AddDays(20)) };

            var painel = CalculadoraDoPainel.Calcular(cobrancas, assinaturas, Hoje, 3);

            painel.AReceber30Dias.Should().Be(130m);
            painel.QuantidadeAReceber30Dias.Should().Be(2);
        }

        [Fact]
        public void Previsao_DeveSepararRecebidoPrevistoEEmRiscoPorMes()
        {
            var cobrancas = new[]
            {
                Cobranca(StatusCobranca.Paga, 100m, new DateTime(2026, 9, 10), pagoEm: new DateTime(2026, 9, 11)),
                Cobranca(StatusCobranca.Vencida, 40m, new DateTime(2026, 10, 2), diasEmAtraso: 14),
                Cobranca(StatusCobranca.Pendente, 60m, new DateTime(2026, 10, 25)),
            };

            var painel = CalculadoraDoPainel.Calcular(cobrancas, Array.Empty<AssinaturaParaFaturamento>(), Hoje, mesesFuturos: 3);

            // 2 meses de histórico + mês atual + 2 meses à frente
            painel.Previsao.Select(p => p.Mes).Should().Equal("2026-08", "2026-09", "2026-10", "2026-11", "2026-12");

            var setembro = painel.Previsao.Single(p => p.Mes == "2026-09");
            setembro.Recebido.Should().Be(100m);

            var outubro = painel.Previsao.Single(p => p.Mes == "2026-10");
            outubro.Previsto.Should().Be(60m);
            outubro.EmRisco.Should().Be(40m);
        }

        [Fact]
        public void ProjetarCobrancas_DeveGerarCiclosFuturosAteOFimDaJanela()
        {
            var assinatura = Assinatura(30m, "Mensal", inicio: new DateTime(2026, 9, 20), ultimoPeriodoFim: new DateTime(2026, 10, 20));

            var projetadas = CalculadoraDoPainel.ProjetarCobrancas(new[] { assinatura }, Hoje, ate: new DateTime(2026, 12, 31));

            projetadas.Select(p => p.Vencimento).Should().Equal(
                new DateTime(2026, 10, 20), new DateTime(2026, 11, 20), new DateTime(2026, 12, 20));
            projetadas.Should().OnlyContain(p => p.Valor == 30m);
        }

        private static AssinaturaParaFaturamento Assinatura(decimal valorMensal, string recorrencia, DateTime? inicio = null, DateTime? ultimoPeriodoFim = null) =>
            new()
            {
                AssinaturaId = 1,
                ValorMensalPlano = valorMensal,
                TipoRecorrencia = recorrencia,
                DataInicio = inicio ?? Hoje.AddDays(-5),
                UltimoPeriodoFim = ultimoPeriodoFim ?? Hoje.AddDays(300)
            };

        private static CobrancaResponseDto Cobranca(string status, decimal valor, DateTime vencimento, int diasEmAtraso = 0, DateTime? pagoEm = null) =>
            new()
            {
                ClienteNome = "Cliente",
                PlanoNome = "Plano",
                Status = status,
                Valor = valor,
                DataVencimento = vencimento,
                DiasEmAtraso = diasEmAtraso,
                DataPagamento = pagoEm
            };
    }
}
