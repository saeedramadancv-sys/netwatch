using FluentValidation;
using NetWatch.Domain.Entities;
using NetWatch.Domain.Enums;

namespace NetWatch.Application.Probes;

public class CreateProbeRequestValidator : AbstractValidator<CreateProbeRequest>
{
    public CreateProbeRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.IntervalSeconds).InclusiveBetween(Probe.MinIntervalSeconds, Probe.MaxIntervalSeconds);
        RuleFor(x => x.TimeoutMs).InclusiveBetween(Probe.MinTimeoutMs, Probe.MaxTimeoutMs);
        RuleFor(x => x.FailureThreshold).InclusiveBetween(1, Probe.MaxThreshold);
        RuleFor(x => x.RecoveryThreshold).InclusiveBetween(1, Probe.MaxThreshold);

        RuleFor(x => x.TimeoutMs)
            .LessThan(x => x.IntervalSeconds * 1_000)
            .WithMessage("Timeout must be shorter than the check interval, otherwise checks overlap.");

        RuleFor(x => x.Port)
            .NotNull()
            .InclusiveBetween(1, 65_535)
            .When(x => x.Type == ProbeType.Tcp)
            .WithMessage("A TCP probe requires a port between 1 and 65535.");

        RuleFor(x => x.Port)
            .Null()
            .When(x => x.Type == ProbeType.Icmp)
            .WithMessage("An ICMP probe cannot target a port.");

        RuleFor(x => x.HttpPath)
            .Must(path => path is null || path.StartsWith('/'))
            .When(x => x.Type == ProbeType.Http)
            .WithMessage("HTTP path must start with '/'.");

        RuleFor(x => x.ExpectedStatusCode)
            .InclusiveBetween(100, 599)
            .When(x => x.Type == ProbeType.Http);

        RuleFor(x => x.DegradedLatencyMs)
            .GreaterThan(0)
            .LessThanOrEqualTo(x => x.TimeoutMs)
            .When(x => x.DegradedLatencyMs.HasValue)
            .WithMessage("The degraded threshold must be positive and no larger than the timeout.");
    }
}

public class UpdateProbeRequestValidator : AbstractValidator<UpdateProbeRequest>
{
    public UpdateProbeRequestValidator()
    {
        RuleFor(x => x.IntervalSeconds).InclusiveBetween(Probe.MinIntervalSeconds, Probe.MaxIntervalSeconds);
        RuleFor(x => x.TimeoutMs).InclusiveBetween(Probe.MinTimeoutMs, Probe.MaxTimeoutMs);
        RuleFor(x => x.FailureThreshold).InclusiveBetween(1, Probe.MaxThreshold);
        RuleFor(x => x.RecoveryThreshold).InclusiveBetween(1, Probe.MaxThreshold);

        RuleFor(x => x.TimeoutMs)
            .LessThan(x => x.IntervalSeconds * 1_000)
            .WithMessage("Timeout must be shorter than the check interval, otherwise checks overlap.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65_535)
            .When(x => x.Port.HasValue);

        RuleFor(x => x.HttpPath)
            .Must(path => path!.StartsWith('/'))
            .When(x => !string.IsNullOrEmpty(x.HttpPath))
            .WithMessage("HTTP path must start with '/'.");

        RuleFor(x => x.DegradedLatencyMs)
            .GreaterThan(0)
            .LessThanOrEqualTo(x => x.TimeoutMs)
            .When(x => x.DegradedLatencyMs.HasValue)
            .WithMessage("The degraded threshold must be positive and no larger than the timeout.");
    }
}
