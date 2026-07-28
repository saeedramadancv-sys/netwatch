using FluentValidation;

namespace NetWatch.Application.Devices;

/// <summary>
/// Request-shape validation, run before anything reaches the domain.
///
/// This is not a second copy of the domain's rules: the entity still enforces its own
/// invariants for any caller. Validators exist so the API can answer with a field-level
/// 400 listing every problem at once, instead of an exception describing the first one.
/// </summary>
public class CreateDeviceRequestValidator : AbstractValidator<CreateDeviceRequest>
{
    public CreateDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(253);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Site).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class UpdateDeviceRequestValidator : AbstractValidator<UpdateDeviceRequest>
{
    public UpdateDeviceRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Hostname).NotEmpty().MaximumLength(253);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Site).MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}
