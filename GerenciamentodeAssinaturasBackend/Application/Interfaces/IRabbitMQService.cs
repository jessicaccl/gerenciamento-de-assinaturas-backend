using System.Threading.Tasks;

namespace GerenciamentodeAssinaturasBackend.Application.Interfaces
{
    public interface IRabbitMQService
    {
        Task EnviarMensagemAsync<T>(T mensagem, string nomeFila);
    }
}