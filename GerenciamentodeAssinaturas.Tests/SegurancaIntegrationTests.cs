using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace GerenciamentodeAssinaturas.Tests
{
    public class SegurancaIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public SegurancaIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Assinaturas_SemToken_DeveRetornar401()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/assinaturas");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task CriarPlano_ComoColaborador_DeveRetornar403()
        {
            var client = CriarClienteComCargo("Colaborador");

            var response = await client.PostAsJsonAsync("/api/planos", new { Nome = "Plano Indevido", Valor = 1m, Descricao = "x", Ativo = true });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ListarUsuarios_NaoDeveExporSenhaHashNemCpf()
        {
            var client = CriarClienteComCargo("Admin");

            var response = await client.GetAsync("/api/usuario");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotContainEquivalentOf("senhaHash");
            json.Should().NotContainEquivalentOf("cpf");
        }

        [Fact]
        public async Task Login_ComMuitasTentativasSeguidas_DeveRetornar429()
        {
            var client = _factory.CreateClient();
            HttpResponseMessage? ultima = null;

            // O limite é de 5 tentativas por minuto; a 6ª é bloqueada
            for (var i = 0; i < 6; i++)
                ultima = await client.PostAsJsonAsync("/api/auth/login", new { Email = "ataque@teste.com", Senha = $"chute{i}" });

            ultima!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        [Fact]
        public async Task Respostas_DevemTerCabecalhosDeSeguranca()
        {
            var response = await _factory.CreateClient().GetAsync("/api/planos");

            response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        }

        [Fact]
        public async Task TokenDeOutroEmissor_DeveSerRecusado()
        {
            var client = _factory.CreateClient();
            var tokenForjado = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }),
                Issuer = "outro-sistema",
                Audience = "AssinaMais.Web",
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.ASCII.GetBytes("chave-somente-para-desenvolvimento-local-assina-mais")),
                    SecurityAlgorithms.HmacSha256Signature)
            }));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenForjado);

            (await client.GetAsync("/api/planos")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [InlineData("curta1")]          // menos de 8 caracteres
        [InlineData("somenteletras")]   // sem números
        [InlineData("12345678")]        // sem letras
        public async Task Cadastro_ComSenhaFraca_DeveRetornar400(string senha)
        {
            var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
                new { Nome = "Teste Senha", Email = $"senha-{Guid.NewGuid():N}@teste.com", Senha = senha });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        private HttpClient CriarClienteComCargo(string cargo)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar(cargo));
            return client;
        }
    }
}
