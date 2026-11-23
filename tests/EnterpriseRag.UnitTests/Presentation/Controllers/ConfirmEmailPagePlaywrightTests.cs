using System.Text.Json;
using EnterpriseRag.Core.Application.Contracts.Account;
using EnterpriseRag.Core.Application.DTOs.Account;
using EnterpriseRag.Core.Domain.Common;
using EnterpriseRag.Core.Domain.Common.Errors.Account;
using EnterpriseRag.WebApi.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace EnterpriseRag.UnitTests.Presentation.Controllers;

public class ConfirmEmailPagePlaywrightTests
{
    private static async Task<IBrowser> LaunchHeadlessBrowserAsync(IPlaywright playwright)
    {
        try
        {
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }
        catch (PlaywrightException)
        {
            // Fallback a Microsoft Edge instalado en Windows si los binarios de Playwright Chromium no están descargados
            return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",
                Headless = true
            });
        }
    }

    private static Dictionary<string, string> ExtractHeaders(IHeaderDictionary headers)
    {
        return headers.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmEmailPage_RealBrowserFlow_ShouldClearHash_PostJson_Handle400RetryAnd200_AndEnforceCsp()
    {
        // Arrange: Controlador real con servicio mockeado (sin infraestructura externa)
        var accountServiceMock = new Mock<IAccountService>();
        var userId = Guid.NewGuid();
        var token = "CfDJ8TestToken+With/Special=Chars";

        var callCount = 0;
        ConfirmEmailRequestDto? capturedPostBody = null;

        accountServiceMock
            .Setup(x => x.ConfirmEmailAsync(userId, token))
            .ReturnsAsync(() =>
            {
                callCount++;
                // 1er intento devuelve 400 (simula error transitorio/inválido), 2do intento (reintento) devuelve 200 OK
                return callCount == 1
                    ? Result.Failure(AccountErrors.InvalidConfirmationToken)
                    : Result.Success();
            });

        var controller = new AuthController(accountServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchHeadlessBrowserAsync(playwright);
        var page = await browser.NewPageAsync();

        var cspViolations = new List<string>();
        page.Console += (_, msg) =>
        {
            if (msg.Type == "error" && msg.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
            {
                cspViolations.Add(msg.Text);
            }
        };

        // Interceptar el origen aislado sirviendo el HTML y cabeceras reales de AuthController
        await page.RouteAsync("https://confirm-test.local/**", async route =>
        {
            var request = route.Request;
            var uri = new Uri(request.Url);

            if (request.Method == "GET" && uri.AbsolutePath == "/api/v1/auth/confirm-email")
            {
                var actionResult = controller.ConfirmEmailPage(userId) as ContentResult;
                actionResult.Should().NotBeNull();

                await route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 200,
                    ContentType = "text/html; charset=utf-8",
                    Headers = ExtractHeaders(controller.Response.Headers),
                    Body = actionResult!.Content
                });
                return;
            }

            if (request.Method == "POST" && uri.AbsolutePath == "/api/v1/auth/confirm-email")
            {
                var rawJson = request.PostData ?? "{}";
                capturedPostBody = JsonSerializer.Deserialize<ConfirmEmailRequestDto>(
                    rawJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var postResult = await controller.ConfirmEmail(capturedPostBody!);
                if (postResult is OkObjectResult ok)
                {
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 200,
                        ContentType = "application/json",
                        Body = JsonSerializer.Serialize(ok.Value)
                    });
                    return;
                }

                if (postResult is BadRequestObjectResult bad && bad.Value is ProblemDetails problem)
                {
                    // Inyectar etiqueta HTML en detail para verificar que el cliente renderiza como texto plano (sin innerHTML)
                    problem.Detail = "<img src=x onerror=alert(1)> El token expiró.";
                    await route.FulfillAsync(new RouteFulfillOptions
                    {
                        Status = 400,
                        ContentType = "application/problem+json",
                        Body = JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                    });
                    return;
                }
            }

            await route.AbortAsync();
        });

        // Act 1: Navegar con userId en query y token en fragmento #token=...
        var targetUrl = $"https://confirm-test.local/api/v1/auth/confirm-email?userId={userId}#token={Uri.EscapeDataString(token)}";
        await page.GotoAsync(targetUrl);

        // Assert 1 & 2 & 3: history.replaceState eliminó el fragmento ANTES de pulsar el botón
        page.Url.Should().NotContain("#token=");
        page.Url.Should().Be($"https://confirm-test.local/api/v1/auth/confirm-email?userId={userId}");
        var currentHash = await page.EvaluateAsync<string>("() => window.location.hash");
        currentHash.Should().BeEmpty();

        // Assert 8a: El script autorizado por nonce ejecutó sin violaciones CSP y el botón está habilitado
        cspViolations.Should().BeEmpty();
        (await page.Locator("#btnConfirm").IsEnabledAsync()).Should().BeTrue();

        // Assert 8b: Un script inline sin nonce debe ser bloqueado por la política CSP
        await page.EvaluateAsync(@"() => {
            const s = document.createElement('script');
            s.textContent = 'window.__unauthorizedCspRan = true;';
            document.body.appendChild(s);
        }");
        var bypassed = await page.EvaluateAsync<bool?>("() => window.__unauthorizedCspRan ?? null");
        bypassed.Should().BeNull("la CSP estricta con nonce debe bloquear scripts inline no autorizados");
        cspViolations.Should().NotBeEmpty();

        // Act 2: Primer clic -> POST devuelve 400 Bad Request
        await page.Locator("#btnConfirm").ClickAsync();
        await page.Locator("#result").WaitForAsync();

        // Assert 4 & 6 & 7: POST recibió userId y token; error se muestra como texto plano y permite reintentar
        capturedPostBody.Should().NotBeNull();
        capturedPostBody!.UserId.Should().Be(userId);
        capturedPostBody.Token.Should().Be(token);

        var errorText = await page.Locator("#result").TextContentAsync();
        errorText.Should().Contain("<img src=x onerror=alert(1)> El token expiró.");
        (await page.Locator("#result img").CountAsync()).Should().Be(0, "el mensaje de error debe renderizarse como texto plano, nunca como HTML");
        (await page.Locator("#btnConfirm").IsEnabledAsync()).Should().BeTrue();
        (await page.Locator("#btnConfirm").TextContentAsync()).Should().Be("Reintentar");

        // Act 3: Segundo clic (reintento) -> POST devuelve 200 OK
        await page.Locator("#btnConfirm").ClickAsync();
        await page.WaitForFunctionAsync("() => document.getElementById('btnConfirm').style.display === 'none'");

        // Assert 5: Mensaje de éxito y botón oculto
        var successText = await page.Locator("#result").TextContentAsync();
        successText.Should().Contain("Cuenta confirmada exitosamente");
    }

    [Fact]
    public async Task ConfirmEmailPage_WhenFragmentTokenIsMissing_ShouldDisableButtonAndShowErrorWithoutPost()
    {
        // Arrange
        var accountServiceMock = new Mock<IAccountService>();
        var userId = Guid.NewGuid();
        var controller = new AuthController(accountServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await LaunchHeadlessBrowserAsync(playwright);
        var page = await browser.NewPageAsync();

        await page.RouteAsync("https://confirm-test.local/**", async route =>
        {
            var actionResult = controller.ConfirmEmailPage(userId) as ContentResult;
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "text/html; charset=utf-8",
                Headers = ExtractHeaders(controller.Response.Headers),
                Body = actionResult!.Content
            });
        });

        // Act: Navegar sin fragmento #token=...
        await page.GotoAsync($"https://confirm-test.local/api/v1/auth/confirm-email?userId={userId}");

        // Assert: Botón deshabilitado y mensaje de enlace incompleto
        (await page.Locator("#btnConfirm").IsDisabledAsync()).Should().BeTrue();
        var text = await page.Locator("#result").TextContentAsync();
        text.Should().Contain("inválido o está incompleto");
        accountServiceMock.Verify(x => x.ConfirmEmailAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }
}
