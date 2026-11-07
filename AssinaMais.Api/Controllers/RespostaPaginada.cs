using AssinaMais.Api.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AssinaMais.Api.Controllers
{
    public static class RespostaPaginada
    {
        public const string CabecalhoTotal = "X-Total-Count";

        // O corpo continua sendo a lista; o total de registros vai no cabeçalho (padrão usado por várias APIs REST)
        public static IActionResult OkPaginado<T>(this ControllerBase controller, PaginaDe<T> pagina)
        {
            controller.Response.Headers[CabecalhoTotal] = pagina.Total.ToString();
            return controller.Ok(pagina.Itens);
        }
    }
}
