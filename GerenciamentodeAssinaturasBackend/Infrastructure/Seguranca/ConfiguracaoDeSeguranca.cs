using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace GerenciamentodeAssinaturasBackend.Infrastructure.Seguranca
{
    // Limite de acesso (rate limiting) e cabeçalhos de segurança HTTP
    public static class ConfiguracaoDeSeguranca
    {
        // Rotas que recebem senha (login, cadastro, confirmação de senha): o limite baixo dificulta tentar senhas em massa
        public const string PoliticaDeSenha = "tentativas-de-senha";

        public static IServiceCollection AddSegurancaDaApi(this IServiceCollection services, IConfiguration configuration)
        {
            var tentativasDeSenha = configuration.GetValue("Seguranca:TentativasDeSenhaPorMinuto", 5);
            var limiteGeral = configuration.GetValue("Seguranca:RequisicoesPorMinuto", 120);

            services.AddRateLimiter(opcoes =>
            {
                opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                opcoes.OnRejected = async (contexto, cancelamento) =>
                {
                    contexto.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                    if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var esperar))
                        contexto.HttpContext.Response.Headers.RetryAfter = ((int)esperar.TotalSeconds).ToString();

                    await contexto.HttpContext.Response.WriteAsync("Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.", cancelamento);
                };

                // Por IP: 5 tentativas de senha por minuto
                opcoes.AddPolicy(PoliticaDeSenha, contexto => RateLimitPartition.GetFixedWindowLimiter(
                    IpDoCliente(contexto),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = tentativasDeSenha, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

                // Geral: por usuário logado (ou por IP, para quem não está logado), em todas as rotas
                opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto => RateLimitPartition.GetFixedWindowLimiter(
                    contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value is { } usuario ? $"usuario:{usuario}" : $"ip:{IpDoCliente(contexto)}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteGeral, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            // Atrás de um proxy (Render, Nginx), o IP real do visitante chega no cabeçalho X-Forwarded-For.
            // Só é confiável quando existe proxy na frente; sem ele, qualquer um poderia inventar o IP e driblar o limite.
            if (configuration.GetValue("Seguranca:AtrasDeProxy", false))
            {
                services.Configure<ForwardedHeadersOptions>(opcoes =>
                {
                    opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    opcoes.ForwardLimit = 1;   // usa só o IP acrescentado pelo proxy, o último da lista
                    opcoes.KnownNetworks.Clear();
                    opcoes.KnownProxies.Clear();
                });
            }

            return services;
        }

        public static WebApplication UseSegurancaDaApi(this WebApplication app)
        {
            if (app.Configuration.GetValue("Seguranca:AtrasDeProxy", false))
                app.UseForwardedHeaders();

            if (!app.Environment.IsDevelopment())
                app.UseHsts();

            app.Use(async (contexto, proximo) =>
            {
                var cabecalhos = contexto.Response.Headers;
                cabecalhos["X-Content-Type-Options"] = "nosniff";       // o navegador não "adivinha" o tipo do conteúdo
                cabecalhos["X-Frame-Options"] = "DENY";                 // a API não pode ser embutida em iframes (clickjacking)
                cabecalhos["Referrer-Policy"] = "no-referrer";
                cabecalhos["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                await proximo();
            });

            return app;
        }

        private static string IpDoCliente(HttpContext contexto) =>
            contexto.Connection.RemoteIpAddress?.ToString() ?? IPAddress.None.ToString();
    }
}
