using System.ComponentModel.DataAnnotations;

namespace AssinaMais.Api.Application.DTOs
{
    // ?pagina=1&tamanhoPagina=20. Sem "pagina", a rota devolve a lista completa (usado por listas de seleção, como o cliente no modal de assinatura).
    public class PaginacaoDto
    {
        public const int TamanhoMaximo = 100;

        [Range(1, int.MaxValue, ErrorMessage = "A página começa em 1")]
        public int? Pagina { get; set; }

        [Range(1, TamanhoMaximo, ErrorMessage = "O tamanho da página vai de 1 a 100")]
        public int TamanhoPagina { get; set; } = 20;

        // null em LIMIT, no PostgreSQL, significa "sem limite"
        public int? Limite => Pagina.HasValue ? TamanhoPagina : null;
        public int Deslocamento => Pagina.HasValue ? (Pagina.Value - 1) * TamanhoPagina : 0;
    }

    public record PaginaDe<T>(IReadOnlyList<T> Itens, int Total);
}
