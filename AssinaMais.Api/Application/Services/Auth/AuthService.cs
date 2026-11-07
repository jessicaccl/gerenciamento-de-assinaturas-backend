using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Interfaces;

namespace AssinaMais.Api.Application.Services.Auth
{
    public class AuthService
    {
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly TokenService _tokenService;

        public AuthService(IUsuarioRepository usuarioRepository, TokenService tokenService)
        {
            _usuarioRepository = usuarioRepository;
            _tokenService = tokenService;
        }

        public async Task<LoginResponseDto?> LoginAsync(LoginDto loginDto)
        {
            var usuario = await _usuarioRepository.GetUsuarioByEmailAsync(loginDto.Email);

            if (usuario == null) return null;

            bool senhaValida = BCrypt.Net.BCrypt.Verify(loginDto.Senha, usuario.SenhaHash);

            if (!senhaValida)
            {
                return null;
            }
                      
            var token = _tokenService.GenerateToken(usuario);

            return new LoginResponseDto
            {
                Email = usuario.Email,
                Token = token
            };
        }
    }
}