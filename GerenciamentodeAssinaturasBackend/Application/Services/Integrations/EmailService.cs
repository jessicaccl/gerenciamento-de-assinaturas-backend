using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using System.Net;
using System.Net.Mail;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Integrations
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarEmailAsync(string para, string assunto, string corpo)
        {
            var remetente = _configuration["Email:Remetente"];
            var senha = _configuration["Email:Senha"];

            // Sem credenciais configuradas (ex.: ambiente local), o e-mail é apenas registrado no log.
            if (string.IsNullOrWhiteSpace(remetente) || string.IsNullOrWhiteSpace(senha))
            {
                _logger.LogInformation("[Email] SMTP não configurado. E-mail para {Para} com assunto '{Assunto}' não foi enviado.", para, assunto);
                return;
            }

            var servidor = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            var porta = _configuration.GetValue("Email:Porta", 587);

            using var client = new SmtpClient(servidor, porta)
            {
                Credentials = new NetworkCredential(remetente, senha),
                EnableSsl = true
            };

            var mailMessage = new MailMessage(remetente, para, assunto, corpo)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
        }
    }
}
