using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.Services.Clientes;
using GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/clientes")]
    public class ClientesController : ControllerBase
    {
        private static readonly string[] SituacoesValidas = { SituacaoCliente.EmDia, SituacaoCliente.AVencer, SituacaoCliente.Inadimplente };

        private readonly IClienteRepository _clienteRepository;
        private readonly ServicoDeClientes _servico;
        private readonly ServicoDeLembretes _lembretes;
        private readonly IRabbitMQService _rabbitMQService;

        public ClientesController(
            IClienteRepository clienteRepository,
            ServicoDeClientes servico,
            ServicoDeLembretes lembretes,
            IRabbitMQService rabbitMQService)
        {
            _clienteRepository = clienteRepository;
            _servico = servico;
            _lembretes = lembretes;
            _rabbitMQService = rabbitMQService;
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] string? busca, [FromQuery] string? situacao)
        {
            if (!string.IsNullOrWhiteSpace(situacao) && !SituacoesValidas.Contains(situacao))
                return BadRequest($"Situação inválida. Use: {string.Join(", ", SituacoesValidas)}.");

            return Ok(await _servico.ListarAsync(DateTime.Today, busca, situacao));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> ObterFicha(int id)
        {
            var ficha = await _servico.ObterFichaAsync(id, DateTime.Today);
            return ficha == null ? NotFound("Cliente não encontrado.") : Ok(ficha);
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] SalvarClienteDto dto)
        {
            var (cliente, erro) = await ValidarAsync(dto, idAtual: null);
            if (erro != null) return erro;

            cliente!.Id = await _clienteRepository.CriarAsync(cliente);
            await PublicarEventoAsync("POST", cliente.Id, cliente.Nome);

            return Ok(new { message = "Cliente cadastrado com sucesso!", id = cliente.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Atualizar(int id, [FromBody] SalvarClienteDto dto)
        {
            if (await _clienteRepository.ObterPorIdAsync(id) == null) return NotFound("Cliente não encontrado.");

            var (cliente, erro) = await ValidarAsync(dto, idAtual: id);
            if (erro != null) return erro;

            cliente!.Id = id;
            await _clienteRepository.AtualizarAsync(cliente);
            await PublicarEventoAsync("PUT", id, cliente.Nome);

            return Ok(new { message = "Cliente atualizado com sucesso!" });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Excluir(int id)
        {
            var cliente = await _clienteRepository.ObterPorIdAsync(id);
            if (cliente == null) return NotFound("Cliente não encontrado.");

            // O histórico financeiro precisa continuar existindo: com assinaturas, o cliente não pode ser apagado
            if (await _clienteRepository.PossuiAssinaturasAsync(id))
                return Conflict("Este cliente tem assinaturas no histórico e não pode ser excluído.");

            await _clienteRepository.ExcluirAsync(id);
            await PublicarEventoAsync("DELETE", id, cliente.Nome);

            return NoContent();
        }

        // Um lembrete para cada cobrança em aberto do cliente
        [HttpPost("{id}/lembrete")]
        public async Task<IActionResult> EnviarLembrete(int id)
        {
            var ficha = await _servico.ObterFichaAsync(id, DateTime.Today);
            if (ficha == null) return NotFound("Cliente não encontrado.");
            if (string.IsNullOrWhiteSpace(ficha.Email))
                return UnprocessableEntity("Este cliente não tem e-mail cadastrado.");

            var emAberto = ficha.Cobrancas.Where(c => c.Status is StatusCobranca.Pendente or StatusCobranca.Vencida).ToList();
            if (emAberto.Count == 0)
                return Conflict("Este cliente não tem cobranças em aberto.");

            foreach (var cobranca in emAberto)
                await _lembretes.EnviarManualAsync(cobranca, DateTime.Today);

            return Ok(new { message = $"{emAberto.Count} lembrete(s) enviado(s) para {ficha.Email}." });
        }

        private async Task<(Cliente? Cliente, IActionResult? Erro)> ValidarAsync(SalvarClienteDto dto, int? idAtual)
        {
            string? documento = null;

            if (!string.IsNullOrWhiteSpace(dto.Documento))
            {
                if (!Documento.EhValido(dto.Documento))
                    return (null, BadRequest("CPF ou CNPJ inválido."));

                documento = Documento.ApenasDigitos(dto.Documento);
                var existente = await _clienteRepository.ObterPorDocumentoAsync(documento);
                if (existente != null && existente.Id != idAtual)
                    return (null, Conflict($"Já existe um cliente com este documento: {existente.Nome}."));
            }

            return (new Cliente
            {
                Nome = dto.Nome.Trim(),
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim(),
                Documento = documento,
                Telefone = string.IsNullOrWhiteSpace(dto.Telefone) ? null : dto.Telefone.Trim()
            }, null);
        }

        private Task PublicarEventoAsync(string operacao, int id, string nome) =>
            _rabbitMQService.EnviarMensagemAsync(new
            {
                Operacao = operacao,
                Entidade = "Cliente",
                Id = id,
                Nome = nome,
                Autor = User.FindFirst(ClaimTypes.Email)?.Value ?? "Desconhecido"
            }, "clientes_eventos");
    }
}
