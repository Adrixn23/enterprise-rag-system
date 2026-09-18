namespace EnterpriseRag.Core.Application.DTOs.Account;

public class RegisterRequestDto
{
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
    public required Guid TenantId { get; set; }
}
