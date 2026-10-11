using Microsoft.AspNetCore.Mvc;
using GerenciamentoAssinaturas.DTOs;
using GerenciamentoAssinaturas.Domain.Interfaces;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;

namespace GerenciamentoAssinaturas.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AssinaturasController : ControllerBase
    {
        private readonly IAssinaturaRepository _repository;
        private readonly IRabbitMQService _rabbitMQService;

        public AssinaturasController(IAssinaturaRepository repository, IRabbitMQService rabbitMQService)
        {
            _repository = repository;
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost]
        public async Task<IActionResult> CriarAssinatura([FromBody] CreateAssinaturaDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var id = await _repository.CriarAssinaturaAsync(dto);

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
        public async Task<IActionResult> ListarTodas()
        {
            var signatures = await _repository.ListarTodasAsync();
            return Ok(signatures);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> AtualizarAssinatura(int id, [FromBody] UpdateAssinaturaDto dto)
        {
            if (id != dto.Id) return BadRequest("O ID da URL difere do ID do corpo da requisição.");

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