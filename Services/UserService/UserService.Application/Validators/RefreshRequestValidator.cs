using FluentValidation;
using UserService.Application.DTOs.Auth;

namespace UserService.Application.Validators
{
    public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
    {
        public RefreshRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty()
                .WithMessage("Refresh token is required.")
                .MaximumLength(200)
                .WithMessage("Refresh token is not valid.");
        }
    }
}
