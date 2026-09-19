using System.Text.Json;
using FluentValidation;
using Indigo.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Validation;

internal static class ValidacionDeRequest
{
    private static readonly JsonNamingPolicy PoliticaDeNombres = JsonNamingPolicy.CamelCase;

    public static async Task<ValidationProblemDetails?> ValidarAsync<T>(
        IValidator<T> validador,
        T request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(validador);

        var resultado = await validador.ValidateAsync(request, ct);

        if (resultado.IsValid)
        {
            return null;
        }

        return Crear(resultado.Errors.Select(error => (error.PropertyName, error.ErrorMessage)));
    }

    public static ValidationProblemDetails Problema(string campo, string mensaje) =>
        Crear([(campo, mensaje)]);

    public static ValidationProblemDetails ApartirDe(IEnumerable<ErrorDeRegistro> errores)
    {
        ArgumentNullException.ThrowIfNull(errores);

        return Crear(errores.Select(error => (error.Campo, error.Mensaje)));
    }

    private static ValidationProblemDetails Crear(IEnumerable<(string Campo, string Mensaje)> errores)
    {
        var porCampo = errores
            .GroupBy(error => PoliticaDeNombres.ConvertName(error.Campo))
            .ToDictionary(
                grupo => grupo.Key,
                grupo => grupo.Select(error => error.Mensaje).ToArray());

        return new ValidationProblemDetails(porCampo)
        {
            Status = StatusCodes.Status400BadRequest,
        };
    }
}
