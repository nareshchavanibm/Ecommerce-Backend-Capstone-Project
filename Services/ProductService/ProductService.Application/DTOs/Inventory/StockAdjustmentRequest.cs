using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.DTOs.Inventory
{
    public class StockLineDto
    {
        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }
    }

    public class StockAdjustmentRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required.")]
        [MaxLength(100, ErrorMessage = "A maximum of 100 items is allowed.")]
        public List<StockLineDto> Items { get; set; } = new();
    }

    public class StockReservationResult
    {
        public bool Success { get; init; }

        public string? Message { get; init; }

        public static StockReservationResult Ok() => new() { Success = true };

        public static StockReservationResult Failed(string message) =>
            new() { Success = false, Message = message };
    }
}
