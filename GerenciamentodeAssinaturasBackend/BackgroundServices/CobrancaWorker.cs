using GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas;

namespace GerenciamentodeAssinaturasBackend.BackgroundServices
{
    // Roda na subida da API e depois em intervalos: emite as cobranças dos novos ciclos e marca as vencidas
    public class CobrancaWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CobrancaWorker> _logger;
        private readonly TimeSpan _intervalo;

        public CobrancaWorker(IServiceProvider serviceProvider, ILogger<CobrancaWorker> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _intervalo = TimeSpan.FromMinutes(configuration.GetValue("Cobrancas:IntervaloMinutos", 60));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var gerador = scope.ServiceProvider.GetRequiredService<GeradorDeCobrancas>();
                    await gerador.ProcessarAsync(DateTime.Today);
                }
                catch (Exception ex)
                {
                    // Uma falha (ex.: banco indisponível) não derruba a API; tenta de novo no próximo ciclo
                    _logger.LogError(ex, "[Cobranças] Falha ao processar cobranças.");
                }

                await Task.Delay(_intervalo, stoppingToken);
            }
        }
    }
}
