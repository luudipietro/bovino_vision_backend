using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BovinoVision.Api.Errores;

/// <summary>Traduce las fallas de enlace de la solicitud a ProblemDetails con su código de estado (por lo general 400).</summary>
internal sealed class ManejadorSolicitudesInvalidas(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancelacion)
    {
        if (excepcion is not BadHttpRequestException error)
        {
            return false;
        }

        contexto.Response.StatusCode = error.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails { Status = error.StatusCode, Detail = error.Message },
        });
    }
}
