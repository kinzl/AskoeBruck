namespace TennisBruck.Extensions;

public class EnvironmentalVariables
{
    public static string RegistrationPurpose = "Registration";
    public static string PasswordResetPurpose = "PasswordReset";
    public static string PhoneRegex = "^[\\+]?[(]?[0-9]{3}[)]?[-\\s\\.]?[0-9]{3}[-\\s\\.]?[0-9]{4,}$";
    public static string PasswordRegex = @"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)[A-Za-z\d@$!%*?&]{8,}$";
    //This example requires:
    // At least one uppercase letter ((?=.*[A-Z]))
    // At least one lowercase letter ((?=.*[a-z]))
    // At least one digit ((?=.*\d))
    // Minimum 8 characters long ({8,}).
}