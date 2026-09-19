namespace Indigo.Domain.Exceptions;

public sealed class ExcepcionNoEncontrado : DomainException
{
    public ExcepcionNoEncontrado(string message)
        : base(message)
    {
    }
}
