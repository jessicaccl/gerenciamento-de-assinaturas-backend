namespace GerenciamentodeAssinaturasBackend.Domain.Services
{
    public record PeriodoDeCobranca(DateTime Inicio, DateTime Fim);

    // Regras de preço e de período das cobranças. Funções puras: sem banco, sem relógio, fáceis de testar.
    public static class RegrasDeCobranca
    {
        public const string Mensal = "Mensal";
        public const string Trimestral = "Trimestral";
        public const string Semestral = "Semestral";
        public const string Anual = "Anual";

        public static readonly IReadOnlyList<string> Recorrencias = new[] { Mensal, Trimestral, Semestral, Anual };

        // Limite de segurança para não gerar um volume absurdo de cobranças de uma vez (ex.: data de início muito antiga)
        private const int MaximoPeriodosPorExecucao = 60;

        public static bool EhRecorrenciaValida(string? recorrencia) =>
            Recorrencias.Any(r => string.Equals(r, recorrencia, StringComparison.OrdinalIgnoreCase));

        // Duração do ciclo em meses
        public static int MesesDoCiclo(string recorrencia) => Normalizar(recorrencia) switch
        {
            Mensal => 1,
            Trimestral => 3,
            Semestral => 6,
            Anual => 12,
            _ => throw new ArgumentException($"Recorrência inválida: {recorrencia}", nameof(recorrencia))
        };

        // Quantos meses são efetivamente cobrados no ciclo: semestral ganha 1 mês grátis e anual ganha 2
        public static int MesesCobrados(string recorrencia) => Normalizar(recorrencia) switch
        {
            Mensal => 1,
            Trimestral => 3,
            Semestral => 5,
            Anual => 10,
            _ => throw new ArgumentException($"Recorrência inválida: {recorrencia}", nameof(recorrencia))
        };

        public static decimal CalcularValor(decimal valorMensalDoPlano, string recorrencia) =>
            Math.Round(valorMensalDoPlano * MesesCobrados(recorrencia), 2, MidpointRounding.AwayFromZero);

        // Valor mensal equivalente (base do MRR): o desconto do ciclo diluído pelos meses
        public static decimal ValorMensalEquivalente(decimal valorMensalDoPlano, string recorrencia) =>
            Math.Round(CalcularValor(valorMensalDoPlano, recorrencia) / MesesDoCiclo(recorrencia), 2, MidpointRounding.AwayFromZero);

        public static decimal PercentualDeDesconto(string recorrencia) =>
            Math.Round(1m - (decimal)MesesCobrados(recorrencia) / MesesDoCiclo(recorrencia), 4) * 100;

        // diaAncora: dia do mês em que a assinatura começou. Evita que o vencimento "escorregue" depois de um mês curto:
        // 31/01 -> 28/02 -> 31/03 (e não 28/03, 28/04...)
        public static DateTime FimDoPeriodo(DateTime inicio, string recorrencia, int? diaAncora = null)
        {
            var meses = MesesDoCiclo(recorrencia);
            var dia = inicio.Day;

            var inicioFoiAjustadoParaFimDoMes = inicio.Day == DateTime.DaysInMonth(inicio.Year, inicio.Month);
            if (diaAncora > inicio.Day && inicioFoiAjustadoParaFimDoMes)
                dia = diaAncora.Value;

            var mesAlvo = new DateTime(inicio.Year, inicio.Month, 1).AddMonths(meses);
            return new DateTime(mesAlvo.Year, mesAlvo.Month, Math.Min(dia, DateTime.DaysInMonth(mesAlvo.Year, mesAlvo.Month)));
        }

        // Períodos que já devem ter cobrança: os que começam até a data limite (hoje + dias de antecedência)
        public static IReadOnlyList<PeriodoDeCobranca> PeriodosAGerar(DateTime proximoInicio, string recorrencia, DateTime limite, int? diaAncora = null)
        {
            var periodos = new List<PeriodoDeCobranca>();
            var inicio = proximoInicio.Date;

            while (inicio <= limite.Date && periodos.Count < MaximoPeriodosPorExecucao)
            {
                var fim = FimDoPeriodo(inicio, recorrencia, diaAncora);
                periodos.Add(new PeriodoDeCobranca(inicio, fim));
                inicio = fim;
            }

            return periodos;
        }

        public static string Normalizar(string recorrencia) =>
            Recorrencias.FirstOrDefault(r => string.Equals(r, recorrencia?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? recorrencia;
    }
}
