namespace EnterpriseRag.Core.Application.DTOs.Account;

public class ConfirmEmailRequestDto
{
    public Guid UserId { get; set; }
    public required string Token { get; set; }
}
