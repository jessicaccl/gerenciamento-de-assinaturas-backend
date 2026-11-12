using AssinaMais.Api.Application.Services.Demo;
using FluentAssertions;

namespace AssinaMais.Tests
{
    public class ResetDemoTests
    {
        [Theory]
        [InlineData(3, false)]   // madrugada, antes do horário: ainda não reseta
        [InlineData(4, true)]
        [InlineData(15, true)]   // API "dormiu" às 4h e acordou à tarde: reseta assim que acorda
        public void JaPassouDoHorario_DeveLiberarOResetAPartirDaHoraConfigurada(int hora, bool esperado)
        {
            ServicoDeDadosDemo.JaPassouDoHorario(new DateTime(2026, 11, 12, hora, 30, 0), horaDoReset: 4).Should().Be(esperado);
        }
    }
}
