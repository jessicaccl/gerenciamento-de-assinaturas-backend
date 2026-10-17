using System.Net;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;

namespace GerenciamentodeAssinaturasBackend.BackgroundServices
{
    // Consome a fila de e-mails e monta cada mensagem a partir de um template HTML em /Templates
    public class EmailConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailConsumer> _logger;
        private readonly ConnectionFactory _factory;
        private readonly string _urlFrontend;

        private IConnection? _connection;
        private IChannel? _channel;

        private const string QueueName = "emails_queue";

        public EmailConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<EmailConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _factory = RabbitMQConexao.CriarFactory(configuration);
            _urlFrontend = configuration["App:UrlFrontend"] ?? "http://localhost:5173";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _connection = await RabbitMQConexao.ConectarComRetentativaAsync(_factory, _logger, stoppingToken);
                _channel = await _connection.CreateChannelAsync(null, stoppingToken);

                await _channel.QueueDeclareAsync(queue: QueueName, durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);

                consumer.ReceivedAsync += async (model, ea) =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(ea.Body.ToArray()));
                        var email = await MontarEmailAsync(doc.RootElement);

                        if (email != null)
                            await EnviarEmailComEscopo(email.Value.Destino, email.Value.Assunto, email.Value.Corpo);

                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        // Mensagem com problema é descartada (sem reenfileirar) para não travar a fila
                        _logger.LogError(ex, "[EmailConsumer] Falha ao processar mensagem.");
                        await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                    }
                };

                await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Encerramento normal da aplicação
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmailConsumer] Falha crítica.");
            }
        }

        private async Task<(string Destino, string Assunto, string Corpo)?> MontarEmailAsync(JsonElement mensagem)
        {
            var tipo = Texto(mensagem, "Tipo");
            var destino = Texto(mensagem, "EmailDestino");
            var nome = Texto(mensagem, "Nome");

            if (string.IsNullOrWhiteSpace(destino))
            {
                _logger.LogWarning("[EmailConsumer] Mensagem do tipo {Tipo} sem destinatário ignorada.", tipo);
                return null;
            }

            switch (tipo)
            {
                case "BOAS_VINDAS":
                    return (destino, "Bem-vindo(a) ao Assina+! 🎉", await RenderizarAsync("BoasVindas.html", new()
                    {
                        ["NOME"] = nome,
                        ["URL_SISTEMA"] = _urlFrontend
                    }));

                case "LEMBRETE_COBRANCA":
                    return (destino, Texto(mensagem, "Assunto"), await RenderizarAsync("LembreteCobranca.html", new()
                    {
                        ["NOME"] = nome,
                        ["TITULO"] = Texto(mensagem, "Titulo"),
                        ["MENSAGEM"] = Texto(mensagem, "Mensagem"),
                        ["PLANO"] = Texto(mensagem, "Plano"),
                        ["VALOR"] = Texto(mensagem, "Valor"),
                        ["VENCIMENTO"] = Texto(mensagem, "Vencimento"),
                        ["URL_SISTEMA"] = _urlFrontend
                    }));

                default:
                    _logger.LogWarning("[EmailConsumer] Tipo de e-mail desconhecido: {Tipo}", tipo);
                    return null;
            }
        }

        private static string Texto(JsonElement mensagem, string propriedade) =>
            mensagem.TryGetProperty(propriedade, out var valor) ? valor.ToString() : string.Empty;

        // Os valores são escapados: o nome do cliente vem do cadastro e não pode injetar HTML no e-mail
        private static async Task<string> RenderizarAsync(string template, Dictionary<string, string> valores)
        {
            var caminho = Path.Combine(AppContext.BaseDirectory, "Templates", template);
            var html = await File.ReadAllTextAsync(caminho);

            foreach (var (chave, valor) in valores)
            {
                var conteudo = chave == "URL_SISTEMA" ? valor : WebUtility.HtmlEncode(valor);
                html = html.Replace("{{" + chave + "}}", conteudo);
            }

            return html;
        }

        private async Task EnviarEmailComEscopo(string email, string assunto, string corpoHtml)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await emailService.EnviarEmailAsync(email, assunto, corpoHtml);
            _logger.LogInformation("[EmailConsumer] E-mail '{Assunto}' processado para {Email}.", assunto, email);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync(cancellationToken);
            if (_connection != null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}
