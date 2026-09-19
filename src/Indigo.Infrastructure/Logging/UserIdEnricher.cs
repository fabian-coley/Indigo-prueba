using Serilog.Core;
using Serilog.Events;

namespace Indigo.Infrastructure.Logging;

public sealed class UserIdEnricher : ILogEventEnricher
{
    public const string NombrePropiedad = "UserId";

    private readonly Func<string?> _usuarioId;

    public UserIdEnricher(Func<string?> usuarioId)
    {
        ArgumentNullException.ThrowIfNull(usuarioId);
        _usuarioId = usuarioId;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var usuarioId = _usuarioId();
        if (string.IsNullOrEmpty(usuarioId))
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(NombrePropiedad, usuarioId));
    }
}
