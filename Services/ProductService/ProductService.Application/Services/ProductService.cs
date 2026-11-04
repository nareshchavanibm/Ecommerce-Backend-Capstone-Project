using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using ProductService.Application.DTOs;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Exceptions;
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
                var cached = JsonSerializer.Deserialize<ProductResponseDto>(
                    cachedProduct);

                if (cached != null)
                    await ApplyStockAsync([cached]);

                return cached;
            }

            var product = await _productRepository.GetByIdAsync(id);

            if (product == null)
                return null;

            var result = MapToResponse(product);

            result.Specifications = SpecificationNormalizer.ToDtos(
                await _productRepository.GetSpecificationsAsync(id));

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                GetCacheOptions());

            await ApplyStockAsync([result]);

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
                var cached = JsonSerializer.Deserialize<List<ProductResponseDto>>(
                    cachedProducts) ?? new List<ProductResponseDto>();

                await ApplyStockAsync(cached);

                return cached;
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

            await ApplyStockAsync(result);

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
                var cached = JsonSerializer.Deserialize<PagedProductResponseDto>(
                    cachedResult)!;

                await ApplyStockAsync(cached.Items);

                return cached;
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

            await ApplyStockAsync(response.Items);

            return response;
        }

        public async Task<ProductResponseDto> CreateAsync(
            ProductCreateDto request)
        {
            var category = await _categoryRepository
                .GetByIdAsync(request.CategoryId);

            if (category == null)
                throw new BusinessRuleException("Category not found.");

            var specificationError = SpecificationNormalizer.TryNormalize(
                request.Specifications,
                out var specifications);

            if (specificationError != null)
                throw new BusinessRuleException(specificationError);

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

            if (specifications.Count > 0)
                await _productRepository.ReplaceSpecificationsAsync(product.Id, specifications);

            await InvalidateProductCacheAsync();

            var response = MapToResponse(product);

            response.Specifications = SpecificationNormalizer.ToDtos(specifications);

            return response;
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
                throw new BusinessRuleException("Category not found.");

            var specificationError = SpecificationNormalizer.TryNormalize(
                request.Specifications,
                out var specifications);

            if (specificationError != null)
                throw new BusinessRuleException(specificationError);

            product.Name = request.Name;
            product.Description = request.Description;
            product.Price = request.Price;
            product.ImageUrl = request.ImageUrl;
            product.CategoryId = request.CategoryId;
            product.Category = category;

            await _productRepository.UpdateAsync(product);

            if (request.Specifications != null)
                await _productRepository.ReplaceSpecificationsAsync(id, specifications);

            await InvalidateProductCacheAsync();

            var response = MapToResponse(product);

            response.Specifications = request.Specifications != null
                ? SpecificationNormalizer.ToDtos(specifications)
                : SpecificationNormalizer.ToDtos(await _productRepository.GetSpecificationsAsync(id));

            return response;
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
                            throw new BusinessRuleException(
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
            var products = new List<(Product Product, int Quantity, int ReorderLevel, List<ProductSpecification> Specifications)>();

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

                products.Add((built.Product!, built.Quantity, built.ReorderLevel, built.Specifications));
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

                var specifications = products
                    .SelectMany(x => x.Specifications.Select(s =>
                    {
                        s.ProductId = x.Product.Id;
                        return s;
                    }))
                    .ToList();

                if (specifications.Count > 0)
                    await _productRepository.AddSpecificationsAsync(specifications);

                await InvalidateProductCacheAsync();
            }

            result.Inserted = products.Count;

            return result;
        }

        private static readonly List<ProductSpecification> NoSpecifications = new();

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

        private async Task<(Product? Product, int Quantity, int ReorderLevel, List<ProductSpecification> Specifications, string? Error)> TryBuildImportProductAsync(
            ProductImportRow row,
            Dictionary<string, Category> categories,
            Dictionary<int, Category> categoriesById)
        {
            var name = row.Name?.Trim();
            var description = row.Description?.Trim() ?? string.Empty;
            var imageUrl = row.ImageUrl?.Trim();

            if (string.IsNullOrEmpty(name))
                return (null, 0, 0, NoSpecifications, "Name is required.");

            if (name.Length > 100)
                return (null, 0, 0, NoSpecifications, "Name cannot exceed 100 characters.");

            if (description.Length > 1000)
                return (null, 0, 0, NoSpecifications, "Description cannot exceed 1000 characters.");

            if (!decimal.TryParse(
                    row.Price,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var price) || price <= 0)
                return (null, 0, 0, NoSpecifications, "Price must be a number greater than 0.");

            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.Length > 500)
                return (null, 0, 0, NoSpecifications, "ImageUrl cannot exceed 500 characters.");

            if (!TryParseCount(row.StockQuantity, DefaultImportStock, out var quantity))
                return (null, 0, 0, NoSpecifications, "StockQuantity must be a whole number, 0 or more.");

            if (!TryParseCount(row.ReorderLevel, DefaultImportReorderLevel, out var reorderLevel))
                return (null, 0, 0, NoSpecifications, "ReorderLevel must be a whole number, 0 or more.");

            var specificationError = SpecificationNormalizer.TryNormalize(row.Specifications, out var specifications);

            if (specificationError != null)
                return (null, 0, 0, NoSpecifications, specificationError);

            Category? category;
            var categoryName = row.CategoryName?.Trim();

            if (!string.IsNullOrEmpty(categoryName))
            {
                if (categoryName.Length > 100)
                    return (null, 0, 0, NoSpecifications, "CategoryName cannot exceed 100 characters.");

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
                    return (null, 0, 0, NoSpecifications, $"CategoryId {categoryId} does not exist.");
            }
            else
            {
                return (null, 0, 0, NoSpecifications, "CategoryName or a valid CategoryId is required.");
            }

            return (new Product
            {
                Name = name,
                Description = description,
                Price = price,
                ImageUrl = string.IsNullOrEmpty(imageUrl) ? null : imageUrl,
                CategoryId = category.Id,
                IsActive = true
            }, quantity, reorderLevel, specifications, null);
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

        // Stock changes on every order, so it is added after the cached copy is read.
        private async Task ApplyStockAsync(IReadOnlyCollection<ProductResponseDto> products)
        {
            if (products.Count == 0)
                return;

            var quantities = await _inventoryRepository.GetQuantitiesAsync(
                products.Select(x => x.Id).ToList());

            foreach (var product in products)
            {
                product.StockQuantity = quantities.TryGetValue(product.Id, out var quantity)
                    ? quantity
                    : null;
            }
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