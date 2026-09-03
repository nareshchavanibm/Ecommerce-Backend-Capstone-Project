using ECommerce.Application.DTOs.Products;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Linq;

namespace ECommerce.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IMemoryCache _cache;
    private readonly ECommerce.Application.Interfaces.INotificationService _notificationService;

    // Track cache keys to allow targeted invalidation for list keys
    private static readonly ConcurrentDictionary<string, byte> _cacheKeys = new();

    public ProductService(IProductRepository productRepository, IMemoryCache cache, ECommerce.Application.Interfaces.INotificationService notificationService)
    {
        _productRepository = productRepository;
        _cache = cache;
        _notificationService = notificationService;
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var products = await _productRepository.GetAllAsync();

        return products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockQuantity = p.Inventory?.Quantity ?? 0
        }).ToList();
    }

    public async Task<ProductPagedResultDto> GetFilteredAsync(ProductQueryDto query)
    {
        var cacheKey = $"product:list:search:{query.Search ?? ""}:cat:{query.CategoryId}:min:{query.MinPrice}:max:{query.MaxPrice}:sort:{query.SortBy}:{query.SortOrder}:page:{query.PageNumber}:size:{query.PageSize}";

        if (_cache.TryGetValue<ProductPagedResultDto>(cacheKey, out var cachedPaged))
        {
            return cachedPaged;
        }

        var (products, totalCount) = await _productRepository.GetFilteredAsync(query);

        var productDtos = products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            CategoryId = p.CategoryId,
            CategoryName = p.Category?.Name ?? string.Empty,
            StockQuantity = p.Inventory?.Quantity ?? 0
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);

        var result = new ProductPagedResultDto
        {
            Products = productDtos,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1)
        };

        _cache.Set(cacheKey, result, options);
        _cacheKeys.TryAdd(cacheKey, 0);

        return result;
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var cacheKey = $"product:detail:{id}";

        if (_cache.TryGetValue<ProductDto>(cacheKey, out var cached))
            return cached;

        var product = await _productRepository.GetByIdAsync(id);

        if (product == null)
            return null;

        var dto = new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.Name ?? string.Empty,
            StockQuantity = product.Inventory?.Quantity ?? 0
        };

        _cache.Set(cacheKey, dto, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
        });

        _cacheKeys.TryAdd(cacheKey, 0);

        return dto;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto)
    {
        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            CategoryId = dto.CategoryId,

            Inventory = new Inventory
            {
                Quantity = dto.InitialStock,
                ReorderLevel = 10
            }
        };

        var createdProduct = await _productRepository.AddAsync(product);

        // Invalidate list caches so new product appears in searches
        foreach (var key in _cacheKeys.Keys.Where(k => k.StartsWith("product:list:")))
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        var createdDto = new ProductDto
        {
            Id = createdProduct.Id,
            Name = createdProduct.Name,
            Description = createdProduct.Description,
            Price = createdProduct.Price,
            CategoryId = createdProduct.CategoryId,
            StockQuantity = createdProduct.Inventory?.Quantity ?? 0
        };

        // Optionally cache detail for created product
        var detailKey = $"product:detail:{createdDto.Id}";
        _cache.Set(detailKey, createdDto, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
        _cacheKeys.TryAdd(detailKey, 0);

        // Notify clients about new product
        try
        {
            await _notificationService.NotifyProductCreated(createdDto);
        }
        catch (Exception) { }

        return createdDto;
    }

    public async Task<bool> UpdateAsync(
        int id,
        UpdateProductDto dto)
    {
        var product =
            await _productRepository.GetByIdAsync(id);

        if (product == null)
        {
            return false;
        }

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.CategoryId = dto.CategoryId;

        await _productRepository.UpdateAsync(product);

        // Invalidate caches for this product and lists
        var detailKey = $"product:detail:{id}";
        _cache.Remove(detailKey);
        _cacheKeys.TryRemove(detailKey, out _);

        foreach (var key in _cacheKeys.Keys.Where(k => k.StartsWith("product:list:")))
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        // Notify clients about update
        try
        {
            var updatedDto = new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name ?? string.Empty,
                StockQuantity = product.Inventory?.Quantity ?? 0
            };

            await _notificationService.NotifyProductUpdated(updatedDto);
        }
        catch (Exception) { }

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product =
            await _productRepository.GetByIdAsync(id);

        if (product == null)
            return false;

        await _productRepository.DeleteAsync(product);

        // Invalidate caches for this product and lists
        var detailKey = $"product:detail:{id}";
        _cache.Remove(detailKey);
        _cacheKeys.TryRemove(detailKey, out _);

        foreach (var key in _cacheKeys.Keys.Where(k => k.StartsWith("product:list:")))
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }

        // Notify clients about deletion
        try
        {
            await _notificationService.NotifyProductDeleted(id);
        }
        catch { }

        return true;
    }
}