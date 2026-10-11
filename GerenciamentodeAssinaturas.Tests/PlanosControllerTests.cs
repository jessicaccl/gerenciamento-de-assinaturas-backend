using Moq;
using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using GerenciamentodeAssinaturasBackend.Controllers;
using GerenciamentodeAssinaturasBackend.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Domain.Entities;
using GerenciamentoAssinaturas.Domain.Interfaces;
using GerenciamentodeAssinaturasBackend.Application.Interfaces;

namespace GerenciamentodeAssinaturas.Tests
{
    public class PlanosControllerTests
    {
        private readonly Mock<IPlanoRepository> _planoRepoMock;
        private readonly Mock<IAssinaturaRepository> _assinaturaRepoMock;
        private readonly Mock<IUsuarioRepository> _usuarioRepoMock;
        private readonly Mock<IRabbitMQService> _rabbitMock;
        private readonly PlanosController _controller;

        public PlanosControllerTests()
        {
            _planoRepoMock = new Mock<IPlanoRepository>();
            _assinaturaRepoMock = new Mock<IAssinaturaRepository>();
            _usuarioRepoMock = new Mock<IUsuarioRepository>();
            _rabbitMock = new Mock<IRabbitMQService>();

            _controller = new PlanosController(
                _planoRepoMock.Object,
                _usuarioRepoMock.Object,
                _rabbitMock.Object,
                _assinaturaRepoMock.Object
            );

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim(ClaimTypes.Email, "admin@teste.com")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
        }

        [Fact]
        public async Task Delete_DeveRetornarConflict_QuandoPlanoPossuirAssinaturas()
        {
            int planoId = 1;
            var confirmacao = new ConfirmacaoDeleteDto { Senha = "senha_correta" };
            var usuarioMock = new Usuario { Email = "admin@teste.com", SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha_correta") };

            _usuarioRepoMock.Setup(repo => repo.GetUsuarioByEmailAsync(It.IsAny<string>()))
                            .ReturnsAsync(usuarioMock);

            _assinaturaRepoMock.Setup(repo => repo.ExisteAssinaturaParaPlanoAsync(planoId))
                               .ReturnsAsync(true);

            var result = await _controller.Delete(planoId, confirmacao);

            result.Should().BeOfType<ConflictObjectResult>();
            var conflictResult = result as ConflictObjectResult;
            conflictResult?.Value.Should().Be("Não é possível excluir este plano. Existem assinaturas vinculadas a ele no histórico do sistema.");

            _planoRepoMock.Verify(repo => repo.DeleteAsync(planoId), Times.Never);
        }

        [Fact]
        public async Task Delete_DeveRetornarNoContent_QuandoExclusaoForBemSucedida()
        {
            int planoId = 99;
            var confirmacao = new ConfirmacaoDeleteDto { Senha = "senha_correta" };
            var usuarioMock = new Usuario
            {
                Email = "admin@teste.com",
                SenhaHash = BCrypt.Net.BCrypt.HashPassword("senha_correta")
            };

            _usuarioRepoMock.Setup(repo => repo.GetUsuarioByEmailAsync(It.IsAny<string>()))
                            .ReturnsAsync(usuarioMock);

            _assinaturaRepoMock.Setup(repo => repo.ExisteAssinaturaParaPlanoAsync(planoId))
                               .ReturnsAsync(false);

            var result = await _controller.Delete(planoId, confirmacao);

            result.Should().BeOfType<NoContentResult>();

            _planoRepoMock.Verify(repo => repo.DeleteAsync(planoId), Times.Once);

            _rabbitMock.Verify(m => m.EnviarMensagemAsync(
                It.IsAny<object>(),
                "planos_eventos"
            ), Times.Once);
        }
    }
}