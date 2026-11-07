using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssinaMais.Api.Domain.Entities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace AssinaMais.Api.Application.Services.Auth
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public const string EmissorPadrao = "AssinaMais.Api";
        public const string PublicoPadrao = "AssinaMais.Web";

        public string GenerateToken(Usuario usuario)
        {
            var secretKey = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Configuração 'Jwt:Key' não encontrada.");
            var key = Encoding.ASCII.GetBytes(secretKey);

            var tokenConfig = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    
                    new Claim(ClaimTypes.Email, usuario.Email),
                    
                    new Claim(ClaimTypes.Role, usuario.Cargo ?? "Colaborador")
                }),
                Issuer = _configuration["Jwt:Issuer"] ?? EmissorPadrao,
                Audience = _configuration["Jwt:Audience"] ?? PublicoPadrao,
                // Sessão de um dia de trabalho: um token vazado deixa de valer no mesmo dia
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenConfig);

            return tokenHandler.WriteToken(token);
        }
    }
}