using FluentValidation;

namespace NetWatch.Application.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        // Only presence is checked here. Complexity rules belong to Identity, and echoing
        // them back on a *login* attempt would tell an attacker the shape of valid
        // passwords for free.
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Role).NotEmpty();
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public class RevokeRequestValidator : AbstractValidator<RevokeRequest>
{
    public RevokeRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}
