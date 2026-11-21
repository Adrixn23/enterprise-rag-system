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

    public static Error PasswordsDoNotMatch =>
        new("Account.PasswordsDoNotMatch", "La contraseña y la confirmación de la contraseña no coinciden.");

    public static Error EmailNotConfirmed =>
        new("Account.EmailNotConfirmed", "La cuenta no ha sido confirmada. Por favor verifica tu correo electrónico.");

    public static Error InvalidConfirmationToken =>
        new("Account.InvalidConfirmationToken", "El token de confirmación de correo es inválido o ha expirado.");

    public static Error RoleAssignmentFailed =>
        new("Account.RoleAssignmentFailed", "No se pudo asignar el rol inicial al usuario.");

    public static Error InconsistentUserState =>
        new("Account.InconsistentUserState", "Error crítico en el aprovisionamiento de usuario. La operación fue abortada por seguridad.");
}
