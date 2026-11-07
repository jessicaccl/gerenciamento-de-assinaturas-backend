using AssinaMais.Api.Domain.Interfaces;
using AssinaMais.Api.Application.DTOs;
using AssinaMais.Api.Domain.Services;
using AssinaMais.Api.Application.Interfaces;
using AssinaMais.Api.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AssinaMais.Api.Infrastructure.Seguranca;
using System.Security.Claims;

namespace AssinaMais.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PlanosController : ControllerBase
    {
        private readonly IPlanoRepository _planoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        private readonly IRabbitMQService _rabbitMQService;
        private readonly IAssinaturaRepository _assinaturaRepository;

        public PlanosController(
            IPlanoRepository planoRepository,
            IUsuarioRepository usuarioRepository,
            IRabbitMQService rabbitMQService,
            IAssinaturaRepository assinaturaRepository)
        {
            _planoRepository = planoRepository;
            _usuarioRepository = usuarioRepository;
            _rabbitMQService = rabbitMQService;
            _assinaturaRepository = assinaturaRepository;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var planos = await _planoRepository.GetAllAsync();
            return Ok(planos);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var plano = await _planoRepository.GetByIdAsync(id);
            if (plano == null) return NotFound();

            return Ok(plano);
        }

        // Preço do plano em cada ciclo de cobrança, já com os descontos aplicados
        [HttpGet("{id}/ciclos")]
        public async Task<IActionResult> GetCiclos(int id)
        {
            var plano = await _planoRepository.GetByIdAsync(id);
            if (plano == null) return NotFound();

            var ciclos = RegrasDeCobranca.Recorrencias.Select(r => new CicloDePrecoDto
            {
                Recorrencia = r,
                Meses = RegrasDeCobranca.MesesDoCiclo(r),
                MesesCobrados = RegrasDeCobranca.MesesCobrados(r),
                Valor = RegrasDeCobranca.CalcularValor(plano.Valor, r),
                ValorMensalEquivalente = RegrasDeCobranca.ValorMensalEquivalente(plano.Valor, r),
                PercentualDesconto = RegrasDeCobranca.PercentualDeDesconto(r)
            });

            return Ok(ciclos);
        }

        [HttpPost]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Create([FromBody] SalvarPlanoDto dto)
        {
            var plano = new Plano { Nome = dto.Nome.Trim(), Valor = dto.Valor, Descricao = dto.Descricao.Trim(), Ativo = dto.Ativo };

            var novoId = await _planoRepository.CreateAsync(plano);
            plano.Id = novoId;

            var evento = new { Operacao = "POST", Dados = plano, Autor = ObterEmailUsuario() };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return Ok(new { id = novoId });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        public async Task<IActionResult> Update(int id, [FromBody] SalvarPlanoDto dto)
        {
            var plano = await _planoRepository.GetByIdAsync(id);
            if (plano == null) return NotFound("Plano não encontrado.");

            plano.Nome = dto.Nome.Trim();
            plano.Valor = dto.Valor;
            plano.Descricao = dto.Descricao.Trim();
            plano.Ativo = dto.Ativo;

            await _planoRepository.UpdateAsync(plano);

            var evento = new { Operacao = "PUT", Dados = plano, Autor = ObterEmailUsuario() };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = Cargos.Admin)]
        [EnableRateLimiting(ConfiguracaoDeSeguranca.PoliticaDeSenha)]   // pede a senha do Admin
        public async Task<IActionResult> Delete(int id, [FromBody] ConfirmacaoDeleteDto confirmacao)
        {
            var emailLogado = ObterEmailUsuario();

            var usuario = await _usuarioRepository.GetUsuarioByEmailAsync(emailLogado);
            if (usuario == null) return Unauthorized("Usuário não encontrado.");

            if (!BCrypt.Net.BCrypt.Verify(confirmacao.Senha, usuario.SenhaHash))
                return BadRequest("Senha de administrador incorreta.");

            var possuiDependencias = await _assinaturaRepository.ExisteAssinaturaParaPlanoAsync(id);

            if (possuiDependencias)
            {
                return Conflict("Não é possível excluir este plano. Existem assinaturas vinculadas a ele no histórico do sistema.");
            }

            await _planoRepository.DeleteAsync(id);

            var evento = new { Operacao = "DELETE", PlanoId = id, Autor = emailLogado };
            await _rabbitMQService.EnviarMensagemAsync(evento, "planos_eventos");

            return NoContent();
        }

        private string ObterEmailUsuario()
        {
            return User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? "Desconhecido";
        }
    }

    public class ConfirmacaoDeleteDto
    {
        public string? Senha { get; set; }
    }
}