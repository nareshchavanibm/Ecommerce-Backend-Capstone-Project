using FluentValidation;
using ProductService.Application.DTOs.Products;

namespace ProductService.Application.Validators
{
    public class ProductCreateDtoValidator : AbstractValidator<ProductCreateDto>
    {
        public ProductCreateDtoValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Description)
                .NotEmpty()
                .MaximumLength(1000);

            RuleFor(x => x.Price)
                .GreaterThan(0);

            RuleFor(x => x.CategoryId)
                .GreaterThan(0);

            RuleFor(x => x.ImageUrl)
                .MaximumLength(500)
                .When(x => !string.IsNullOrWhiteSpace(x.ImageUrl));
        }
    }
}