using GerenciamentoAssinaturas.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PlanosController : ControllerBase
    {
        private readonly IPlanoRepository _planoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IAssinaturaRepository _assinaturaRepository;

        public PlanosController(
            IPlanoRepository planoRepository,
            IUsuarioRepository usuarioRepository,
            IRabbitMQService rabbitMQService,
            IAssinaturaRepository assinaturaRepository)
        {
            _planoRepository = planoRepository;
            _usuarioRepository = usuarioRepository;
            _rabbitMQService = rabbitMQService;
            _assinaturaRepository = assinaturaRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var planos = await _planoRepository.GetAllAsync();
            return Ok(planos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var plano = await _planoRepository.GetByIdAsync(id);
            if (plano == null) return NotFound();

            return Ok(plano);
        }

        [HttpPost]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Create(Plano plano)
        {
            var novoId = await _planoRepository.CreateAsync(plano);
            plano.Id = novoId;

            var evento = new { Operacao = "POST", Dados = plano, Autor = ObterEmailUsuario() };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return Ok(new { id = novoId });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Update(int id, Plano plano)
        {
            if (id != plano.Id) return BadRequest("O ID da URL é diferente do ID do corpo.");

            await _planoRepository.UpdateAsync(plano);

            var evento = new { Operacao = "PUT", Dados = plano, Autor = ObterEmailUsuario() };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Delete(int id, [FromBody] ConfirmacaoDeleteDto confirmacao)
        {
            var emailLogado = ObterEmailUsuario();

            var usuario = await _usuarioRepository.GetUsuarioByEmailAsync(emailLogado);
            if (usuario == null) return Unauthorized("Usuário não encontrado.");

            if (!BCrypt.Net.BCrypt.Verify(confirmacao.Senha, usuario.SenhaHash))
                return BadRequest("Senha de administrador incorreta.");

            var possuiDependencias = await _assinaturaRepository.ExisteAssinaturaParaPlanoAsync(id);

            if (possuiDependencias)
            {
                return Conflict("Não é possível excluir este plano. Existem assinaturas vinculadas a ele no histórico do sistema.");
            }

            await _planoRepository.DeleteAsync(id);

            var evento = new { Operacao = "DELETE", PlanoId = id, Autor = emailLogado };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return NoContent();
        }

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }

    public class ConfirmacaoDeleteDto
    {
        public string? Senha { get; set; }
    }
}