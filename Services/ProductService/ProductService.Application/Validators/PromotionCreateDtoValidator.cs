using FluentValidation;
using ProductService.Application.DTOs.Promotions;
using ProductService.Domain.Entities;

namespace ProductService.Application.Validators
{
    public class PromotionCreateDtoValidator : AbstractValidator<PromotionCreateDto>
    {
        public PromotionCreateDtoValidator()
        {
            RuleFor(x => x.Type)
                .Must(value => Enum.TryParse<PromotionType>(value, true, out _))
                .WithMessage("Type must be Banner, Ad or Offer.");

            RuleFor(x => x.Title)
                .NotEmpty()
                .MaximumLength(120);

            RuleFor(x => x.Subtitle)
                .MaximumLength(300);

            RuleFor(x => x.DiscountText)
                .MaximumLength(50);

            RuleFor(x => x.PromoCode)
                .MaximumLength(50);

            RuleFor(x => x.ImageUrl)
                .MaximumLength(500)
                .Must(BeHttpUrl)
                .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                .WithMessage("Image URL must start with http:// or https://.");

            // Only same-site paths or http(s) links, so a stored link can never run script.
            RuleFor(x => x.LinkUrl)
                .MaximumLength(500)
                .Must(value => BeHttpUrl(value) || BeRelativePath(value))
                .When(x => !string.IsNullOrWhiteSpace(x.LinkUrl))
                .WithMessage("Link must be a path starting with / or an http(s) URL.");

            RuleFor(x => x.EndsAt)
                .GreaterThan(x => x.StartsAt)
                .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
                .WithMessage("End date must be after the start date.");
        }

        private static bool BeHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private static bool BeRelativePath(string value) =>
            value.StartsWith('/') && !value.StartsWith("//") && !value.Contains('\\');
    }
}
