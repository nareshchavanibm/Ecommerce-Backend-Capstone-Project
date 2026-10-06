using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.DTOs.Inventory
{
    public class InventoryUpdateDto
    {
        [Range(0, int.MaxValue, ErrorMessage = "Quantity cannot be negative.")]
        public int Quantity { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Reorder level cannot be negative.")]
        public int ReorderLevel { get; set; }
    }
}