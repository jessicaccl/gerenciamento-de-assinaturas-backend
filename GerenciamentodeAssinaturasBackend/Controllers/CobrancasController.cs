using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/cobrancas")]
    public class CobrancasController : ControllerBase
    {
        private readonly ICobrancaRepository _cobrancaRepository;
        private readonly GeradorDeCobrancas _gerador;
        private readonly IRabbitMQService _rabbitMQService;

        public CobrancasController(ICobrancaRepository cobrancaRepository, GeradorDeCobrancas gerador, IRabbitMQService rabbitMQService)
        {
            _cobrancaRepository = cobrancaRepository;
            _gerador = gerador;
            _rabbitMQService = rabbitMQService;
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] FiltroCobrancasDto filtro)
        {
            var statusValidos = new[] { StatusCobranca.Pendente, StatusCobranca.Vencida, StatusCobranca.Paga, StatusCobranca.Cancelada };
            if (!string.IsNullOrWhiteSpace(filtro.Status) && !statusValidos.Contains(filtro.Status))
                return BadRequest($"Status inválido. Use: {string.Join(", ", statusValidos)}.");

            return Ok(await _cobrancaRepository.ListarAsync(filtro, DateTime.Today));
        }

        [HttpPost("{id}/pagamento")]
        public async Task<IActionResult> RegistrarPagamento(int id, [FromBody] RegistrarPagamentoDto dto)
        {
            var dataPagamento = dto.DataPagamento ?? DateTime.Now;
            if (dataPagamento.Date > DateTime.Today)
                return BadRequest("A data de pagamento não pode estar no futuro.");

            var cobranca = await _cobrancaRepository.ObterPorIdAsync(id);
            if (cobranca == null) return NotFound("Cobrança não encontrada.");

            if (!await _cobrancaRepository.RegistrarPagamentoAsync(id, dataPagamento, dto.FormaPagamento))
                return Conflict($"Não é possível registrar pagamento de uma cobrança com status '{cobranca.Status}'.");

            await _rabbitMQService.EnviarMensagemAsync(new
            {
                Operacao = "PAGAMENTO",
                Entidade = "Cobranca",
                Id = id,
                cobranca.Valor,
                DataPagamento = dataPagamento,
                EmAtraso = dataPagamento.Date > cobranca.DataVencimento.Date,
                Autor = ObterEmailUsuario()
            }, "cobrancas_eventos");

            return Ok(new { message = "Pagamento registrado com sucesso!" });
        }

        [HttpPost("{id}/cancelar")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Cancelar(int id)
        {
            var cobranca = await _cobrancaRepository.ObterPorIdAsync(id);
            if (cobranca == null) return NotFound("Cobrança não encontrada.");

            if (!await _cobrancaRepository.CancelarAsync(id))
                return Conflict($"Não é possível cancelar uma cobrança com status '{cobranca.Status}'.");

            await _rabbitMQService.EnviarMensagemAsync(new
            {
                Operacao = "CANCELAMENTO",
                Entidade = "Cobranca",
                Id = id,
                Autor = ObterEmailUsuario()
            }, "cobrancas_eventos");

            return Ok(new { message = "Cobrança cancelada." });
        }

        // Dispara a geração na hora, sem esperar o worker (útil para demonstração)
        [HttpPost("gerar")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Gerar()
        {
            var geradas = await _gerador.ProcessarAsync(DateTime.Today);
            return Ok(new { message = $"{geradas} cobrança(s) gerada(s).", geradas });
        }

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }
}
