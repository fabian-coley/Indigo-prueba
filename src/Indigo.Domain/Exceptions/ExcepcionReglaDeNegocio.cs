namespace Indigo.Domain.Exceptions;

public sealed class ExcepcionReglaDeNegocio : DomainException
{
    public ExcepcionReglaDeNegocio(string message) : base(message)
    {
    }
}
