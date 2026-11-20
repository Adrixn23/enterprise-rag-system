namespace EnterpriseRag.WebApi.Controllers;

using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.DTOs.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[AllowAnonymous]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AuthController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthenticationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.AuthenticationAsync(request);

        if (result.IsFailure)
        {
            var firstError = result.Errors.FirstOrDefault();
            var errorCode = firstError?.Code ?? "Account.LoginFailed";
            var description = firstError?.Description ?? "Error al autenticar el usuario.";

            if (errorCode == "Account.AccountLocked")
            {
                return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
                {
                    Status = StatusCodes.Status423Locked,
                    Title = errorCode,
                    Detail = description
                });
            }

            if (errorCode == "Account.InvalidCredentials" || errorCode == "Account.UserNotFound")
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = errorCode,
                    Detail = description
                });
            }

            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = errorCode,
                Detail = description
            });
        }

        return Ok(result.Value);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.RegisterAsync(request);

        if (result.IsFailure)
        {
            var firstError = result.Errors.FirstOrDefault();
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = firstError?.Code ?? "Account.RegistrationFailed",
                Detail = firstError?.Description ?? "Error durante el registro de usuario."
            });
        }

        return Ok(new { UserId = result.Value });
    }
}
