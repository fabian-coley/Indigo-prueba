namespace Indigo.Application.Common;

public sealed class ResultadoRegistro
{
    private static readonly IReadOnlyList<ErrorDeRegistro> SinErrores = [];

    private ResultadoRegistro(bool exitoso, UsuarioAutenticado? usuario, IReadOnlyList<ErrorDeRegistro> errores)
    {
        Exitoso = exitoso;
        Usuario = usuario;
        Errores = errores;
    }

    public bool Exitoso { get; }

    public UsuarioAutenticado? Usuario { get; }

    public IReadOnlyList<ErrorDeRegistro> Errores { get; }

    public static ResultadoRegistro Correcto(UsuarioAutenticado usuario) => new(true, usuario, SinErrores);

    public static ResultadoRegistro Fallido(IEnumerable<ErrorDeRegistro> errores) => new(false, null, [.. errores]);
}
