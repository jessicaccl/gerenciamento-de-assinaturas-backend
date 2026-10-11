using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;

namespace GerenciamentodeAssinaturasBackend.BackgroundServices
{
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
                        var body = ea.Body.ToArray();
                        var mensagemJson = Encoding.UTF8.GetString(body);
                        Console.WriteLine($"[EmailConsumer] Mensagem recebida: {mensagemJson}");

                        using (var doc = JsonDocument.Parse(mensagemJson))
                        {
                            var root = doc.RootElement;
                            if (root.TryGetProperty("Tipo", out var tipoProp))
                            {
                                string? tipo = tipoProp.GetString();
                                string? emailDestino = root.GetProperty("EmailDestino").GetString();
                                string? nome = root.GetProperty("Nome").GetString();

                                if (tipo == "BOAS_VINDAS" && !string.IsNullOrEmpty(emailDestino) && !string.IsNullOrEmpty(nome))
                                {
                                    string caminhoArquivo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "BoasVindas.html");
                                    string corpoEmail;
                                    string assunto = "Bem-vindo(a) ao Assina+! 🎉";

                                    if (File.Exists(caminhoArquivo))
                                    {
                                        string templateHtml = await File.ReadAllTextAsync(caminhoArquivo);
                                        corpoEmail = templateHtml
                                            .Replace("{{NOME}}", nome)
                                            .Replace("{{URL_SISTEMA}}", _urlFrontend);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[ERRO] Template não encontrado em: {caminhoArquivo}");
                                        corpoEmail = $"<h1>Olá {nome}, seja bem-vindo!</h1><p>Cadastro realizado com sucesso.</p>";
                                    }

                                    await EnviarEmailComEscopo(emailDestino, assunto, corpoEmail);
                                }
                            }
                        }
                        await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[EmailConsumer] Erro: {ex.Message}");
                    }
                };

                await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailConsumer] Falha crítica: {ex.Message}");
            }
        }

        private async Task EnviarEmailComEscopo(string email, string assunto, string corpoHtml)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await emailService.EnviarEmailAsync(email, assunto, corpoHtml);
                Console.WriteLine($"[EmailConsumer] E-mail enviado para {email}");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_channel != null) await _channel.CloseAsync(cancellationToken);
            if (_connection != null) await _connection.CloseAsync(cancellationToken);
            await base.StopAsync(cancellationToken);
        }
    }
}