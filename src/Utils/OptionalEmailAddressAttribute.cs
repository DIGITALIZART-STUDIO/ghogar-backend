using System.ComponentModel.DataAnnotations;

namespace GestionHogar.Utils;

/// <summary>
/// Valida formato de correo, pero permite null o vacío.
/// Necesario en update: "" significa borrar el campo y [EmailAddress] de .NET rechaza "".
/// </summary>
public sealed class OptionalEmailAddressAttribute : ValidationAttribute
{
    private static readonly EmailAddressAttribute EmailAddress = new();

    public OptionalEmailAddressAttribute()
        : base(() => "El correo electrónico no es válido") { }

    public override bool IsValid(object? value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is not string email)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        return EmailAddress.IsValid(email);
    }
}
