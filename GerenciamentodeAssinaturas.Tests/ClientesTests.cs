using FluentAssertions;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GerenciamentodeAssinaturas.Tests
{
    public class DocumentoTests
    {
        [Theory]
        [InlineData("123.456.789-09", true)]
        [InlineData("12345678909", true)]
        [InlineData("123.456.789-00", false)]       // dígito verificador errado
        [InlineData("111.111.111-11", false)]       // todos iguais: passa na conta, mas não existe
        [InlineData("12.345.678/0001-95", true)]
        [InlineData("12.345.678/0001-90", false)]
        [InlineData("00.000.000/0000-00", false)]
        [InlineData("1234", false)]
        [InlineData(null, false)]
        public void EhValido_DeveConferirOsDigitosVerificadores(string? documento, bool esperado)
        {
            Documento.EhValido(documento).Should().Be(esperado);
        }

        [Theory]
        [InlineData("12345678909", "***.456.789-**")]
        [InlineData("12345678000195", "**.***.678/0001-95")]
        [InlineData(null, "")]
        public void Mascarar_DeveEsconderParteDoDocumento(string? documento, string esperado)
        {
            Documento.Mascarar(documento).Should().Be(esperado);
        }

        [Theory]
        [InlineData("12345678909", "123.456.789-09")]
        [InlineData("12345678000195", "12.345.678/0001-95")]
        public void Formatar_DeveAplicarAPontuacao(string documento, string esperado)
        {
            Documento.Formatar(documento).Should().Be(esperado);
        }
    }

    public class SituacaoClienteTests
    {
        [Theory]
        [InlineData(0, 0, "Em dia")]
        [InlineData(0, 2, "A vencer")]
        [InlineData(1, 0, "Inadimplente")]
        [InlineData(1, 3, "Inadimplente")]   // uma vencida já basta, mesmo com outras em dia
        public void Calcular_DeveSeguirARegraDeAdimplencia(int vencidas, int pendentes, string esperado)
        {
            SituacaoCliente.Calcular(vencidas, pendentes).Should().Be(esperado);
        }
    }

    public class ClientesIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _admin;
        private readonly HttpClient _colaborador;

        public ClientesIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _admin = factory.CreateClient();
            _admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar("Admin"));
            _colaborador = factory.CreateClient();
            _colaborador.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDeTeste.Gerar("Colaborador"));
        }

        [Fact]
        public async Task CriarCliente_ComDocumentoInvalido_DeveRetornar400()
        {
            var response = await _admin.PostAsJsonAsync("/api/clientes", new { Nome = "Cliente Doc Inválido", Documento = "123.456.789-00" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task CriarCliente_ComDocumentoRepetido_DeveRetornar409()
        {
            var documento = GerarCpfValido();
            (await _admin.PostAsJsonAsync("/api/clientes", new { Nome = "Primeiro", Documento = documento })).EnsureSuccessStatusCode();

            var response = await _admin.PostAsJsonAsync("/api/clientes", new { Nome = "Segundo", Documento = documento });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task Listagem_DeveMascararDocumento_EFicha_DeveMostrarCompleto()
        {
            var documento = GerarCpfValido();
            var id = await CriarClienteAsync("Cliente Máscara", documento);

            var lista = await _admin.GetFromJsonAsync<List<ClienteResposta>>("/api/clientes?busca=Cliente Máscara");
            var ficha = await _admin.GetFromJsonAsync<ClienteResposta>($"/api/clientes/{id}");

            var resumo = lista!.Single(c => c.Id == id);
            resumo.DocumentoMascarado.Should().StartWith("***.");
            resumo.Documento.Should().BeNull();
            Documento.ApenasDigitos(ficha!.Documento).Should().Be(documento);
        }

        [Fact]
        public async Task FichaDoCliente_DeveSomarMrrEAssinaturasAtivas()
        {
            var clienteId = await CriarClienteAsync("Cliente Com Assinatura");
            var planoId = await CriarPlanoAsync(120m);
            await AssinarAsync(clienteId, planoId, "Anual");     // 1200 / 12 = 100 por mês
            await AssinarAsync(clienteId, planoId, "Mensal");    // 120 por mês

            var ficha = await _admin.GetFromJsonAsync<ClienteResposta>($"/api/clientes/{clienteId}");

            ficha!.AssinaturasAtivas.Should().Be(2);
            ficha.Mrr.Should().Be(220m);
            ficha.Situacao.Should().Be(SituacaoCliente.AVencer);   // primeira cobrança de cada uma vence hoje
            ficha.EmAberto.Should().Be(1320m);
        }

        [Fact]
        public async Task ExcluirCliente_ComAssinatura_DeveRetornar409_ESemAssinatura_204()
        {
            var comAssinatura = await CriarClienteAsync("Cliente Com Histórico");
            await AssinarAsync(comAssinatura, await CriarPlanoAsync(10m), "Mensal");
            var semAssinatura = await CriarClienteAsync("Cliente Sem Histórico");

            (await _admin.DeleteAsync($"/api/clientes/{comAssinatura}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await _admin.DeleteAsync($"/api/clientes/{semAssinatura}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task ExcluirCliente_ComoColaborador_DeveRetornar403()
        {
            var id = await CriarClienteAsync("Cliente Protegido");

            (await _colaborador.DeleteAsync($"/api/clientes/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        private async Task<int> CriarClienteAsync(string nome, string? documento = null)
        {
            var response = await _admin.PostAsJsonAsync("/api/clientes", new { Nome = nome, Documento = documento });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IdResposta>())!.Id;
        }

        private async Task<int> CriarPlanoAsync(decimal valor)
        {
            var response = await _admin.PostAsJsonAsync("/api/planos", new { Nome = "Plano Teste Clientes", Valor = valor, Descricao = "x", Ativo = true });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<IdResposta>())!.Id;
        }

        private async Task AssinarAsync(int clienteId, int planoId, string recorrencia)
        {
            var response = await _admin.PostAsJsonAsync("/api/assinaturas", new
            {
                ClienteId = clienteId, PlanoId = planoId, TipoRecorrencia = recorrencia, FormaPagamento = "Pix"
            });
            response.EnsureSuccessStatusCode();
        }

        // CPF aleatório com dígitos verificadores corretos, para os testes não colidirem entre execuções
        private static string GerarCpfValido()
        {
            var digitos = Enumerable.Range(0, 9).Select(_ => Random.Shared.Next(0, 10)).ToList();
            foreach (var pesoInicial in new[] { 10, 11 })
            {
                var resto = digitos.Select((d, i) => d * (pesoInicial - i)).Sum() * 10 % 11;
                digitos.Add(resto == 10 ? 0 : resto);
            }
            return string.Concat(digitos);
        }

        private record IdResposta(int Id);
        private record ClienteResposta(int Id, string Nome, string DocumentoMascarado, string? Documento,
            decimal Mrr, decimal EmAberto, int AssinaturasAtivas, string Situacao);
    }
}
