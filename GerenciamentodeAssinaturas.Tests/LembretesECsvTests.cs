using FluentAssertions;
using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas;
using GerenciamentodeAssinaturasBackend.Domain.Services;
using System.Text;

namespace GerenciamentodeAssinaturas.Tests
{
    public class RegrasDeLembreteTests
    {
        private static readonly DateTime Hoje = new(2026, 10, 16);

        [Theory]
        [InlineData("Pendente", 3, "AVISO_VENCIMENTO")]   // vence em 3 dias
        [InlineData("Pendente", 0, "AVISO_VENCIMENTO")]   // vence hoje
        [InlineData("Pendente", 4, null)]                 // ainda é cedo
        [InlineData("Vencida", -10, "COBRANCA_VENCIDA")]
        [InlineData("Paga", 2, null)]
        [InlineData("Cancelada", 2, null)]
        public void LembreteAutomatico_DeveDependerDoStatusEDoPrazo(string status, int diasParaVencer, string? esperado)
        {
            RegrasDeLembrete.LembreteAutomatico(status, Hoje.AddDays(diasParaVencer), Hoje, diasDeAntecedencia: 3)
                .Should().Be(esperado);
        }

        [Fact]
        public void Texto_DeCobrancaEmAtraso_DeveInformarOsDias()
        {
            var (assunto, _, mensagem) = RegrasDeLembrete.Texto(TipoDeLembrete.CobrancaVencida, 12, Hoje.AddDays(-12), Hoje);

            assunto.Should().Contain("atraso");
            mensagem.Should().Contain("12 dia(s)");
        }

        [Fact]
        public void Texto_DeAviso_DeveDizerQuandoVence()
        {
            RegrasDeLembrete.Texto(TipoDeLembrete.AvisoDeVencimento, 0, Hoje.AddDays(1), Hoje).Mensagem
                .Should().Contain("vence amanhã");
        }
    }

    public class ExportadorCsvTests
    {
        [Fact]
        public void Cobrancas_DeveGerarCsvNoPadraoDoExcelEmPortugues()
        {
            var bytes = ExportadorCsv.Cobrancas(new[]
            {
                new CobrancaResponseDto
                {
                    ClienteNome = "Padaria Pão Quente",
                    ClienteEmail = "financeiro@paoquente.example",
                    PlanoNome = "Básico",
                    TipoRecorrencia = "Mensal",
                    PeriodoInicio = new DateTime(2026, 9, 1),
                    PeriodoFim = new DateTime(2026, 10, 1),
                    DataVencimento = new DateTime(2026, 9, 1),
                    Valor = 1234.5m,
                    Status = "Vencida",
                    DiasEmAtraso = 45
                }
            });

            bytes.Take(3).Should().Equal(Encoding.UTF8.GetPreamble());   // BOM: Excel reconhece os acentos

            var linhas = Encoding.UTF8.GetString(bytes[3..]).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            linhas.Should().HaveCount(2);
            linhas[0].Should().StartWith("Cliente;E-mail;Plano;Ciclo");
            linhas[1].Should().Be("Padaria Pão Quente;financeiro@paoquente.example;Básico;Mensal;01/09/2026;01/10/2026;01/09/2026;1234,50;Vencida;45;617,25;;");
        }

        [Theory]
        [InlineData("Empresa; Filial", "\"Empresa; Filial\"")]
        [InlineData("Loja \"Central\"", "\"Loja \"\"Central\"\"\"")]
        [InlineData("=HYPERLINK(\"x\")", "\"'=HYPERLINK(\"\"x\"\")\"")]   // não vira fórmula no Excel
        public void Cobrancas_DeveEscaparCamposPerigosos(string cliente, string esperado)
        {
            var csv = Encoding.UTF8.GetString(ExportadorCsv.Cobrancas(new[]
            {
                new CobrancaResponseDto { ClienteNome = cliente, Status = "Paga" }
            })[3..]);

            csv.Split(Environment.NewLine)[1].Should().StartWith(esperado + ";");
        }
    }
}
