using Indigo.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Errors;

internal sealed class ManejadorDeExcepciones : IExceptionHandler
{
    private readonly IProblemDetailsService _problemas;
    private readonly ILogger<ManejadorDeExcepciones> _log;

    public ManejadorDeExcepciones(IProblemDetailsService problemas, ILogger<ManejadorDeExcepciones> log)
    {
        _problemas = problemas;
        _log = log;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var (estado, titulo, detalle) = excepcion switch
        {
            ExcepcionNoEncontrado => (StatusCodes.Status404NotFound, "Recurso no encontrado", excepcion.Message),
            ExcepcionStockInsuficiente => (StatusCodes.Status400BadRequest, "Stock insuficiente", excepcion.Message),
            ExcepcionReglaDeNegocio => (StatusCodes.Status400BadRequest, "Solicitud inválida", excepcion.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Error interno del servidor",
                "Ocurrió un error inesperado. Revisá los logs del servidor."),
        };

        if (estado == StatusCodes.Status500InternalServerError)
        {
            _log.LogError(excepcion, "Excepción no controlada en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
        }
        else
        {
            _log.LogWarning(excepcion, "Excepción de dominio en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
        }

        contexto.Response.StatusCode = estado;

        return await _problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
            },
        });
    }
}
