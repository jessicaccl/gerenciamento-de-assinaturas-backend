using System;

namespace GerenciamentoAssinaturas.Domain.Entities 
{
    public class Assinatura
    {
        public int Id { get; set; }
        public string? ClienteNome { get; set; }
        public int PlanoId { get; set; }
        public string? TipoRecorrencia { get; set; } 
        public string? FormaPagamento { get; set; }  
        public DateTime DataInicio { get; set; }
        public DateTime? DataExpiracao { get; set; }
        public string? Status { get; set; }
    }
}