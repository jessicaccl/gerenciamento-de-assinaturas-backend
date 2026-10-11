using System.ComponentModel.DataAnnotations; 

namespace GerenciamentoAssinaturas.DTOs
{
    public class CreateAssinaturaDto
    {
        [Required(ErrorMessage = "O ID do plano é obrigatório")]
        public int PlanoId { get; set; }

        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        public string? ClienteNome { get; set; }

        [Required(ErrorMessage = "O ciclo de cobrança é obrigatório")]
        public string? TipoRecorrencia { get; set; } 

        [Required(ErrorMessage = "A forma de pagamento é obrigatória")]
        public string? FormaPagamento { get; set; } 
    }
}