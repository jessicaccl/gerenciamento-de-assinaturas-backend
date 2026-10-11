using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace GerenciamentodeAssinaturasBackend.BackgroundServices
{
    public class AssinaturaWorker : BackgroundService
    {
        private readonly ILogger<AssinaturaWorker> _logger;
        private readonly ConnectionFactory _factory;
        private readonly string[] _filas = { "assinaturas_eventos", "planos_eventos", "usuarios_eventos", "auth_eventos" };

        public AssinaturaWorker(ILogger<AssinaturaWorker> logger, IConfiguration configuration)
        {
            _logger = logger;
            _factory = RabbitMQConexao.CriarFactory(configuration);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var connection = await RabbitMQConexao.ConectarComRetentativaAsync(_factory, _logger, stoppingToken);
            using var channel = await connection.CreateChannelAsync();

            foreach (var fila in _filas)
            {
                await channel.QueueDeclareAsync(queue: fila, durable: false, exclusive: false, autoDelete: false, arguments: null);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (model, ea) =>
                {
                    var message = Encoding.UTF8.GetString(ea.Body.ToArray());
                    _logger.LogInformation($"[FILA: {fila}] MENSAGEM: {message}");
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                };

                await channel.BasicConsumeAsync(queue: fila, autoAck: false, consumer: consumer);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}