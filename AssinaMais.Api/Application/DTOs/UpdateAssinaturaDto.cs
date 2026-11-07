using System.ComponentModel.DataAnnotations;

namespace AssinaMais.Api.Application.DTOs
{
    public class UpdateAssinaturaDto
    {
        [Required]
        public int Id { get; set; } 

        public int PlanoId { get; set; }
        public string? TipoRecorrencia { get; set; }
        public string? FormaPagamento { get; set; }
    }
}