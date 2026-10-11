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
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IDistributedCache _cache;

        private const string ProductCacheVersionKey = "products_cache_version";

        // Stock given to imported products whose file has no stock columns.
        private const int DefaultImportStock = 10;
        private const int DefaultImportReorderLevel = 2;
        private const int MaxStockValue = 10_000_000;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IInventoryRepository inventoryRepository,
            IDistributedCache cache)
        {
            _productRepository = productRepository;
            _inventoryRepository = inventoryRepository;
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

        public async Task<ProductImportResult> ImportAsync(
            List<ProductImportRow> rows)
        {
            var result = new ProductImportResult { TotalRows = rows.Count };

            var categories = (await _categoryRepository.GetAllAsync())
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.First(),
                    StringComparer.OrdinalIgnoreCase);

            var categoriesById = categories.Values.ToDictionary(x => x.Id);
            var products = new List<(Product Product, int Quantity, int ReorderLevel)>();

            foreach (var row in rows)
            {
                var built = await TryBuildImportProductAsync(
                    row,
                    categories,
                    categoriesById);

                if (built.Error != null)
                {
                    result.Errors++;
                    result.ErrorDetails.Add($"Row {row.RowNumber}: {built.Error}");
                    continue;
                }

                products.Add((built.Product!, built.Quantity, built.ReorderLevel));
            }

            if (products.Count > 0)
            {
                await _productRepository.AddRangeAsync(
                    products.Select(x => x.Product).ToList());

                // Product ids exist once saved, so the stock records follow.
                await _inventoryRepository.AddRangeAsync(
                    products.Select(x => new Inventory
                    {
                        ProductId = x.Product.Id,
                        Quantity = x.Quantity,
                        ReorderLevel = x.ReorderLevel
                    }).ToList());

                await InvalidateProductCacheAsync();
            }

            result.Inserted = products.Count;

            return result;
        }

        private static bool TryParseCount(string? text, int fallback, out int value)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                value = fallback;
                return true;
            }

            value = 0;

            if (!decimal.TryParse(
                    text.Trim(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var number)
                || number < 0
                || number > MaxStockValue
                || number != decimal.Truncate(number))
                return false;

            value = (int)number;

            return true;
        }

        private async Task<(Product? Product, int Quantity, int ReorderLevel, string? Error)> TryBuildImportProductAsync(
            ProductImportRow row,
            Dictionary<string, Category> categories,
            Dictionary<int, Category> categoriesById)
        {
            var name = row.Name?.Trim();
            var description = row.Description?.Trim() ?? string.Empty;
            var imageUrl = row.ImageUrl?.Trim();

            if (string.IsNullOrEmpty(name))
                return (null, 0, 0, "Name is required.");

            if (name.Length > 100)
                return (null, 0, 0, "Name cannot exceed 100 characters.");

            if (description.Length > 1000)
                return (null, 0, 0, "Description cannot exceed 1000 characters.");

            if (!decimal.TryParse(
                    row.Price,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var price) || price <= 0)
                return (null, 0, 0, "Price must be a number greater than 0.");

            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.Length > 500)
                return (null, 0, 0, "ImageUrl cannot exceed 500 characters.");

            if (!TryParseCount(row.StockQuantity, DefaultImportStock, out var quantity))
                return (null, 0, 0, "StockQuantity must be a whole number, 0 or more.");

            if (!TryParseCount(row.ReorderLevel, DefaultImportReorderLevel, out var reorderLevel))
                return (null, 0, 0, "ReorderLevel must be a whole number, 0 or more.");

            Category? category;
            var categoryName = row.CategoryName?.Trim();

            if (!string.IsNullOrEmpty(categoryName))
            {
                if (categoryName.Length > 100)
                    return (null, 0, 0, "CategoryName cannot exceed 100 characters.");

                if (!categories.TryGetValue(categoryName, out category))
                {
                    category = new Category { Name = categoryName };
                    await _categoryRepository.AddAsync(category);
                    categories[categoryName] = category;
                    categoriesById[category.Id] = category;
                }
            }
            else if (int.TryParse(row.CategoryId, out var categoryId))
            {
                if (!categoriesById.TryGetValue(categoryId, out category))
                    return (null, 0, 0, $"CategoryId {categoryId} does not exist.");
            }
            else
            {
                return (null, 0, 0, "CategoryName or a valid CategoryId is required.");
            }

            return (new Product
            {
                Name = name,
                Description = description,
                Price = price,
                ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
                CategoryId = category.Id,
                IsActive = true
            }, quantity, reorderLevel, null);
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