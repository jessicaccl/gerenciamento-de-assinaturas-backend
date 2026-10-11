using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.BackgroundServices;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Integrations
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly ConnectionFactory _factory;

        public RabbitMQService(IConfiguration configuration)
        {
            _factory = RabbitMQConexao.CriarFactory(configuration);
        }

        public async Task EnviarMensagemAsync<T>(T mensagem, string nomeFila)
        {
            using var connection = await _factory.CreateConnectionAsync();
            using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(
                queue: nomeFila,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var json = JsonSerializer.Serialize(mensagem);
            var body = Encoding.UTF8.GetBytes(json);

            await channel.BasicPublishAsync(
                exchange: "",
                routingKey: nomeFila,
                body: body
            );
        }
    }

    // Usado quando RabbitMQ:Habilitado = false (ex.: deploy gratuito sem broker): os eventos são apenas registrados em log
    public class RabbitMQDesabilitadoService : IRabbitMQService
    {
        private readonly ILogger<RabbitMQDesabilitadoService> _logger;

        public RabbitMQDesabilitadoService(ILogger<RabbitMQDesabilitadoService> logger)
        {
            _logger = logger;
        }

        public Task EnviarMensagemAsync<T>(T mensagem, string nomeFila)
        {
            _logger.LogInformation("[Fila: {Fila}] (RabbitMQ desabilitado) {Mensagem}", nomeFila, JsonSerializer.Serialize(mensagem));
            return Task.CompletedTask;
        }
    }
}
