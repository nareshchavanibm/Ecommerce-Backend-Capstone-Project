using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ProductService.Application.DTOs;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDistributedCache _cache;

        private const string ProductCacheVersionKey = "products_cache_version";

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IDistributedCache cache)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _cache = cache;
        }

        public async Task<ProductResponseDto?> GetByIdAsync(int id)
        {
            var cacheVersion = await GetProductCacheVersionAsync();

            var cacheKey = $"product_{cacheVersion}_{id}";

            var cachedProduct = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedProduct))
            {
                return JsonSerializer.Deserialize<ProductResponseDto>(
                    cachedProduct);
            }

            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
                return null;

            var result = MapToResponse(product);

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                GetCacheOptions());

            return result;
        }

        public async Task<List<ProductResponseDto>> GetAllAsync(
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var cacheVersion = await GetProductCacheVersionAsync();

            var cacheKey =
                $"products_{cacheVersion}_{search}_{category}_{minPrice}_{maxPrice}_{sortBy}_{sortOrder}";

            var cachedProducts = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedProducts))
            {
                return JsonSerializer.Deserialize<List<ProductResponseDto>>(
                    cachedProducts) ?? new List<ProductResponseDto>();
            }

            var products = await _productRepository.GetAllAsync(
                search,
                category,
                minPrice,
                maxPrice,
                sortBy,
                sortOrder);

            var result = products
                .Select(MapToResponse)
                .ToList();

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                GetCacheOptions());

            return result;
        }

        public async Task<PagedProductResponseDto> GetPagedAsync(
            int pageNumber,
            int pageSize,
            string? search = null,
            string? category = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            string? sortOrder = null)
        {
            var cacheVersion = await GetProductCacheVersionAsync();

            var cacheKey =
                $"products_paged_{cacheVersion}_{pageNumber}_{pageSize}_{search}_{category}_{minPrice}_{maxPrice}_{sortBy}_{sortOrder}";

            var cachedResult = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedResult))
            {
                return JsonSerializer.Deserialize<PagedProductResponseDto>(
                    cachedResult)!;
            }

            var result = await _productRepository.GetPagedAsync(
                pageNumber,
                pageSize,
                search,
                category,
                minPrice,
                maxPrice,
                sortBy,
                sortOrder);

            var totalPages = (int)Math.Ceiling(
                (double)result.TotalItems / pageSize);

            var response = new PagedProductResponseDto
            {
                Items = result.Products
                    .Select(MapToResponse)
                    .ToList(),

                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = result.TotalItems,
                TotalPages = totalPages
            };

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                GetCacheOptions());

            return response;
        }

        public async Task<ProductResponseDto> CreateAsync(
            ProductCreateDto request)
        {
            var category = await _categoryRepository
                .GetByIdAsync(request.CategoryId);

            if (category == null)
                throw new InvalidOperationException("Category not found.");

            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Price = request.Price,
                ImageUrl = request.ImageUrl,
                CategoryId = request.CategoryId,
                Category = category,
                IsActive = true
            };

            await _productRepository.AddAsync(product);

            await InvalidateProductCacheAsync();

            return MapToResponse(product);
        }

        public async Task<ProductResponseDto?> UpdateAsync(
            int id,
            ProductCreateDto request)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
                return null;

            var category = await _categoryRepository
                .GetByIdAsync(request.CategoryId);

            if (category == null)
                throw new InvalidOperationException("Category not found.");

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.ImageUrl = request.ImageUrl;
            product.CategoryId = request.CategoryId;
            product.Category = category;

            await _productRepository.UpdateAsync(product);

            await InvalidateProductCacheAsync();

            return MapToResponse(product);
        }

        public async Task<List<ProductResponseDto>> BulkUpdateAsync(
            List<BulkUpdateProductRequest> requests)
        {
            var productIds = requests
                .Select(x => x.Id)
                .Distinct()
                .ToList();

            var products = await _productRepository
                .GetByIdsAsync(productIds);

            var categoryIds = requests
                .Where(x => x.CategoryId.HasValue)
                .Select(x => x.CategoryId!.Value)
                .Distinct()
                .ToList();

            var categories = categoryIds.Count > 0
                ? await _categoryRepository.GetByIdsAsync(categoryIds)
                : new List<Category>();

            var categoryDictionary = categories
                .ToDictionary(x => x.Id);

            var productDictionary = products
                .ToDictionary(x => x.Id);

            var productsToUpdate = new List<Product>();
            var updatedProducts = new List<ProductResponseDto>();

            foreach (var request in requests)
            {
                if (!productDictionary.TryGetValue(
                        request.Id,
                        out var product))
                {
                    continue;
                }

                if (request.CategoryId.HasValue)
                {
                    var categoryId = request.CategoryId.Value;

                    if (categoryDictionary.TryGetValue(
                            categoryId,
                            out var category))
                    {
                        product.CategoryId = category.Id;
                        product.Category = category;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(
                                request.CategoryName))
                        {
                            throw new InvalidOperationException(
                                $"Category with ID {categoryId} not found. " +
                                "Provide CategoryName to create a new category.");
                        }

                        category = new Category
                        {
                            Name = request.CategoryName
                        };

                        await _categoryRepository.AddAsync(category);

                        categoryDictionary[category.Id] = category;

                        product.CategoryId = category.Id;
                        product.Category = category;
                    }
                }

                if (request.Name != null)
                    product.Name = request.Name;

                if (request.Description != null)
                    product.Description = request.Description;

                if (request.Price.HasValue)
                    product.Price = request.Price.Value;

                if (request.ImageUrl != null)
                    product.ImageUrl = request.ImageUrl;

                productsToUpdate.Add(product);

                updatedProducts.Add(MapToResponse(product));
            }

            if (productsToUpdate.Count > 0)
            {
                await _productRepository.UpdateRangeAsync(
                    productsToUpdate);

                await InvalidateProductCacheAsync();
            }

            return updatedProducts;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
                return false;

            await _productRepository.DeleteAsync(product);

            await InvalidateProductCacheAsync();

            return true;
        }

        private async Task<string> GetProductCacheVersionAsync()
        {
            var version = await _cache.GetStringAsync(
                ProductCacheVersionKey);

            if (!string.IsNullOrEmpty(version))
            {
                return version;
            }

            version = Guid.NewGuid().ToString();

            await _cache.SetStringAsync(
                ProductCacheVersionKey,
                version,
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });

            return version;
        }

        private async Task InvalidateProductCacheAsync()
        {
            await _cache.SetStringAsync(
                ProductCacheVersionKey,
                Guid.NewGuid().ToString(),
                new DistributedCacheEntryOptions
                {
                    SlidingExpiration = TimeSpan.FromHours(1)
                });
        }

        private static DistributedCacheEntryOptions GetCacheOptions()
        {
            return new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
        }

        private static ProductResponseDto MapToResponse(Product product)
        {
            return new ProductResponseDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.Name ?? string.Empty,
                IsActive = product.IsActive
            };
        }
    }
}