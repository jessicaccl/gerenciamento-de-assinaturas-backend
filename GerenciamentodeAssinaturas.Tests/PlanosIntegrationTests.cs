using FluentAssertions;
using GerenciamentoAssinaturas.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Xunit;

namespace GerenciamentodeAssinaturas.Tests
{
    public class PlanosIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public PlanosIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
            var token = GerarTokenParaTeste();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        [Fact]
        public async Task CicloDeVida_Plano_DeveRealizarCrudEInativar()
        {
            var novoPlano = new Plano
            {
                Nome = "Plano Teste CRUD",
                Valor = 50.0m,
                Descricao = "Plano criado para teste de ciclo de vida",
                Ativo = true
            };

            var responsePost = await _client.PostAsJsonAsync("/api/planos", novoPlano);

            // Segurança
            if (!responsePost.IsSuccessStatusCode)
            {
                var erro = await responsePost.Content.ReadAsStringAsync();
                throw new Exception($"Falha ao criar Plano: {responsePost.StatusCode} - {erro}");
            }

            var resultadoPost = await responsePost.Content.ReadFromJsonAsync<PlanoIdResponse>();

            // GET
            var responseGet = await _client.GetAsync($"/api/planos/{resultadoPost.Id}");
            var planoObtido = await responseGet.Content.ReadFromJsonAsync<Plano>();

            // UPDATE 
            planoObtido.Nome = "Plano Alterado no Teste";
            var responsePut = await _client.PutAsJsonAsync($"/api/planos/{planoObtido.Id}", planoObtido);
            responsePut.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // PUT (Inativação/Delete)
            planoObtido.Ativo = false;
            var responseInativar = await _client.PutAsJsonAsync($"/api/planos/{planoObtido.Id}", planoObtido);
            responseInativar.StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Validação final: Confere se o plano está False no banco
            var responseFinal = await _client.GetAsync($"/api/planos/{planoObtido.Id}");
            var planoFinal = await responseFinal.Content.ReadFromJsonAsync<Plano>();
            planoFinal.Ativo.Should().BeFalse();
        }

        [Fact]
        public async Task CicloDeVida_Assinatura_DeveCriarEInativarCorretamente()
        {
            var planoParaAssinatura = new Plano
            {
                Nome = "Plano Para Assinatura",
                Valor = 100.0m,
                Descricao = "Dependência do teste de assinatura",
                Ativo = true
            };

            var responsePlano = await _client.PostAsJsonAsync("/api/planos", planoParaAssinatura);
            responsePlano.EnsureSuccessStatusCode();

            var dadosPlano = await responsePlano.Content.ReadFromJsonAsync<PlanoIdResponse>();
            int idPlanoValido = dadosPlano.Id;

            var responseCliente = await _client.PostAsJsonAsync("/api/clientes", new { Nome = "Teste Inativação" });
            responseCliente.EnsureSuccessStatusCode();
            var clienteId = (await responseCliente.Content.ReadFromJsonAsync<PlanoIdResponse>())!.Id;

            var novaAssinaturaDto = new
            {
                PlanoId = idPlanoValido,
                ClienteId = clienteId,
                TipoRecorrencia = "Mensal",
                FormaPagamento = "Cartão"
            };

            var responsePost = await _client.PostAsJsonAsync("/api/assinaturas", novaAssinaturaDto);

            if (!responsePost.IsSuccessStatusCode)
            {
                var erroTxt = await responsePost.Content.ReadAsStringAsync();
                throw new Exception($"Erro no POST da Assinatura: {responsePost.StatusCode} - {erroTxt}");
            }

            var retorno = await responsePost.Content.ReadFromJsonAsync<PlanoIdResponse>();
            int assinaturaId = retorno.Id;

            var responseDelete = await _client.DeleteAsync($"/api/assinaturas/{assinaturaId}");

            if (!responseDelete.IsSuccessStatusCode)
            {
                var erroDelete = await responseDelete.Content.ReadAsStringAsync();
                throw new Exception($"Erro no DELETE da Assinatura: {responseDelete.StatusCode} - {erroDelete}");
            }

            responseDelete.StatusCode.Should().Be(HttpStatusCode.OK);

            var mensagemRetorno = await responseDelete.Content.ReadAsStringAsync();
            mensagemRetorno.Should().Contain("sucesso");
        }

        private string GerarTokenParaTeste()
        {
            // Mesma chave do appsettings.Development.json (ambiente usado pelo WebApplicationFactory)
            var chaveSecreta = "chave-somente-para-desenvolvimento-local-assina-mais";
            var key = Encoding.ASCII.GetBytes(chaveSecreta);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] {
                    new Claim(ClaimTypes.Email, "admin@teste.com"),
                    new Claim(ClaimTypes.Role, "Admin")
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }

    public class PlanoIdResponse { public int Id { get; set; } }
}