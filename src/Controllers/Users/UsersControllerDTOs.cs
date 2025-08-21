using System.ComponentModel.DataAnnotations;
using GestionHogar.Model;

namespace GestionHogar.Controllers;

public class UserGetDTO
{
    public required User User { get; set; }
    public required IList<string> Roles { get; set; }
}

public class UserHigherRankDTO
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}

public class UserCreateDTO
{
    [MinLength(1)]
    [MaxLength(250)]
    public required string Name { get; set; }

    [EmailAddress]
    public required string Email { get; set; }

    [MinLength(1)]
    [MaxLength(250)]
    public required string Phone { get; set; }

    [MinLength(1)]
    public required string Role { get; set; }

    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public required string Password { get; set; }
}

public class UserUpdateDTO
{
    [MinLength(1)]
    [MaxLength(250)]
    public required string Name { get; set; }

    [EmailAddress]
    public required string Email { get; set; }

    [MinLength(1)]
    [MaxLength(250)]
    public required string Phone { get; set; }

    [MinLength(1)]
    public required string Role { get; set; }
}

public class UserUpdatePasswordDTO
{
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    public required string NewPassword { get; set; }
}

public class UpdateProfilePasswordDTO
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
