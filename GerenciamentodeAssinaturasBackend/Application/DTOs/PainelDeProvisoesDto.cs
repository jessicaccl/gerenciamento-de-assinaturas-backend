namespace GerenciamentodeAssinaturasBackend.Application.DTOs
{
    public class PainelDeProvisoesDto
    {
        public DateTime DataReferencia { get; set; }

        public decimal Mrr { get; set; }
        public int AssinaturasAtivas { get; set; }
        public decimal TicketMedio { get; set; }

        public decimal AReceber30Dias { get; set; }
        public int QuantidadeAReceber30Dias { get; set; }

        public decimal Vencido { get; set; }
        public int QuantidadeVencida { get; set; }

        public decimal ProvisaoInadimplencia { get; set; }
        public decimal PercentualProvisionado { get; set; }

        public decimal RecebidoMesAtual { get; set; }

        public List<FaixaDeAgingDto> Aging { get; set; } = new();
        public List<PrevisaoMensalDto> Previsao { get; set; } = new();
        public List<ProximoVencimentoDto> ProximosVencimentos { get; set; } = new();
    }

    public class FaixaDeAgingDto
    {
        public string Faixa { get; set; } = string.Empty;
        public decimal PercentualDeRisco { get; set; }
        public decimal Valor { get; set; }
        public int Quantidade { get; set; }
        public decimal Provisao { get; set; }
    }

    public class PrevisaoMensalDto
    {
        public string Mes { get; set; } = string.Empty;   // yyyy-MM
        public decimal Recebido { get; set; }
        public decimal Previsto { get; set; }
        public decimal EmRisco { get; set; }
    }

    public class ProximoVencimentoDto
    {
        public int CobrancaId { get; set; }
        public string ClienteNome { get; set; } = string.Empty;
        public string PlanoNome { get; set; } = string.Empty;
        public DateTime DataVencimento { get; set; }
        public decimal Valor { get; set; }
    }
}
