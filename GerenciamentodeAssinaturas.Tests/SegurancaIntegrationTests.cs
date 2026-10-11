using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

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

        private HttpClient CriarClienteComCargo(string cargo)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GerarToken(cargo));
            return client;
        }

        private static string GerarToken(string cargo)
        {
            // Mesma chave do appsettings.Development.json (ambiente usado pelo WebApplicationFactory)
            var key = Encoding.ASCII.GetBytes("chave-somente-para-desenvolvimento-local-assina-mais");
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "999"),
                    new Claim(ClaimTypes.Email, $"{cargo.ToLower()}@teste.com"),
                    new Claim(ClaimTypes.Role, cargo)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }
}
