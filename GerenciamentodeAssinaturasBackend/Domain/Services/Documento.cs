namespace GerenciamentodeAssinaturasBackend.Domain.Services
{
    // CPF e CNPJ: validação pelos dígitos verificadores, formatação e máscara para listagens
    public static class Documento
    {
        public static string ApenasDigitos(string? documento) =>
            new string((documento ?? string.Empty).Where(char.IsDigit).ToArray());

        public static bool EhValido(string? documento)
        {
            var digitos = ApenasDigitos(documento);
            return digitos.Length switch
            {
                11 => CpfValido(digitos),
                14 => CnpjValido(digitos),
                _ => false
            };
        }

        public static string? Tipo(string? documento) => ApenasDigitos(documento).Length switch
        {
            11 => "CPF",
            14 => "CNPJ",
            _ => null
        };

        public static string Formatar(string? documento)
        {
            var d = ApenasDigitos(documento);
            return d.Length switch
            {
                11 => $"{d[..3]}.{d[3..6]}.{d[6..9]}-{d[9..]}",
                14 => $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}",
                _ => documento ?? string.Empty
            };
        }

        // Na listagem, mostra só o suficiente para conferir (LGPD: minimização de dados).
        // CPF: ***.456.789-**   CNPJ: **.***.678/0001-90 (no CNPJ, o final identifica a filial)
        public static string Mascarar(string? documento)
        {
            var d = ApenasDigitos(documento);
            return d.Length switch
            {
                11 => $"***.{d[3..6]}.{d[6..9]}-**",
                14 => $"**.***.{d[5..8]}/{d[8..12]}-{d[12..]}",
                _ => string.Empty
            };
        }

        private static bool CpfValido(string cpf)
        {
            if (cpf.Distinct().Count() == 1) return false;   // 111.111.111-11 e afins passam na conta, mas não existem

            return cpf[9] - '0' == DigitoVerificador(cpf[..9], 10)
                && cpf[10] - '0' == DigitoVerificador(cpf[..10], 11);
        }

        private static bool CnpjValido(string cnpj)
        {
            if (cnpj.Distinct().Count() == 1) return false;

            int[] pesos1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
            int[] pesos2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

            return cnpj[12] - '0' == DigitoPorPesos(cnpj[..12], pesos1)
                && cnpj[13] - '0' == DigitoPorPesos(cnpj[..13], pesos2);
        }

        private static int DigitoVerificador(string numeros, int pesoInicial)
        {
            var soma = numeros.Select((c, i) => (c - '0') * (pesoInicial - i)).Sum();
            var resto = soma * 10 % 11;
            return resto == 10 ? 0 : resto;
        }

        private static int DigitoPorPesos(string numeros, int[] pesos)
        {
            var soma = numeros.Select((c, i) => (c - '0') * pesos[i]).Sum();
            var resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }
    }
}
