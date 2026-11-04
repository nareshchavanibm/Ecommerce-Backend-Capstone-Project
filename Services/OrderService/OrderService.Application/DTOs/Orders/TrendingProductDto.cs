namespace OrderService.Application.DTOs.Orders
{
    // Aggregated sales for one product; no customer or order data is exposed.
    public class TrendingProductDto
    {
        public int ProductId { get; set; }

        public int UnitsSold { get; set; }
    }
}
