using FluentAssertions;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturas.Tests
{
    public class RegrasDeCobrancaTests
    {
        [Theory]
        [InlineData(29.90, "Mensal", 29.90)]
        [InlineData(29.90, "Trimestral", 89.70)]
        [InlineData(199.90, "Semestral", 999.50)]   // 1 mês grátis
        [InlineData(79.90, "Anual", 799.00)]        // 2 meses grátis
        public void CalcularValor_DeveAplicarDescontoDoCiclo(decimal valorMensal, string recorrencia, decimal esperado)
        {
            RegrasDeCobranca.CalcularValor(valorMensal, recorrencia).Should().Be(esperado);
        }

        [Fact]
        public void ValorMensalEquivalente_DeveDiluirODescontoPelosMeses()
        {
            // 799,00 / 12 meses
            RegrasDeCobranca.ValorMensalEquivalente(79.90m, "Anual").Should().Be(66.58m);
        }

        [Theory]
        [InlineData("Mensal", 0)]
        [InlineData("Trimestral", 0)]
        [InlineData("Semestral", 16.67)]
        [InlineData("Anual", 16.67)]
        public void PercentualDeDesconto_DeveRefletirMesesGratis(string recorrencia, decimal esperado)
        {
            RegrasDeCobranca.PercentualDeDesconto(recorrencia).Should().Be(esperado);
        }

        [Theory]
        [InlineData("Mensal", 1)]
        [InlineData("Trimestral", 3)]
        [InlineData("Semestral", 6)]
        [InlineData("Anual", 12)]
        public void FimDoPeriodo_DeveSomarOsMesesDoCiclo(string recorrencia, int meses)
        {
            var inicio = new DateTime(2026, 1, 10);

            RegrasDeCobranca.FimDoPeriodo(inicio, recorrencia).Should().Be(inicio.AddMonths(meses));
        }

        [Fact]
        public void PeriodosAGerar_DeveManterODiaOriginalDepoisDeUmMesCurto()
        {
            var periodos = RegrasDeCobranca.PeriodosAGerar(
                new DateTime(2026, 1, 31), "Mensal", limite: new DateTime(2026, 4, 30), diaAncora: 31);

            periodos.Select(p => p.Inicio).Should().Equal(
                new DateTime(2026, 1, 31),
                new DateTime(2026, 2, 28),
                new DateTime(2026, 3, 31),
                new DateTime(2026, 4, 30));
        }

        [Fact]
        public void PeriodosAGerar_DeveIncluirApenasPeriodosQueComecamAteOLimite()
        {
            var periodos = RegrasDeCobranca.PeriodosAGerar(
                new DateTime(2026, 1, 10), "Mensal", limite: new DateTime(2026, 3, 15));

            periodos.Should().HaveCount(3);
            periodos.Last().Should().Be(new PeriodoDeCobranca(new DateTime(2026, 3, 10), new DateTime(2026, 4, 10)));
        }

        [Fact]
        public void PeriodosAGerar_ComInicioDepoisDoLimite_NaoGeraNada()
        {
            RegrasDeCobranca.PeriodosAGerar(new DateTime(2026, 5, 1), "Anual", limite: new DateTime(2026, 4, 1))
                .Should().BeEmpty();
        }

        [Theory]
        [InlineData("anual", true)]
        [InlineData("Semanal", false)]
        [InlineData(null, false)]
        public void EhRecorrenciaValida_DeveAceitarApenasCiclosConhecidos(string? recorrencia, bool esperado)
        {
            RegrasDeCobranca.EhRecorrenciaValida(recorrencia).Should().Be(esperado);
        }

        [Fact]
        public void RecorrenciaInvalida_DeveLancarExcecao()
        {
            var acao = () => RegrasDeCobranca.MesesDoCiclo("Semanal");

            acao.Should().Throw<ArgumentException>();
        }
    }
}
