using System.Threading.Tasks;

namespace AssinaMais.Api.Application.Interfaces
{
    public interface IRabbitMQService
    {
        Task EnviarMensagemAsync<T>(T mensagem, string nomeFila);
    }
}