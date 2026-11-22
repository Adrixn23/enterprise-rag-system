namespace EnterpriseRag.WebApi.Controllers;

using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.WebApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[AllowAnonymous]
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private static readonly JsonSerializerOptions SafeJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
        return result.ToActionResult();
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-register")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.RegisterAsync(request);
        return result.ToActionResult(userId => Ok(new { UserId = userId }));
    }

    [HttpGet("confirm-email")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public IActionResult ConfirmEmailPage([FromQuery] Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Account.InvalidConfirmationRequest",
                Detail = "Los parámetros de confirmación son inválidos o están incompletos."
            });
        }

        // Nonce criptográfico único por respuesta para CSP estricta sin 'unsafe-inline'
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

        // El token nunca se recibe ni se refleja desde el servidor; solo se serializa userId de forma segura.
        var serializedPayload = JsonSerializer.Serialize(new { userId }, SafeJsonOptions);

        // El endpoint GET no muta el estado (idempotente/safe según HTTP RFC 7231).
        var html = $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='utf-8'/>
    <title>Confirmación de Correo - Enterprise RAG</title>
    <style nonce='{nonce}'>
        body {{ font-family: sans-serif; background: #07090e; color: #e2e8f0; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }}
        .card {{ background: #0f131c; border: 1px solid #1e293b; padding: 2rem; border-radius: 1rem; max-width: 420px; text-align: center; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }}
        h1 {{ font-size: 1.25rem; color: #fff; margin-bottom: 0.5rem; }}
        p {{ font-size: 0.875rem; color: #94a3b8; line-height: 1.5; }}
        button {{ background: #4f46e5; color: white; border: none; padding: 0.75rem 1.5rem; border-radius: 0.5rem; font-weight: 600; cursor: pointer; margin-top: 1rem; width: 100%; }}
        button:hover {{ background: #4338ca; }}
        button:disabled {{ opacity: 0.5; cursor: not-allowed; }}
        #result {{ margin-top: 1rem; font-size: 0.875rem; }}
    </style>
</head>
<body>
    <div class='card'>
        <h1>Confirmar Cuenta</h1>
        <p>Haz clic en el botón inferior para completar la verificación segura de tu correo electrónico corporativo.</p>
        <button id='btnConfirm' type='button'>Confirmar mi Cuenta</button>
        <div id='result'></div>
    </div>
    <script id='confirm-payload' type='application/json'>{serializedPayload}</script>
    <script nonce='{nonce}'>
        (function () {{
            const btn = document.getElementById('btnConfirm');
            const resDiv = document.getElementById('result');
            const payloadElement = document.getElementById('confirm-payload');
            const payload = JSON.parse(payloadElement ? payloadElement.textContent : '{{}}');

            const rawHash = window.location.hash && window.location.hash.startsWith('#')
                ? window.location.hash.slice(1)
                : '';
            const hashParams = new URLSearchParams(rawHash);
            const token = hashParams.get('token');

            // Eliminar inmediatamente el fragmento de la URL y del historial antes de cualquier petición
            if (window.location.hash && window.history && typeof window.history.replaceState === 'function') {{
                window.history.replaceState(null, '', window.location.pathname + window.location.search);
            }}

            if (!token || !token.trim()) {{
                btn.disabled = true;
                resDiv.style.color = '#f87171';
                resDiv.textContent = 'El enlace de confirmación es inválido o está incompleto.';
                return;
            }}

            btn.addEventListener('click', async function () {{
                btn.disabled = true;
                btn.textContent = 'Verificando...';
                resDiv.textContent = '';

                try {{
                    const res = await fetch('/api/v1/auth/confirm-email', {{
                        method: 'POST',
                        headers: {{ 'Content-Type': 'application/json' }},
                        body: JSON.stringify({{ userId: payload.userId, token: token }})
                    }});
                    if (res.ok) {{
                        resDiv.style.color = '#34d399';
                        resDiv.textContent = '¡Cuenta confirmada exitosamente! Ya puedes iniciar sesión en la aplicación.';
                        btn.style.display = 'none';
                    }} else {{
                        const err = await res.json().catch(function () {{ return {{}}; }});
                        resDiv.style.color = '#f87171';
                        resDiv.textContent = err.detail || 'El enlace es inválido o ha expirado.';
                        btn.disabled = false;
                        btn.textContent = 'Reintentar';
                    }}
                }} catch (e) {{
                    resDiv.style.color = '#f87171';
                    resDiv.textContent = 'Error de conexión al confirmar.';
                    btn.disabled = false;
                    btn.textContent = 'Reintentar';
                }}
            }});
        }})();
    </script>
</body>
</html>";
        return Content(html, "text/html");
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth-confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequestDto request)
    {
        var result = await _accountService.ConfirmEmailAsync(request.UserId, request.Token);
        return result.ToActionResult(() => Ok(new { Message = "Correo electrónico confirmado exitosamente." }));
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth-resend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationEmailRequestDto request)
    {
        var result = await _accountService.ResendConfirmationEmailAsync(request.Email);
        return result.ToActionResult(() => Ok(new { Message = "Si la cuenta existe y no está confirmada, se ha enviado un nuevo enlace de confirmación." }));
    }
}
