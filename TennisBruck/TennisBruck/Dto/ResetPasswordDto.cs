using System.ComponentModel.DataAnnotations;

namespace TennisBruck.Dto;

public class ResetPasswordDto
{
    public ResetPasswordDto(string newPassword, string confirmPassword)
    {
        NewPassword = newPassword;
        ConfirmPassword = confirmPassword;
    }

    [Required(ErrorMessage = "Neues Passwort ist erforderlich.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; }

    [Required(ErrorMessage = "Passwort-Bestätigung ist erforderlich.")]
    [Compare("NewPassword", ErrorMessage = "Die Passwörter stimmen nicht überein.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; }
}