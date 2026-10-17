using GerenciamentodeAssinaturasBackend.Application.DTOs;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Services;

namespace GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas
{
    public enum ResultadoDoLembrete { Enviado, JaEnviado, SemEmail, CobrancaFechada }

    // Decide quem recebe lembrete e publica o pedido na fila de e-mails (o EmailConsumer faz o envio)
    public class ServicoDeLembretes
    {
        public const string FilaDeEmails = "emails_queue";

        private readonly ICobrancaRepository _cobrancaRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly ILogger<ServicoDeLembretes> _logger;
        private readonly int _diasDeAntecedencia;

        public ServicoDeLembretes(
            ICobrancaRepository cobrancaRepository,
            IRabbitMQService rabbitMQService,
            ILogger<ServicoDeLembretes> logger,
            IConfiguration configuration)
        {
            _cobrancaRepository = cobrancaRepository;
            _rabbitMQService = rabbitMQService;
            _logger = logger;
            _diasDeAntecedencia = configuration.GetValue("Lembretes:DiasAntesDoVencimento", 3);
        }

        // Chamado pelo CobrancaWorker: avisa quem vence nos próximos dias e quem já está em atraso
        public async Task<int> EnviarAutomaticosAsync(DateTime hoje)
        {
            var enviados = 0;
            var emAberto = await _cobrancaRepository.ListarAsync(new FiltroCobrancasDto(), hoje);

            foreach (var cobranca in emAberto.Where(c => !string.IsNullOrWhiteSpace(c.ClienteEmail)))
            {
                var tipo = RegrasDeLembrete.LembreteAutomatico(cobranca.Status, cobranca.DataVencimento, hoje, _diasDeAntecedencia);
                if (tipo != null && await EnviarAsync(cobranca, tipo, hoje) == ResultadoDoLembrete.Enviado)
                    enviados++;
            }

            if (enviados > 0)
                _logger.LogInformation("[Lembretes] {Quantidade} lembrete(s) automático(s) enviado(s).", enviados);

            return enviados;
        }

        public async Task<ResultadoDoLembrete> EnviarManualAsync(CobrancaResponseDto cobranca, DateTime hoje) =>
            await EnviarAsync(cobranca, TipoDeLembrete.Manual, hoje);

        // "Notificar vencidas": um lembrete manual para cada cobrança vencida que tenha e-mail
        public async Task<(int Enviados, int SemEmail)> NotificarVencidasAsync(DateTime hoje)
        {
            var vencidas = await _cobrancaRepository.ListarAsync(new FiltroCobrancasDto { Status = StatusCobranca.Vencida }, hoje);
            int enviados = 0, semEmail = 0;

            foreach (var cobranca in vencidas)
            {
                var resultado = await EnviarAsync(cobranca, TipoDeLembrete.Manual, hoje);
                if (resultado == ResultadoDoLembrete.Enviado) enviados++;
                if (resultado == ResultadoDoLembrete.SemEmail) semEmail++;
            }

            return (enviados, semEmail);
        }

        private async Task<ResultadoDoLembrete> EnviarAsync(CobrancaResponseDto cobranca, string tipo, DateTime hoje)
        {
            if (cobranca.Status != StatusCobranca.Pendente && cobranca.Status != StatusCobranca.Vencida)
                return ResultadoDoLembrete.CobrancaFechada;

            if (string.IsNullOrWhiteSpace(cobranca.ClienteEmail))
                return ResultadoDoLembrete.SemEmail;

            // Registra antes de publicar: se outra execução já registrou este lembrete automático, não envia de novo
            if (!await _cobrancaRepository.RegistrarLembreteAsync(cobranca.Id, tipo))
                return ResultadoDoLembrete.JaEnviado;

            var (assunto, titulo, mensagem) = RegrasDeLembrete.Texto(tipo, cobranca.DiasEmAtraso, cobranca.DataVencimento, hoje);

            await _rabbitMQService.EnviarMensagemAsync(new
            {
                Tipo = "LEMBRETE_COBRANCA",
                EmailDestino = cobranca.ClienteEmail,
                Nome = cobranca.ClienteNome,
                Assunto = assunto,
                Titulo = titulo,
                Mensagem = mensagem,
                Plano = cobranca.PlanoNome,
                Valor = cobranca.Valor.ToString("C", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")),
                Vencimento = cobranca.DataVencimento.ToString("dd/MM/yyyy"),
                CobrancaId = cobranca.Id,
                TipoLembrete = tipo
            }, FilaDeEmails);

            return ResultadoDoLembrete.Enviado;
        }
    }
}
