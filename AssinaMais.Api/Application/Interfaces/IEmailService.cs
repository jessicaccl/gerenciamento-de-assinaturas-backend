using System.Threading.Tasks;

namespace AssinaMais.Api.Application.Interfaces 
{
    public interface IEmailService
    {
        Task EnviarEmailAsync(string para, string assunto, string corpo);
    }
}