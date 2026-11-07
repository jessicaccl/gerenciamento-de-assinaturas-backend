using System.ComponentModel.DataAnnotations;

namespace AssinaMais.Api.Application.DTOs
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "O nome é obrigatório")]
        [StringLength(100)]
        public string? Nome { get; set; }

        public string? CPF { get; set; }

        [Required(ErrorMessage = "O e-mail é obrigatório")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "A senha é obrigatória")]
        [MinLength(8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "A senha deve ter letras e números")]
        public string? Senha { get; set; }
    }
}
