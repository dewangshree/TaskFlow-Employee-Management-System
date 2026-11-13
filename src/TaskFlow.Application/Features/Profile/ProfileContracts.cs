using FluentValidation;

namespace TaskFlow.Application.Features.Profile;

public sealed record ProfileDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    DateTime JoinDate,
    string Role,
    int AssignedTasks,
    int CompletedTasks,
    int PendingLeaveRequests);

public sealed record UpdateProfileRequest(string FirstName, string LastName, string Email, string Department);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmPassword);

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Department).NotEmpty().MaximumLength(100);
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6).MaximumLength(100);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword).WithMessage("Passwords do not match.");
    }
}
