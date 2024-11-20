using System.ComponentModel.DataAnnotations;

namespace TennisBruck.Dto;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Neues Passwort ist erforderlich.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; }

    [Required(ErrorMessage = "Passwort-Bestätigung ist erforderlich.")]
    [Compare("NewPassword", ErrorMessage = "Die Passwörter stimmen nicht überein.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; }
}