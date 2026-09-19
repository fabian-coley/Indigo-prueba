using FluentValidation;
using Indigo.Api.Validation;
using Indigo.Application.Contracts.Auth;
using Indigo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Indigo.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly IValidator<RegistroRequest> _validadorDeRegistro;
    private readonly IValidator<LoginRequest> _validadorDeLogin;
    private readonly ILogger<AuthController> _log;

    public AuthController(
        AuthService auth,
        IValidator<RegistroRequest> validadorDeRegistro,
        IValidator<LoginRequest> validadorDeLogin,
        ILogger<AuthController> log)
    {
        _auth = auth;
        _validadorDeRegistro = validadorDeRegistro;
        _validadorDeLogin = validadorDeLogin;
        _log = log;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Registrar([FromBody] RegistroRequest peticion, CancellationToken ct)
    {
        var problema = await ValidacionDeRequest.ValidarAsync(_validadorDeRegistro, peticion, ct);
        if (problema is not null)
        {
            return BadRequest(problema);
        }

        var resultado = await _auth.RegistrarAsync(peticion, ct);

        if (!resultado.Exitoso)
        {
            return BadRequest(ValidacionDeRequest.ApartirDe(resultado.Errores));
        }

        _log.LogInformation("Usuario registrado {Email}", peticion.Email);
        return Ok();
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequest peticion, CancellationToken ct)
    {
        var problema = await ValidacionDeRequest.ValidarAsync(_validadorDeLogin, peticion, ct);
        if (problema is not null)
        {
            return BadRequest(problema);
        }

        var respuesta = await _auth.LoginAsync(peticion, ct);

        if (respuesta is null)
        {
            _log.LogWarning("Login fallido para {Email}", peticion.Email);

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Credenciales inválidas",
                Detail = "El email o la contraseña no son correctos.",
            });
        }

        _log.LogInformation("Login exitoso para {Email}", peticion.Email);
        return Ok(respuesta);
    }
}
