using AssinaMais.Api.Application.Services.Demo;

namespace AssinaMais.Api.BackgroundServices
{
    // Ligado só no deploy público (Demo:ResetDiario = true). Verifica de tempos em tempos, e não num horário fixo,
    // porque no plano gratuito do Render a API "dorme": o reset acontece no primeiro momento em que ela está
    // acordada depois do horário configurado.
    public class ResetDemoWorker : BackgroundService
    {
        private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(15);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ResetDemoWorker> _logger;
        private readonly bool _habilitado;
        private readonly int _horaDoReset;

        public ResetDemoWorker(IServiceProvider serviceProvider, ILogger<ResetDemoWorker> logger, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _habilitado = configuration.GetValue("Demo:ResetDiario", false);
            _horaDoReset = configuration.GetValue("Demo:HoraDoReset", 4);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_habilitado) return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (ServicoDeDadosDemo.JaPassouDoHorario(DateTime.Now, _horaDoReset))
                    {
                        using var scope = _serviceProvider.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<ServicoDeDadosDemo>().ResetarSeNecessarioAsync(DateTime.Today);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Demo] Falha ao recriar os dados de demonstração.");
                }

                await Task.Delay(Intervalo, stoppingToken);
            }
        }
    }
}
