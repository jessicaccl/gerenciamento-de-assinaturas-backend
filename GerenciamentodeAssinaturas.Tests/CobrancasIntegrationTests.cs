using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GerenciamentodeAssinaturas.Tests
{
    public class CobrancasIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _admin;

        public CobrancasIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _admin = CriarCliente("Admin");
        }

        [Fact]
        public async Task CriarAssinaturaAnual_DeveGerarPrimeiraCobrancaComDesconto()
        {
            var (assinaturaId, _) = await CriarAssinaturaAsync(valorMensal: 100m, recorrencia: "Anual");

            var cobrancas = await ListarCobrancasAsync(assinaturaId);

            cobrancas.Should().ContainSingle();
            cobrancas[0].Valor.Should().Be(1000m);   // 12 meses pelo preço de 10
            cobrancas[0].Status.Should().Be("Pendente");
            cobrancas[0].DataVencimento.Date.Should().Be(DateTime.Today);
            cobrancas[0].PeriodoFim.Date.Should().Be(DateTime.Today.AddYears(1));
        }

        [Fact]
        public async Task RegistrarPagamento_DeveMarcarComoPaga_ENaoPermitirPagarDeNovo()
        {
            var (assinaturaId, _) = await CriarAssinaturaAsync(valorMensal: 50m, recorrencia: "Mensal");
            var cobranca = (await ListarCobrancasAsync(assinaturaId)).Single();

            var pagamento = await _admin.PostAsJsonAsync($"/api/cobrancas/{cobranca.Id}/pagamento", new { FormaPagamento = "Pix" });
            pagamento.StatusCode.Should().Be(HttpStatusCode.OK);

            var depois = (await ListarCobrancasAsync(assinaturaId)).Single();
            depois.Status.Should().Be("Paga");
            depois.FormaPagamento.Should().Be("Pix");

            var segundoPagamento = await _admin.PostAsJsonAsync($"/api/cobrancas/{cobranca.Id}/pagamento", new { });
            segundoPagamento.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task PagamentoComDataFutura_DeveRetornar400()
        {
            var (assinaturaId, _) = await CriarAssinaturaAsync(valorMensal: 50m, recorrencia: "Mensal");
            var cobranca = (await ListarCobrancasAsync(assinaturaId)).Single();

            var response = await _admin.PostAsJsonAsync($"/api/cobrancas/{cobranca.Id}/pagamento",
                new { DataPagamento = DateTime.Today.AddDays(3) });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CancelarCobranca_ComoColaborador_DeveRetornar403()
        {
            var (assinaturaId, _) = await CriarAssinaturaAsync(valorMensal: 50m, recorrencia: "Mensal");
            var cobranca = (await ListarCobrancasAsync(assinaturaId)).Single();

            var response = await CriarCliente("Colaborador").PostAsync($"/api/cobrancas/{cobranca.Id}/cancelar", null);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task CriarAssinaturaComCicloInvalido_DeveRetornar400()
        {
            var planoId = await CriarPlanoAsync(10m);

            var response = await _admin.PostAsJsonAsync("/api/assinaturas", new
            {
                PlanoId = planoId,
                ClienteNome = "Cliente Ciclo Inválido",
                TipoRecorrencia = "Semanal",
                FormaPagamento = "Pix"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CiclosDoPlano_DevemTrazerValoresComDesconto()
        {
            var planoId = await CriarPlanoAsync(60m);

            var ciclos = await _admin.GetFromJsonAsync<List<CicloResposta>>($"/api/planos/{planoId}/ciclos");

            ciclos!.Select(c => (c.Recorrencia, c.Valor)).Should().Equal(
                ("Mensal", 60m), ("Trimestral", 180m), ("Semestral", 300m), ("Anual", 600m));
        }

        private async Task<int> CriarPlanoAsync(decimal valorMensal)
        {
            var response = await _admin.PostAsJsonAsync("/api/planos", new
            {
                Nome = $"Plano Teste Cobrança {Guid.NewGuid():N}"[..40],
                Valor = valorMensal,
                Descricao = "Criado pelos testes de cobrança",
                Ativo = true
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IdResposta>())!.Id;
        }

        private async Task<(int assinaturaId, int planoId)> CriarAssinaturaAsync(decimal valorMensal, string recorrencia)
        {
            var planoId = await CriarPlanoAsync(valorMensal);

            var response = await _admin.PostAsJsonAsync("/api/assinaturas", new
            {
                PlanoId = planoId,
                ClienteNome = "Cliente Teste Cobrança",
                TipoRecorrencia = recorrencia,
                FormaPagamento = "Boleto"
            });
            response.EnsureSuccessStatusCode();

            return ((await response.Content.ReadFromJsonAsync<IdResposta>())!.Id, planoId);
        }

        private async Task<List<CobrancaResposta>> ListarCobrancasAsync(int assinaturaId) =>
            (await _admin.GetFromJsonAsync<List<CobrancaResposta>>($"/api/cobrancas?assinaturaId={assinaturaId}"))!;

        private HttpClient CriarCliente(string cargo)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(cargo));
            return client;
        }

        private record IdResposta(int Id);
        private record CicloResposta(string Recorrencia, decimal Valor);
        private record CobrancaResposta(int Id, decimal Valor, string Status, DateTime DataVencimento, DateTime PeriodoFim, string? FormaPagamento);
    }
}
