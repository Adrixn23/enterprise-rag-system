namespace EnterpriseRag.Core.Application.DTOs.Account;

public class AuthenticationResponseDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
}
