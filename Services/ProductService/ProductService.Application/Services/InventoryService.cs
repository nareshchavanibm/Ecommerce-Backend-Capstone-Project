using ProductService.Application.DTOs.Inventory;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IInventoryRepository _inventoryRepository;

        public InventoryService(IInventoryRepository inventoryRepository)
        {
            _inventoryRepository = inventoryRepository;
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
                throw new ArgumentException(
                    "Quantity and reorder level must be non-negative.");

            var existingInventory =
                await _inventoryRepository.GetByProductIdAsync(productId);

            if (existingInventory != null)
                throw new InvalidOperationException(
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
                throw new ArgumentException(
                    "Quantity and reorder level must be non-negative.");

            var inventory =
                await _inventoryRepository.GetByProductIdAsync(productId);

            if (inventory == null)
                return null;

            inventory.Quantity = request.Quantity;
            inventory.ReorderLevel = request.ReorderLevel;

            await _inventoryRepository.UpdateAsync(inventory);

            return MapToResponse(inventory);
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