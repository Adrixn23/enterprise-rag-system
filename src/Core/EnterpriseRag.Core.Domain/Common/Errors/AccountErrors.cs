using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseRag.Core.Domain.Common.Errors;

public static class AccountErrors
{
    public static Error EmailAlreadyExists =>
        new("Account.EmailAlreadyExists", "An account with this email already exists.");

    public static Error InvalidCredentials =>
        new("Account.InvalidCredentials", "Invalid email or password.");

    public static Error UserNotFound =>
        new("Account.UserNotFound", "User account was not found.");

    public static Error RegistrationFailed(string description) =>
        new("Account.RegistrationFailed", description);
}

