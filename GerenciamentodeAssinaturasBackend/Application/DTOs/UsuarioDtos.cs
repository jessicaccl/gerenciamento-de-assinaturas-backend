using System.ComponentModel.DataAnnotations;

namespace GerenciamentodeAssinaturasBackend.Application.DTOs
{
    public static class Cargos
    {
        public const string Admin = "Admin";
        public const string Colaborador = "Colaborador";

        public static bool EhValido(string? cargo) => cargo == Admin || cargo == Colaborador;
    }

    // Dados públicos do usuário: nunca expõe SenhaHash nem CPF
    public class UsuarioResponseDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Cargo { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
    }

    public class UpdateUsuarioDto
    {
        [Required]
        public int Id { get; set; }

        [Required(ErrorMessage = "O nome é obrigatório")]
        [StringLength(100)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "O e-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;

        // Só é considerado quando quem edita é Admin
        public string? Cargo { get; set; }
    }
}
