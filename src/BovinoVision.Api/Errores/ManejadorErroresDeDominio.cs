using BovinoVision.Dominio.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BovinoVision.Api.Errores;

/// <summary>Translates domain errors into ProblemDetails responses (the "detail" field carries the message).</summary>
internal sealed class ManejadorErroresDeDominio(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken cancelacion)
    {
        if (excepcion is not ErrorDeDominio error)
        {
            return false;
        }

        var estado = error switch
        {
            NoEncontradoError => StatusCodes.Status404NotFound,
            ConflictoError => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status422UnprocessableEntity,
        };

        contexto.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails { Status = estado, Detail = error.Message },
        });
    }
}
