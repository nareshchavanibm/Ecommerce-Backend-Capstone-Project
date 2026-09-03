namespace ECommerce.Application.DTOs.Inventory;

public class InventoryDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int StockQuantity { get; set; }

    public int ReorderLevel { get; set; }

    public bool IsLowStock { get; set; }
}