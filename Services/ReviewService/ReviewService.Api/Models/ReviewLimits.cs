namespace ReviewService.Api.Models
{
    public static class ReviewLimits
    {
        public const int MaxWords = 1000;

        // Backstop for the word limit so one "word" cannot be megabytes long.
        public const int MaxBodyCharacters = 10_000;

        public const int MaxTitleCharacters = 100;
    }
}
