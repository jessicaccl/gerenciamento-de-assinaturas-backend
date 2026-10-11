using System.Threading.Tasks;

namespace GerenciamentodeAssinaturasBackend.Application.Interfaces 
{
    public interface IEmailService
    {
        Task EnviarEmailAsync(string para, string assunto, string corpo);
    }
}