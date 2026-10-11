using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.Services.Auth;

namespace GerenciamentodeAssinaturasBackend.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRabbitMQService _rabbitMQService;

        public AuthController(
            AuthService authService,
            IUsuarioRepository usuarioRepository,
            IRabbitMQService rabbitMQService)
        {
            _authService = authService;
            _usuarioRepository = usuarioRepository;
            _rabbitMQService = rabbitMQService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            var resultado = await _authService.LoginAsync(loginDto);

            if (resultado == null)
            {
                return Unauthorized("Usuário ou senha inválidos.");
            }

            var evento = new
            {
                Operacao = "LOGIN",
                Usuario = loginDto.Email,
                Data = DateTime.Now,
                Status = "Sucesso"
            };
            await _rabbitMQService.EnviarMensagemAsync(evento, "auth_eventos");

            return Ok(resultado);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var usuarioExistente = await _usuarioRepository.GetUsuarioByEmailAsync(dto.Email!);
            if (usuarioExistente != null)
                return BadRequest("Este e-mail já está em uso.");

            var novoUsuario = new Usuario
            {
                Nome = dto.Nome!,
                Email = dto.Email!,
                CPF = dto.CPF ?? string.Empty,
                Cargo = Cargos.Colaborador,
                DataCriacao = DateTime.Now,
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(dto.Senha),
                EmailConfirmado = true,
                TokenConfirmacao = null
            };

            await _usuarioRepository.CreateAsync(novoUsuario);

            var eventoAuditoria = new
            {
                Operacao = "REGISTER",
                Entidade = "Usuario",
                Nome = novoUsuario.Nome,
                Email = novoUsuario.Email,
                Autor = novoUsuario.Email
            };
            await _rabbitMQService.EnviarMensagemAsync(eventoAuditoria, "usuarios_eventos");

            var eventoEmail = new
            {
                Tipo = "BOAS_VINDAS",
                EmailDestino = novoUsuario.Email,
                Nome = novoUsuario.Nome,
                DataEnvio = DateTime.Now
            };
            await _rabbitMQService.EnviarMensagemAsync(eventoEmail, "emails_queue");

            return Ok(new { message = "Cadastro realizado com sucesso! Você já pode fazer login." });
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdString, out int userId)) return Unauthorized("Token inválido.");

            var usuario = await _usuarioRepository.GetByIdAsync(userId);
            if (usuario == null) return NotFound("Usuário não encontrado.");

            return Ok(new
            {
                id = usuario.Id,
                nome = usuario.Nome,
                email = usuario.Email,
                cargo = usuario.Cargo,
                dataCadastro = usuario.DataCriacao
            });
        }

        [HttpPost("validate-password")]
        [Authorize]
        public async Task<IActionResult> ValidatePassword([FromBody] ValidatePasswordDto dto)
        {
            var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name;

            if (string.IsNullOrEmpty(userEmail))
                return Unauthorized("Não foi possível identificar o usuário logado.");

            var usuario = await _usuarioRepository.GetUsuarioByEmailAsync(userEmail);

            if (usuario == null)
                return Unauthorized("Usuário não encontrado.");

            if (!BCrypt.Net.BCrypt.Verify(dto.Senha, usuario.SenhaHash))
                return Unauthorized("Senha incorreta.");

            return Ok(new { valid = true, message = "Identidade confirmada." });
        }
    }

    public class ValidatePasswordDto
    {
        public string? Senha { get; set; }
    }
}
