namespace GerenciamentodeAssinaturasBackend.Domain.Entities
{
    public class Cliente
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Documento { get; set; }   // só dígitos (CPF ou CNPJ)
        public string? Telefone { get; set; }
        public DateTime DataCadastro { get; set; }
    }

    public static class SituacaoCliente
    {
        public const string EmDia = "Em dia";
        public const string AVencer = "A vencer";
        public const string Inadimplente = "Inadimplente";

        // Qualquer cobrança vencida torna o cliente inadimplente, mesmo que ele tenha outras em dia
        public static string Calcular(int cobrancasVencidas, int cobrancasPendentes) =>
            cobrancasVencidas > 0 ? Inadimplente
            : cobrancasPendentes > 0 ? AVencer
            : EmDia;
    }
}
