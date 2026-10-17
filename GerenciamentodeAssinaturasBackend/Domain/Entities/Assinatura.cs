using System;

namespace GerenciamentoAssinaturas.Domain.Entities 
{
    public class Assinatura
    {
        public int Id { get; set; }
        public int ClienteId { get; set; }

        // Vêm do cadastro do cliente (JOIN), só para exibição
        public string? ClienteNome { get; set; }
        public string? ClienteEmail { get; set; }
        public int PlanoId { get; set; }
        public string? TipoRecorrencia { get; set; } 
        public string? FormaPagamento { get; set; }  
        public DateTime DataInicio { get; set; }
        public DateTime? DataExpiracao { get; set; }
        public string? Status { get; set; }
    }
}