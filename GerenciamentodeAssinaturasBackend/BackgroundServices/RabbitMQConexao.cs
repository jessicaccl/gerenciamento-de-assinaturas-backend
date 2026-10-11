using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace GerenciamentodeAssinaturasBackend.BackgroundServices
{
    public static class RabbitMQConexao
    {
        // RabbitMQ:Url (ex.: amqps://usuario:senha@host/vhost, usado em serviços gerenciados) tem prioridade sobre RabbitMQ:Host
        public static ConnectionFactory CriarFactory(IConfiguration configuration)
        {
            var url = configuration["RabbitMQ:Url"];
            if (!string.IsNullOrWhiteSpace(url))
                return new ConnectionFactory { Uri = new Uri(url) };

            return new ConnectionFactory { HostName = configuration["RabbitMQ:Host"] ?? "localhost" };
        }

        // Tenta conectar até o broker ficar disponível, em vez de derrubar a aplicação na inicialização
        public static async Task<IConnection> ConectarComRetentativaAsync(
            ConnectionFactory factory, ILogger logger, CancellationToken stoppingToken)
        {
            var espera = TimeSpan.FromSeconds(2);

            while (true)
            {
                try
                {
                    return await factory.CreateConnectionAsync(stoppingToken);
                }
                catch (BrokerUnreachableException)
                {
                    logger.LogWarning("RabbitMQ indisponível em {Host}. Nova tentativa em {Segundos}s.", factory.HostName, espera.TotalSeconds);
                    await Task.Delay(espera, stoppingToken);
                    espera = TimeSpan.FromSeconds(Math.Min(espera.TotalSeconds * 2, 30));
                }
            }
        }
    }
}
