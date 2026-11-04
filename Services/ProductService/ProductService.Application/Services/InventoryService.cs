using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Exceptions;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IRestockNotifier? _restockNotifier;

        public InventoryService(
            IInventoryRepository inventoryRepository,
            IRestockNotifier? restockNotifier = null)
        {
            _inventoryRepository = inventoryRepository;
            _restockNotifier = restockNotifier;
        }

        public async Task<InventoryResponseDto?> GetByProductIdAsync(int productId)
        {
            var inventory = await _inventoryRepository.GetByProductIdAsync(productId);

            if (inventory == null)
                return null;

            return MapToResponse(inventory);
        }

        public async Task<List<InventoryResponseDto>> GetAllAsync()
        {
            var inventories = await _inventoryRepository.GetAllAsync();

            return inventories
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<InventoryResponseDto> CreateAsync(
            int productId,
            InventoryUpdateDto request)
        {
            if (request.Quantity < 0 || request.ReorderLevel < 0)
                throw new BusinessRuleException(
                    "Quantity and reorder level must be non-negative.");

            var existingInventory =
                await _inventoryRepository.GetByProductIdAsync(productId);

            if (existingInventory != null)
                throw new BusinessRuleException(
                    "Inventory already exists for this product.");

            var inventory = new Inventory
            {
                ProductId = productId,
                Quantity = request.Quantity,
                ReorderLevel = request.ReorderLevel
            };

            await _inventoryRepository.AddAsync(inventory);

            return MapToResponse(inventory);
        }

        public async Task<InventoryResponseDto?> UpdateAsync(
            int productId,
            InventoryUpdateDto request)
        {
            if (request.Quantity < 0 || request.ReorderLevel < 0)
                throw new BusinessRuleException(
                    "Quantity and reorder level must be non-negative.");

            var inventory =
                await _inventoryRepository.GetByProductIdAsync(productId);

            if (inventory == null)
                return null;

            var wasOutOfStock = inventory.Quantity <= 0;

            inventory.Quantity = request.Quantity;
            inventory.ReorderLevel = request.ReorderLevel;

            await _inventoryRepository.UpdateAsync(inventory);

            if (wasOutOfStock && inventory.Quantity > 0 && _restockNotifier != null)
                await _restockNotifier.ProductRestockedAsync(productId);

            return MapToResponse(inventory);
        }

        public Task<StockReservationResult> ReserveAsync(
            StockAdjustmentRequest request)
        {
            return _inventoryRepository.ReserveAsync(Merge(request));
        }

        public Task ReleaseAsync(StockAdjustmentRequest request)
        {
            return _inventoryRepository.ReleaseAsync(Merge(request));
        }

        // Repeated products in one request are summed into a single line.
        private static List<StockLineDto> Merge(StockAdjustmentRequest request)
        {
            return request.Items
                .GroupBy(x => x.ProductId)
                .Select(group => new StockLineDto
                {
                    ProductId = group.Key,
                    Quantity = group.Sum(x => x.Quantity)
                })
                .ToList();
        }

        private static InventoryResponseDto MapToResponse(
            Inventory inventory)
        {
            return new InventoryResponseDto
            {
                Id = inventory.Id,
                ProductId = inventory.ProductId,
                ProductName = inventory.Product?.Name ?? string.Empty,
                Quantity = inventory.Quantity,
                ReorderLevel = inventory.ReorderLevel,
                IsLowStock = inventory.Quantity <= inventory.ReorderLevel
            };
        }
    }
}