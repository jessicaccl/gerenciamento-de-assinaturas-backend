using System.ComponentModel.DataAnnotations;

namespace GerenciamentodeAssinaturasBackend.Application.DTOs
{
    public class CobrancaResponseDto
    {
        public int Id { get; set; }
        public int AssinaturaId { get; set; }
        public int ClienteId { get; set; }
        public string ClienteNome { get; set; } = string.Empty;
        public string? ClienteEmail { get; set; }
        public string PlanoNome { get; set; } = string.Empty;
        public string TipoRecorrencia { get; set; } = string.Empty;
        public DateTime PeriodoInicio { get; set; }
        public DateTime PeriodoFim { get; set; }
        public DateTime DataVencimento { get; set; }
        public decimal Valor { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? DataPagamento { get; set; }
        public string? FormaPagamento { get; set; }
        public int DiasEmAtraso { get; set; }
        public DateTime? UltimoLembrete { get; set; }
    }

    public class FiltroCobrancasDto
    {
        public string? Status { get; set; }
        public int? AssinaturaId { get; set; }
        public int? ClienteId { get; set; }
        public DateTime? VencimentoDe { get; set; }
        public DateTime? VencimentoAte { get; set; }
    }

    public class RegistrarPagamentoDto
    {
        // Se não informada, considera a data de hoje
        public DateTime? DataPagamento { get; set; }

        [StringLength(50)]
        public string? FormaPagamento { get; set; }
    }

    public class CicloDePrecoDto
    {
        public string Recorrencia { get; set; } = string.Empty;
        public int Meses { get; set; }
        public int MesesCobrados { get; set; }
        public decimal Valor { get; set; }
        public decimal ValorMensalEquivalente { get; set; }
        public decimal PercentualDesconto { get; set; }
    }
}
