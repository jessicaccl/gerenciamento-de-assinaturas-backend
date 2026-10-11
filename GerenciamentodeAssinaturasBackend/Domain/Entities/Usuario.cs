namespace GerenciamentodeAssinaturasBackend.Domain.Entities
{
    public class Usuario
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string SenhaHash { get; set; } = string.Empty;

        public string CPF { get; set; } = string.Empty;

        public string Cargo { get; set; } = string.Empty;

        public DateTime DataCriacao { get; set; } = DateTime.Now;
        public string? TokenConfirmacao { get; set; }
        public bool EmailConfirmado { get; set; }
        
    }
}