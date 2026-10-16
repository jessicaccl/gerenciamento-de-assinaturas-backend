namespace GerenciamentodeAssinaturasBackend.Domain.Entities
{
    public static class StatusCobranca
    {
        public const string Pendente = "Pendente";
        public const string Vencida = "Vencida";
        public const string Paga = "Paga";
        public const string Cancelada = "Cancelada";
    }

    public class Cobranca
    {
        public int Id { get; set; }
        public int AssinaturaId { get; set; }
        public DateTime PeriodoInicio { get; set; }
        public DateTime PeriodoFim { get; set; }
        public DateTime DataVencimento { get; set; }
        public decimal Valor { get; set; }
        public string Status { get; set; } = StatusCobranca.Pendente;
        public DateTime? DataPagamento { get; set; }
        public string? FormaPagamento { get; set; }
        public DateTime DataCriacao { get; set; }
    }

    // Dados que o gerador precisa de cada assinatura ativa
    public class AssinaturaParaFaturamento
    {
        public int AssinaturaId { get; set; }
        public string TipoRecorrencia { get; set; } = string.Empty;
        public DateTime DataInicio { get; set; }
        public decimal ValorMensalPlano { get; set; }
        public DateTime? UltimoPeriodoFim { get; set; }
    }
}
