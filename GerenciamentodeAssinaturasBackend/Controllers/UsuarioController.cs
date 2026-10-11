using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/usuario")]
    public class UsuarioController : ControllerBase
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly string[] _contasProtegidas;

        public UsuarioController(IUsuarioRepository usuarioRepository, IRabbitMQService rabbitMQService, IConfiguration configuration)
        {
            _usuarioRepository = usuarioRepository;
            _rabbitMQService = rabbitMQService;
            _contasProtegidas = configuration.GetSection("App:ContasProtegidas").Get<string[]>() ?? Array.Empty<string>();
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var usuarios = await _usuarioRepository.GetAllAsync();

            var resposta = usuarios.Select(u => new UsuarioResponseDto
            {
                Id = u.Id,
                Nome = u.Nome,
                Email = u.Email,
                Cargo = u.Cargo,
                DataCriacao = u.DataCriacao
            });

            return Ok(resposta);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUsuarioDto dto)
        {
            if (id != dto.Id) return BadRequest("ID da URL difere do ID do corpo.");

            var ehAdmin = User.IsInRole(Cargos.Admin);
            if (!ehAdmin && ObterIdUsuario() != id)
                return Forbid();

            var usuarioOriginal = await _usuarioRepository.GetByIdAsync(id);
            if (usuarioOriginal == null) return NotFound("Usuário não encontrado.");
            if (EhContaProtegida(usuarioOriginal.Email))
                return StatusCode(StatusCodes.Status403Forbidden, "Contas de demonstração não podem ser alteradas.");

            if (!string.Equals(usuarioOriginal.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
            {
                var emailEmUso = await _usuarioRepository.GetUsuarioByEmailAsync(dto.Email);
                if (emailEmUso != null) return Conflict("Este e-mail já está em uso.");
            }

            usuarioOriginal.Nome = dto.Nome;
            usuarioOriginal.Email = dto.Email;

            // Apenas administradores podem alterar cargos
            if (ehAdmin && dto.Cargo != null)
            {
                if (!Cargos.EhValido(dto.Cargo)) return BadRequest("Cargo inválido.");
                usuarioOriginal.Cargo = dto.Cargo;
            }

            await _usuarioRepository.UpdateAsync(usuarioOriginal);

            var evento = new
            {
                Operacao = "PUT",
                Entidade = "Usuario",
                Id = id,
                Dados = new { usuarioOriginal.Nome, usuarioOriginal.Email, usuarioOriginal.Cargo },
                Autor = ObterEmailUsuario()
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "usuarios_eventos");

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            if (ObterIdUsuario() == id)
                return BadRequest("Você não pode excluir o próprio usuário.");

            var usuario = await _usuarioRepository.GetByIdAsync(id);
            if (usuario == null) return NotFound("Usuário não encontrado.");
            if (EhContaProtegida(usuario.Email))
                return StatusCode(StatusCodes.Status403Forbidden, "Contas de demonstração não podem ser excluídas.");

            await _usuarioRepository.DeleteAsync(id);

            var evento = new
            {
                Operacao = "DELETE",
                Entidade = "Usuario",
                Id = id,
                Autor = ObterEmailUsuario()
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "usuarios_eventos");

            return NoContent();
        }

        private bool EhContaProtegida(string email)
        {
            return _contasProtegidas.Contains(email, StringComparer.OrdinalIgnoreCase);
        }

        private int? ObterIdUsuario()
        {
            return int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;
        }

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }
}
