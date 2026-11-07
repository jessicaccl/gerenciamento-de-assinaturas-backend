using System.ComponentModel.DataAnnotations;

namespace AssinaMais.Api.Application.DTOs
{
    // Entrada para criar ou editar plano: só os campos que o cliente da API pode definir (o Id vem da URL)
    public class SalvarPlanoDto
    {
        [Required(ErrorMessage = "O nome do plano é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
        public string Nome { get; set; } = string.Empty;

        [Range(0.01, 100000, ErrorMessage = "O valor mensal deve ser maior que zero")]
        public decimal Valor { get; set; }

        [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres")]
        public string Descricao { get; set; } = string.Empty;

        public bool Ativo { get; set; } = true;
    }
}
