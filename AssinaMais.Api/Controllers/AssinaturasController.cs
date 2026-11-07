using Microsoft.AspNetCore.Mvc;
using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Interfaces;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using AssinaMais.Api.Application.Services.Cobrancas;
using AssinaMais.Api.Domain.Services;
using AssinaMais.Api.Application.Interfaces;

namespace AssinaMais.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AssinaturasController : ControllerBase
    {
        private readonly IAssinaturaRepository _repository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly GeradorDeCobrancas _geradorDeCobrancas;
        private readonly ICobrancaRepository _cobrancaRepository;
        private readonly IClienteRepository _clienteRepository;

        public AssinaturasController(
            IAssinaturaRepository repository,
            IRabbitMQService rabbitMQService,
            GeradorDeCobrancas geradorDeCobrancas,
            ICobrancaRepository cobrancaRepository,
            IClienteRepository clienteRepository)
        {
            _clienteRepository = clienteRepository;
            _repository = repository;
            _rabbitMQService = rabbitMQService;
            _geradorDeCobrancas = geradorDeCobrancas;
            _cobrancaRepository = cobrancaRepository;
        }

        [HttpPost]
        public async Task<IActionResult> CriarAssinatura([FromBody] CreateAssinaturaDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (!RegrasDeCobranca.EhRecorrenciaValida(dto.TipoRecorrencia))
                return BadRequest($"Ciclo inválido. Use: {string.Join(", ", RegrasDeCobranca.Recorrencias)}.");
            if (await _clienteRepository.ObterPorIdAsync(dto.ClienteId) == null)
                return BadRequest("Cliente não encontrado.");

            dto.TipoRecorrencia = RegrasDeCobranca.Normalizar(dto.TipoRecorrencia!);
            var id = await _repository.CriarAssinaturaAsync(dto);

            // A primeira cobrança já nasce junto com a assinatura
            await _geradorDeCobrancas.GerarParaAssinaturaAsync(id, DateTime.Today);

            var evento = new
            {
                Operacao = "POST",
                Entidade = "Assinatura",
                Id = id,
                Dados = dto,
                Autor = ObterEmailUsuario()
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "assinaturas_eventos");

            return Ok(new { message = "Assinatura criada com sucesso!", id = id });
        }

        [HttpGet]
        public async Task<IActionResult> Listar([FromQuery] string? status, [FromQuery] int? planoId, [FromQuery] PaginacaoDto paginacao)
        {
            if (!string.IsNullOrWhiteSpace(status) && status is not ("Ativo" or "Inativo"))
                return BadRequest("Status inválido. Use: Ativo ou Inativo.");

            return this.OkPaginado(await _repository.ListarAsync(status, planoId, paginacao));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarAssinatura(int id, [FromBody] UpdateAssinaturaDto dto)
        {
            if (id != dto.Id) return BadRequest("O ID da URL difere do ID do corpo da requisição.");
            if (!RegrasDeCobranca.EhRecorrenciaValida(dto.TipoRecorrencia))
                return BadRequest($"Ciclo inválido. Use: {string.Join(", ", RegrasDeCobranca.Recorrencias)}.");

            // Mudanças de plano ou de ciclo valem a partir da próxima cobrança
            dto.TipoRecorrencia = RegrasDeCobranca.Normalizar(dto.TipoRecorrencia!);

            await _repository.AtualizarAssinaturaAsync(dto);

            var evento = new
            {
                Operacao = "PUT",
                Entidade = "Assinatura",
                Id = id,
                Dados = dto,
                Autor = ObterEmailUsuario()
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "assinaturas_eventos");

            return Ok(new { message = "Assinatura atualizada com sucesso!" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> InativarAssinatura(int id)
        {
            await _repository.InativarAssinaturaAsync(id);
            await _cobrancaRepository.CancelarFuturasDaAssinaturaAsync(id, DateTime.Today);
            await _cobrancaRepository.AtualizarExpiracaoDaAssinaturaAsync(id);

            var evento = new
            {
                Operacao = "DELETE",
                Entidade = "Assinatura",
                Id = id,
                Autor = ObterEmailUsuario()
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "assinaturas_eventos");

            return Ok(new { message = "Assinatura inativada com sucesso!" });
        }

        [HttpPatch("{id}/reativar")]
        public async Task<IActionResult> Reativar(int id)
        {
            await _repository.ReativarAssinaturaAsync(id);

            // O novo ciclo começa hoje: o tempo em que a assinatura ficou inativa não é cobrado
            await _geradorDeCobrancas.GerarParaAssinaturaAsync(id, DateTime.Today, recomecarEm: DateTime.Today);

            var evento = new
            {
                Operacao = "PATCH",
                    Tipo = "REATIVAÇÃO",
                Entidade = "Assinatura",
                Id = id,
                Autor = ObterEmailUsuario()
            };

            await _rabbitMQService.EnviarMensagemAsync(evento, "assinaturas_eventos");

            return Ok(new { message = "Assinatura reativada com sucesso!" });
        }

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }
}