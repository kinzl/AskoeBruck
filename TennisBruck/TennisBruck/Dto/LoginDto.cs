namespace TennisBruck.wwwroot.Dto;

public abstract class LoginDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}