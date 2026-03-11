namespace TennisBruck.wwwroot.Dto;

public class RegistrationDto
{
    public required string Firstname { get; set; }
    public required string Lastname { get; set; }
    public required string Username { get; set; }
    public required string EmailOrPhone { get; set; }
    public required string Password { get; set; }
}