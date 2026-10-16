using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GerenciamentodeAssinaturas.Tests
{
    public static class TokenDeTeste
    {
        // Mesma chave do appsettings.Development.json (ambiente usado pelo WebApplicationFactory)
        private const string Chave = "chave-somente-para-desenvolvimento-local-assina-mais";

        public static string Gerar(string cargo, int usuarioId = 999)
        {
            var key = Encoding.ASCII.GetBytes(Chave);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()),
                    new Claim(ClaimTypes.Email, $"{cargo.ToLower()}@teste.com"),
                    new Claim(ClaimTypes.Role, cargo)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
        }
    }
}
