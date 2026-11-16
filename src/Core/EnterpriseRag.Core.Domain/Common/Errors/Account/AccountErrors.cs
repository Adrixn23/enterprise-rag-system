namespace EnterpriseRag.Core.Domain.Common.Errors.Account;

public static class AccountErrors
{
    public static Error EmailAlreadyExists =>
        new("Account.EmailAlreadyExists", "Ya existe una cuenta registrada con este correo electrónico.");

    public static Error InvalidCredentials =>
        new("Account.InvalidCredentials", "Correo electrónico o contraseña incorrectos.");

    public static Error UserNotFound =>
        new("Account.UserNotFound", "La cuenta de usuario solicitada no fue encontrada.");

    public static Error AccountLocked =>
        new("Account.AccountLocked", "La cuenta ha sido bloqueada temporalmente debido a múltiples intentos fallidos de inicio de sesión. Intente más tarde.");

    public static Error RegistrationFailed(string description) =>
        new("Account.RegistrationFailed", description);
}
