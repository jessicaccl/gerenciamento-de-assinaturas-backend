namespace GerenciamentodeAssinaturasBackend.Domain.Entities
{
    public class Plano
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty; 
        public decimal Valor { get; set; } 
        public string Descricao { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
    }
}