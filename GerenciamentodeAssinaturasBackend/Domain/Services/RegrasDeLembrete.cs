using GerenciamentodeAssinaturasBackend.Domain.Entities;

namespace GerenciamentodeAssinaturasBackend.Domain.Services
{
    public static class TipoDeLembrete
    {
        public const string AvisoDeVencimento = "AVISO_VENCIMENTO";
        public const string CobrancaVencida = "COBRANCA_VENCIDA";
        public const string Manual = "MANUAL";
    }

    public static class RegrasDeLembrete
    {
        // Qual lembrete automático cabe para a cobrança hoje (ou nenhum).
        // Cada tipo é enviado uma única vez por cobrança; quem garante isso é o registro em LembretesCobranca.
        public static string? LembreteAutomatico(string status, DateTime dataVencimento, DateTime hoje, int diasDeAntecedencia)
        {
            var diasParaVencer = (dataVencimento.Date - hoje.Date).Days;

            if (status == StatusCobranca.Pendente && diasParaVencer >= 0 && diasParaVencer <= diasDeAntecedencia)
                return TipoDeLembrete.AvisoDeVencimento;

            if (status == StatusCobranca.Vencida)
                return TipoDeLembrete.CobrancaVencida;

            return null;
        }

        public static (string Assunto, string Titulo, string Mensagem) Texto(string tipo, int diasEmAtraso, DateTime dataVencimento, DateTime hoje)
        {
            var diasParaVencer = (dataVencimento.Date - hoje.Date).Days;

            if (diasEmAtraso > 0)
                return ("Sua assinatura Assina+ está com pagamento em atraso",
                        "Identificamos um pagamento em aberto",
                        $"A cobrança abaixo venceu há {diasEmAtraso} dia(s). Se você já pagou, desconsidere esta mensagem.");

            var quando = diasParaVencer switch
            {
                0 => "vence hoje",
                1 => "vence amanhã",
                _ => $"vence em {diasParaVencer} dias"
            };

            return tipo == TipoDeLembrete.Manual
                ? ($"Lembrete: sua cobrança Assina+ {quando}", "Lembrete de pagamento", $"Passando para lembrar que a cobrança abaixo {quando}.")
                : ($"Sua cobrança Assina+ {quando}", "Sua próxima cobrança está chegando", $"A cobrança abaixo {quando}. Mantenha seu pagamento em dia para não interromper o serviço.");
        }
    }
}
