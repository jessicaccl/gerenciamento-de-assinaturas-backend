using System.ComponentModel.DataAnnotations; 

namespace GerenciamentoAssinaturas.DTOs
{
    public class CreateAssinaturaDto
    {
        [Required(ErrorMessage = "O ID do plano é obrigatório")]
        public int PlanoId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "O cliente é obrigatório")]
        public int ClienteId { get; set; }

        [Required(ErrorMessage = "O ciclo de cobrança é obrigatório")]
        public string? TipoRecorrencia { get; set; } 

        [Required(ErrorMessage = "A forma de pagamento é obrigatória")]
        public string? FormaPagamento { get; set; } 
    }
}