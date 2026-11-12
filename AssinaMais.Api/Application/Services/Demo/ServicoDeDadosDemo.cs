using AssinaMais.Api.Application.Services.Cobrancas;
using AssinaMais.Api.Infrastructure.Data;
using Dapper;
using System.Data;

namespace AssinaMais.Api.Application.Services.Demo
{
    // Recria os dados de demonstração uma vez por dia no deploy público, para que a demo
    // continue arrumada mesmo depois de visitantes excluírem planos ou cancelarem cobranças.
    public class ServicoDeDadosDemo
    {
        // Número arbitrário que identifica o "cadeado" do reset no PostgreSQL
        private const long ChaveDoCadeado = 7_042_026;

        private readonly DapperContext _context;
        private readonly GeradorDeCobrancas _geradorDeCobrancas;
        private readonly ILogger<ServicoDeDadosDemo> _logger;

        public ServicoDeDadosDemo(DapperContext context, GeradorDeCobrancas geradorDeCobrancas, ILogger<ServicoDeDadosDemo> logger)
        {
            _context = context;
            _geradorDeCobrancas = geradorDeCobrancas;
            _logger = logger;
        }

        public static bool JaPassouDoHorario(DateTime agora, int horaDoReset) => agora.Hour >= horaDoReset;

        // Retorna true se o reset rodou agora; false se o de hoje já tinha sido feito (ou se outra instância está fazendo)
        public async Task<bool> ResetarSeNecessarioAsync(DateTime hoje)
        {
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Demo", "dados-demo.sql"));

            using var conn = _context.CreateConnection();
            conn.Open();
            using var transacao = conn.BeginTransaction();

            // Cadeado de transação: duas instâncias da API nunca resetam ao mesmo tempo
            if (!await conn.ExecuteScalarAsync<bool>("SELECT pg_try_advisory_xact_lock(@Chave)", new { Chave = ChaveDoCadeado }, transacao))
                return false;

            // A chave primária por dia garante um único reset diário, mesmo depois de a API reiniciar
            var registrou = await conn.ExecuteAsync(
                "INSERT INTO ResetsDemo (Dia) VALUES (@Dia) ON CONFLICT (Dia) DO NOTHING",
                new { Dia = hoje.Date }, transacao);

            if (registrou == 0)
                return false;

            await conn.ExecuteAsync(
                "TRUNCATE LembretesCobranca, Cobrancas, Assinaturas, Clientes, Planos, Usuarios RESTART IDENTITY CASCADE",
                transaction: transacao);
            await conn.ExecuteAsync(script, transaction: transacao);

            transacao.Commit();

            // Emite os próximos ciclos na hora, sem esperar a próxima rodada do worker de cobranças
            await _geradorDeCobrancas.ProcessarAsync(hoje);

            _logger.LogInformation("[Demo] Dados de demonstração recriados em {Dia:dd/MM/yyyy}.", hoje);
            return true;
        }
    }
}
