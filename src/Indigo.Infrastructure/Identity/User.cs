using Microsoft.AspNetCore.Identity;

namespace Indigo.Infrastructure.Identity;

public class User : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
}
