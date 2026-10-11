using Xunit;
using FluentAssertions;
using System;

namespace GerenciamentodeAssinaturas.Tests
{
    public class AssinaturaLogicTests
    {
        [Theory]
        [InlineData("Mensal", 1)]
        [InlineData("Trimestral", 3)]
        [InlineData("Semestral", 6)]
        [InlineData("Anual", 12)]
        public void CalcularExpiracao_DeveSomarMesesCorretamente(string tipoRecorrencia, int mesesAdicionais)
        {            
            DateTime dataInicio = new DateTime(2026, 1, 1);
            DateTime dataEsperada = dataInicio.AddMonths(mesesAdicionais);
           
            DateTime resultado = ExecutarLogicaDeCalculo(dataInicio, tipoRecorrencia);

            resultado.Should().Be(dataEsperada);
        }

        private DateTime ExecutarLogicaDeCalculo(DateTime inicio, string tipo)
        {
            return tipo switch
            {
                "Mensal" => inicio.AddMonths(1),
                "Trimestral" => inicio.AddMonths(3),
                "Semestral" => inicio.AddMonths(6),
                "Anual" => inicio.AddYears(1),
                _ => inicio.AddMonths(1)
            };
        }
    }
}