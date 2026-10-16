namespace GerenciamentodeAssinaturasBackend.Domain.Services
{
    public record FaixaDeAtraso(string Nome, int DiasMinimo, int? DiasMaximo, decimal PercentualDeRisco);

    // Provisão para devedores duvidosos: quanto mais antiga a dívida, maior a chance de não ser recebida
    public static class RegrasDeProvisao
    {
        public static readonly IReadOnlyList<FaixaDeAtraso> Faixas = new[]
        {
            new FaixaDeAtraso("1 a 30 dias", 1, 30, 30m),
            new FaixaDeAtraso("31 a 60 dias", 31, 60, 50m),
            new FaixaDeAtraso("61 a 90 dias", 61, 90, 80m),
            new FaixaDeAtraso("Mais de 90 dias", 91, null, 100m),
        };

        public static FaixaDeAtraso? Classificar(int diasEmAtraso) =>
            Faixas.FirstOrDefault(f => diasEmAtraso >= f.DiasMinimo && (f.DiasMaximo == null || diasEmAtraso <= f.DiasMaximo));

        public static decimal CalcularProvisao(decimal valor, int diasEmAtraso)
        {
            var faixa = Classificar(diasEmAtraso);
            return faixa == null ? 0m : Math.Round(valor * faixa.PercentualDeRisco / 100m, 2, MidpointRounding.AwayFromZero);
        }
    }
}
