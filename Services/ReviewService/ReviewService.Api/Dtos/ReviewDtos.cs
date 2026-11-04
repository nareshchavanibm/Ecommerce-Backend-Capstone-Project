using System.ComponentModel.DataAnnotations;
using ReviewService.Api.Models;

namespace ReviewService.Api.Dtos
{
    public sealed class MaxWordsAttribute(int maxWords) : ValidationAttribute
    {
        public static int CountWords(string text) =>
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string text && CountWords(text) > maxWords)
            {
                return new ValidationResult($"Your review can be at most {maxWords} words.");
            }

            return ValidationResult.Success;
        }
    }

    public sealed class ReviewWriteDto
    {
        [Range(1, 5, ErrorMessage = "Choose a rating from 1 to 5.")]
        public int Rating { get; set; }

        [StringLength(ReviewLimits.MaxTitleCharacters, ErrorMessage = "The title can be at most 100 characters.")]
        public string? Title { get; set; }

        [StringLength(ReviewLimits.MaxBodyCharacters, ErrorMessage = "Your review is too long.")]
        [MaxWords(ReviewLimits.MaxWords)]
        public string? Body { get; set; }
    }

    public sealed record ReviewDto(
        int Id,
        int ProductId,
        string ReviewerName,
        int Rating,
        string? Title,
        string? Body,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        bool IsMine);

    public sealed record RatingSummaryDto(
        int ProductId,
        double Average,
        int Count,
        // Index 0 is the number of 1-star reviews, index 4 the number of 5-star reviews.
        int[] Distribution);

    public sealed record ReviewPageDto(
        IReadOnlyList<ReviewDto> Items,
        int TotalItems,
        int Page,
        int PageSize,
        RatingSummaryDto Summary);

    // What the signed-in customer may do for one product.
    public sealed record MyReviewStatusDto(bool CanReview, ReviewDto? Review);
}
