using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AssinaMais.Tests
{
    public class PaginacaoIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _admin;

        public PaginacaoIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _admin = factory.CreateClient();
            _admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar("Admin"));
        }

        [Theory]
        [InlineData("/api/cobrancas")]
        [InlineData("/api/clientes")]
        [InlineData("/api/assinaturas")]
        public async Task Listagens_DevemRespeitarOTamanhoDaPagina_EInformarOTotal(string rota)
        {
            await GarantirDadosAsync();

            var response = await _admin.GetAsync($"{rota}?pagina=1&tamanhoPagina=2");
            response.EnsureSuccessStatusCode();

            var itens = await response.Content.ReadFromJsonAsync<List<object>>();
            var total = int.Parse(response.Headers.GetValues("X-Total-Count").Single());

            itens.Should().HaveCount(2);
            total.Should().BeGreaterThanOrEqualTo(3);
        }

        [Fact]
        public async Task PaginasDiferentes_NaoDevemRepetirRegistros()
        {
            await GarantirDadosAsync();

            var pagina1 = await _admin.GetFromJsonAsync<List<IdResposta>>("/api/clientes?pagina=1&tamanhoPagina=2");
            var pagina2 = await _admin.GetFromJsonAsync<List<IdResposta>>("/api/clientes?pagina=2&tamanhoPagina=2");

            pagina1!.Select(c => c.Id).Should().NotIntersectWith(pagina2!.Select(c => c.Id));
        }

        [Fact]
        public async Task TamanhoDePaginaAcimaDoLimite_DeveRetornar400()
        {
            var response = await _admin.GetAsync("/api/cobrancas?pagina=1&tamanhoPagina=500");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task BuscaDeClientes_DeveTratarCuringasComoTexto()
        {
            await _admin.PostAsJsonAsync("/api/clientes", new { Nome = "Empresa 100% Digital" });

            var comPercentual = await _admin.GetFromJsonAsync<List<ClienteResposta>>("/api/clientes?busca=100%25");
            var soCuringa = await _admin.GetFromJsonAsync<List<ClienteResposta>>("/api/clientes?busca=%25");

            comPercentual!.Should().Contain(c => c.Nome == "Empresa 100% Digital");
            soCuringa!.Should().OnlyContain(c => c.Nome.Contains('%'));   // "%" busca o caractere, não "qualquer coisa"
        }

        [Fact]
        public async Task CriarPlano_ComValorZero_DeveRetornar400()
        {
            var response = await _admin.PostAsJsonAsync("/api/planos", new { Nome = "Plano Gratuito Indevido", Valor = 0m, Descricao = "x" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task EditarPlanoInexistente_DeveRetornar404()
        {
            var response = await _admin.PutAsJsonAsync("/api/planos/999999", new { Nome = "Fantasma", Valor = 10m, Descricao = "x" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // Cria 3 clientes com assinatura (e portanto 3 cobranças), para as listagens terem mais de uma página
        private async Task GarantirDadosAsync()
        {
            var plano = await (await _admin.PostAsJsonAsync("/api/planos", new { Nome = "Plano Paginação", Valor = 10m, Descricao = "x" }))
                .Content.ReadFromJsonAsync<IdResposta>();

            for (var i = 0; i < 3; i++)
            {
                var cliente = await (await _admin.PostAsJsonAsync("/api/clientes", new { Nome = $"Cliente Paginação {i}" }))
                    .Content.ReadFromJsonAsync<IdResposta>();
                (await _admin.PostAsJsonAsync("/api/assinaturas", new
                {
                    ClienteId = cliente!.Id, PlanoId = plano!.Id, TipoRecorrencia = "Mensal", FormaPagamento = "Pix"
                })).EnsureSuccessStatusCode();
            }
        }

        private record IdResposta(int Id);
        private record ClienteResposta(int Id, string Nome);
    }
}
