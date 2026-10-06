using FluentValidation;
using ProductService.Application.DTOs;

namespace ProductService.Application.Validators
{
    public class BulkUpdateProductRequestValidator
        : AbstractValidator<BulkUpdateProductRequest>
    {
        public BulkUpdateProductRequestValidator()
        {
            RuleFor(x => x.Id)
                .GreaterThan(0);

            RuleFor(x => x.Price)
                .GreaterThan(0)
                .When(x => x.Price.HasValue);

            RuleFor(x => x.CategoryId)
                .GreaterThan(0)
                .When(x => x.CategoryId.HasValue);

            RuleFor(x => x.CategoryName)
                .MaximumLength(100)
                .When(x => !string.IsNullOrWhiteSpace(x.CategoryName));

            RuleFor(x => x.Name)
                .MaximumLength(100)
                .When(x => !string.IsNullOrWhiteSpace(x.Name));

            RuleFor(x => x.Description)
                .MaximumLength(1000)
                .When(x => !string.IsNullOrWhiteSpace(x.Description));

            RuleFor(x => x.ImageUrl)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
        }
    }
}