using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Services;
using System.Globalization;
using System.Text;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas
{
    // CSV no padrão que o Excel em português abre direto: separador ";", vírgula decimal e UTF-8 com BOM
    public static class ExportadorCsv
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private static readonly string[] Cabecalho =
        {
            "Cliente", "E-mail", "Plano", "Ciclo", "Início do período", "Fim do período", "Vencimento",
            "Valor", "Status", "Dias em atraso", "Provisão", "Data do pagamento", "Forma de pagamento"
        };

        public static byte[] Cobrancas(IEnumerable<CobrancaResponseDto> cobrancas)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(';', Cabecalho));

            foreach (var c in cobrancas)
            {
                var provisao = c.Status == StatusCobranca.Vencida ? RegrasDeProvisao.CalcularProvisao(c.Valor, c.DiasEmAtraso) : 0m;

                csv.AppendLine(string.Join(';', new[]
                {
                    Campo(c.ClienteNome),
                    Campo(c.ClienteEmail),
                    Campo(c.PlanoNome),
                    Campo(c.TipoRecorrencia),
                    Data(c.PeriodoInicio),
                    Data(c.PeriodoFim),
                    Data(c.DataVencimento),
                    Valor(c.Valor),
                    Campo(c.Status),
                    c.Status == StatusCobranca.Vencida ? c.DiasEmAtraso.ToString() : "",
                    Valor(provisao),
                    c.DataPagamento.HasValue ? Data(c.DataPagamento.Value) : "",
                    Campo(c.FormaPagamento)
                }));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }

        private static string Data(DateTime data) => data.ToString("dd/MM/yyyy");

        private static string Valor(decimal valor) => valor.ToString("0.00", PtBr);

        // Aspas quando o texto tem separador, aspas ou quebra de linha (RFC 4180).
        // Textos que começam com =, +, - ou @ ganham um apóstrofo para o Excel não interpretar como fórmula.
        private static string Campo(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";

            if ("=+-@".Contains(texto[0])) texto = "'" + texto;

            return texto.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0
                ? $"\"{texto.Replace("\"", "\"\"")}\""
                : texto;
        }
    }
}
