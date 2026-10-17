using System.ComponentModel.DataAnnotations;

namespace GerenciamentodeAssinaturasBackend.Application.DTOs
{
    public class SalvarClienteDto
    {
        [Required(ErrorMessage = "O nome do cliente é obrigatório")]
        [StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres")]
        public string Nome { get; set; } = string.Empty;

        // Opcional: sem e-mail, o cliente não recebe lembretes de cobrança
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        [StringLength(150)]
        public string? Email { get; set; }

        // CPF ou CNPJ, com ou sem pontuação (validado pelos dígitos verificadores)
        public string? Documento { get; set; }

        [StringLength(20)]
        public string? Telefone { get; set; }
    }

    // Linha da listagem: documento sempre mascarado
    public class ClienteResumoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? TipoDocumento { get; set; }
        public string DocumentoMascarado { get; set; } = string.Empty;
        public string? Telefone { get; set; }
        public DateTime DataCadastro { get; set; }
        public decimal Mrr { get; set; }
        public decimal EmAberto { get; set; }
        public decimal Vencido { get; set; }
        public int AssinaturasAtivas { get; set; }
        public string Situacao { get; set; } = string.Empty;
    }

    // Ficha completa: aqui o documento aparece inteiro
    public class FichaClienteDto : ClienteResumoDto
    {
        public string? Documento { get; set; }
        public List<AssinaturaDoClienteDto> Assinaturas { get; set; } = new();
        public List<CobrancaResponseDto> Cobrancas { get; set; } = new();
    }

    public class AssinaturaDoClienteDto
    {
        public int Id { get; set; }
        public string PlanoNome { get; set; } = string.Empty;
        public string TipoRecorrencia { get; set; } = string.Empty;
        public string FormaPagamento { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public DateTime? DataExpiracao { get; set; }
    }

    // Dados brutos que o repositório entrega para montar o resumo
    public class ClienteComTotais
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Documento { get; set; }
        public string? Telefone { get; set; }
        public DateTime DataCadastro { get; set; }
        public decimal ValorPendente { get; set; }
        public decimal ValorVencido { get; set; }
        public int QuantidadePendente { get; set; }
        public int QuantidadeVencida { get; set; }
    }
}
