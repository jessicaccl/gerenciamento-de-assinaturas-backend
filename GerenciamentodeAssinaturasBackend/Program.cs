using GerenciamentoAssinaturas.Domain.Interfaces;
using GerenciamentoAssinaturas.Infrastructure.Repositories;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.Services.Auth;
using GerenciamentodeAssinaturasBackend.Application.Services.Cobrancas;
using GerenciamentodeAssinaturasBackend.Application.Services.Integrations;
using GerenciamentodeAssinaturasBackend.BackgroundServices;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Infrastructure.Data;
using GerenciamentodeAssinaturasBackend.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var origensPermitidas = builder.Configuration.GetSection("Cors:Origens").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend",
        policy =>
        {
            policy.WithOrigins(origensPermitidas)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Assina+ API",
        Version = "v1",
        Description = "API de gestão de planos e assinaturas. Faça login em /api/auth/login com demo@assinamais.dev / demo123 e use o token no botão Authorize."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT assim: Bearer seu_token_aqui"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<IPlanoRepository, PlanoRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IAssinaturaRepository, AssinaturaRepository>();
builder.Services.AddScoped<ICobrancaRepository, CobrancaRepository>();
builder.Services.AddScoped<GeradorDeCobrancas>();
builder.Services.AddHostedService<CobrancaWorker>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();

if (builder.Configuration.GetValue("RabbitMQ:Habilitado", true))
{
    builder.Services.AddScoped<IRabbitMQService, RabbitMQService>();
    builder.Services.AddHostedService<AssinaturaWorker>();
    builder.Services.AddHostedService<EmailConsumer>();
}
else
{
    builder.Services.AddScoped<IRabbitMQService, RabbitMQDesabilitadoService>();
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Configure 'Jwt:Key' com pelo menos 32 caracteres (appsettings.Development.json, User Secrets ou variável de ambiente Jwt__Key).");

var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

var app = builder.Build();

// Erros não tratados viram uma resposta 500 genérica, sem expor detalhes internos ao cliente
app.UseExceptionHandler();

// Swagger fica disponível também em produção: é a vitrine da API para quem avalia o projeto
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Assina+ API v1"));

app.UseCors("Frontend");

app.UseAuthentication(); 
app.UseAuthorization();  

app.MapControllers();

app.Run();

public partial class Program { }