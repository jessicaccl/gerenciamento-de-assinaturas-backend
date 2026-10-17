using System.ComponentModel.DataAnnotations;

namespace GerenciamentoAssinaturas.DTOs
{
    public class UpdateAssinaturaDto
    {
        [Required]
        public int Id { get; set; } 

        public int PlanoId { get; set; }

        [EmailAddress(ErrorMessage = "E-mail do cliente inválido")]
        [StringLength(150)]
        public string? ClienteEmail { get; set; }
        public string? TipoRecorrencia { get; set; }
        public string? FormaPagamento { get; set; }
    }
}