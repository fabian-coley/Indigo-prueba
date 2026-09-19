using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Indigo.Api.Swagger;

internal sealed class FiltroEsquemaEnums : ISchemaFilter
{
    public void Apply(OpenApiSchema esquema, SchemaFilterContext contexto)
    {
        var tipo = contexto.Type;
        var tipoDesenvuelto = Nullable.GetUnderlyingType(tipo) ?? tipo;

        if (!tipoDesenvuelto.IsEnum)
        {
            return;
        }

        esquema.Type = "string";
        esquema.Format = null;
        esquema.Enum = Enum.GetNames(tipoDesenvuelto)
            .Select(nombre => (IOpenApiAny)new OpenApiString(nombre))
            .ToList();
    }
}
