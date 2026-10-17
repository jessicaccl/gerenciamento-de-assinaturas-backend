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
        private readonly ServicoDeLembretes _lembretes;

        public CobrancasController(
            ICobrancaRepository cobrancaRepository,
            GeradorDeCobrancas gerador,
            IRabbitMQService rabbitMQService,
            ServicoDeLembretes lembretes)
        {
            _cobrancaRepository = cobrancaRepository;
            _gerador = gerador;
            _rabbitMQService = rabbitMQService;
            _lembretes = lembretes;
        }

        private static readonly string[] StatusValidos =
            { StatusCobranca.Pendente, StatusCobranca.Vencida, StatusCobranca.Paga, StatusCobranca.Cancelada };

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] FiltroCobrancasDto filtro)
        {
            if (!StatusEhValido(filtro.Status))
                return BadRequest($"Status inválido. Use: {string.Join(", ", StatusValidos)}.");

            return Ok(await _cobrancaRepository.ListarAsync(filtro, DateTime.Today));
        }

        // Mesmos filtros da listagem, em CSV para abrir no Excel
        [HttpGet("exportar")]
        public async Task<IActionResult> Exportar([FromQuery] FiltroCobrancasDto filtro)
        {
            if (!StatusEhValido(filtro.Status))
                return BadRequest($"Status inválido. Use: {string.Join(", ", StatusValidos)}.");

            var cobrancas = await _cobrancaRepository.ListarAsync(filtro, DateTime.Today);
            var nomeArquivo = $"cobrancas-{DateTime.Today:yyyy-MM-dd}.csv";

            return File(ExportadorCsv.Cobrancas(cobrancas), "text/csv; charset=utf-8", nomeArquivo);
        }

        [HttpPost("{id}/lembrete")]
        public async Task<IActionResult> EnviarLembrete(int id)
        {
            var hoje = DateTime.Today;
            var cobranca = (await _cobrancaRepository.ListarAsync(new FiltroCobrancasDto(), hoje)).SingleOrDefault(c => c.Id == id);
            if (cobranca == null) return NotFound("Cobrança não encontrada.");

            return await _lembretes.EnviarManualAsync(cobranca, hoje) switch
            {
                ResultadoDoLembrete.SemEmail => UnprocessableEntity("O cliente desta assinatura não tem e-mail cadastrado."),
                ResultadoDoLembrete.CobrancaFechada => Conflict($"Não é possível enviar lembrete de uma cobrança com status '{cobranca.Status}'."),
                _ => Ok(new { message = $"Lembrete enviado para {cobranca.ClienteEmail}." })
            };
        }

        [HttpPost("lembretes/vencidas")]
        public async Task<IActionResult> NotificarVencidas()
        {
            var (enviados, semEmail) = await _lembretes.NotificarVencidasAsync(DateTime.Today);

            var message = semEmail > 0
                ? $"{enviados} lembrete(s) enviado(s). {semEmail} cliente(s) sem e-mail cadastrado."
                : $"{enviados} lembrete(s) enviado(s).";

            return Ok(new { message, enviados, semEmail });
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

        private static bool StatusEhValido(string? status) =>
            string.IsNullOrWhiteSpace(status) || StatusValidos.Contains(status);

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }
}
