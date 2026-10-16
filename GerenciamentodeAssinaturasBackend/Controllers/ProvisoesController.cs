using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Services.Provisoes;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/provisoes")]
    public class ProvisoesController : ControllerBase
    {
        private readonly ICobrancaRepository _cobrancaRepository;

        public ProvisoesController(ICobrancaRepository cobrancaRepository)
        {
            _cobrancaRepository = cobrancaRepository;
        }

        // mesesFuturos: quantos meses de previsão a partir do mês atual (inclusive). O gráfico também mostra 2 meses de histórico.
        [HttpGet]
        public async Task<ActionResult<PainelDeProvisoesDto>> Obter([FromQuery] int mesesFuturos = 6)
        {
            if (mesesFuturos is < 1 or > 12)
                return BadRequest("mesesFuturos deve estar entre 1 e 12.");

            var hoje = DateTime.Today;
            var cobrancas = (await _cobrancaRepository.ListarAsync(new FiltroCobrancasDto(), hoje)).ToList();
            var assinaturasAtivas = (await _cobrancaRepository.ListarAssinaturasParaFaturamentoAsync()).ToList();

            return Ok(CalculadoraDoPainel.Calcular(cobrancas, assinaturasAtivas, hoje, mesesFuturos));
        }
    }
}
