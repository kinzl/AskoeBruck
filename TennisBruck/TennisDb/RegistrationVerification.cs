namespace TennisDb;

public class RegistrationVerification
{
    public int Id { get; set; }
    public string Firstname { get; set; }
    public string Lastname { get; set; }
    public string EmailOrPhone { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string VerificationCode { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsVerified { get; set; }
}