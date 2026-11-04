using FluentValidation;
using ProductService.Application.DTOs;

namespace ProductService.Application.Validators
{
    // MVC does not run item validators for a bare list body, so the list gets its own validator.
    public class BulkUpdateProductListValidator
        : AbstractValidator<List<BulkUpdateProductRequest>>
    {
        public BulkUpdateProductListValidator()
        {
            RuleForEach(x => x)
                .SetValidator(new BulkUpdateProductRequestValidator());
        }
    }
}
