using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            if (!string.IsNullOrWhiteSpace(search) && search.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Search cannot exceed 100 characters."
                });
            }

            if (!string.IsNullOrWhiteSpace(category) && category.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Category cannot exceed 100 characters."
                });
            }

            if (minPrice.HasValue && minPrice.Value < 0)
            {
                return BadRequest(new
                {
                    message = "Minimum price cannot be negative."
                });
            }

            if (maxPrice.HasValue && maxPrice.Value < 0)
            {
                return BadRequest(new
                {
                    message = "Maximum price cannot be negative."
                });
            }

            if (minPrice.HasValue &&
                maxPrice.HasValue &&
                minPrice.Value > maxPrice.Value)
            {
                return BadRequest(new
                {
                    message = "Minimum price cannot be greater than maximum price."
                });
            }

            if (!string.IsNullOrWhiteSpace(sortBy) &&
                !new[] { "name", "price" }
                    .Contains(sortBy.ToLower()))
            {
                return BadRequest(new
                {
                    message = "sortBy must be either 'name' or 'price'."
                });
            }

            if (!string.IsNullOrWhiteSpace(sortOrder) &&
                !new[] { "asc", "desc" }
                    .Contains(sortOrder.ToLower()))
            {
                return BadRequest(new
                {
                    message = "sortOrder must be either 'asc' or 'desc'."
                });
            }

            var products = await _productService.GetAllAsync(
                search,
                category,
                minPrice,
                maxPrice,
                sortBy,
                sortOrder);

            return Ok(products);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Product ID must be greater than 0."
                });
            }

            var product = await _productService.GetByIdAsync(id);

            if (product == null)
            {
                return NotFound(new
                {
                    message = "Product not found."
                });
            }

            return Ok(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] ProductCreateDto request)
        {
            var product = await _productService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] ProductCreateDto request)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Product ID must be greater than 0."
                });
            }

            var product = await _productService.UpdateAsync(id, request);

            if (product == null)
            {
                return NotFound(new
                {
                    message = "Product not found."
                });
            }

            return Ok(product);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new
                {
                    message = "Product ID must be greater than 0."
                });
            }

            var deleted = await _productService.DeleteAsync(id);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = "Product not found."
                });
            }

            return NoContent();
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("bulk-update")]
        public async Task<IActionResult> BulkUpdate(
            [FromBody] List<BulkUpdateProductRequest> requests)
        {
            if (requests == null || requests.Count == 0)
            {
                return BadRequest(new
                {
                    message = "At least one product is required."
                });
            }

            if (requests.Count > 100)
            {
                return BadRequest(new
                {
                    message = "A maximum of 100 products can be updated at once."
                });
            }

            var products = await _productService.BulkUpdateAsync(requests);

            return Ok(products);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] decimal? minPrice = null,
            [FromQuery] decimal? maxPrice = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            if (pageNumber < 1)
            {
                return BadRequest(new
                {
                    message = "Page number must be greater than 0."
                });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new
                {
                    message = "Page size must be between 1 and 100."
                });
            }

            if (!string.IsNullOrWhiteSpace(search) && search.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Search cannot exceed 100 characters."
                });
            }

            if (!string.IsNullOrWhiteSpace(category) && category.Length > 100)
            {
                return BadRequest(new
                {
                    message = "Category cannot exceed 100 characters."
                });
            }

            if (minPrice.HasValue && minPrice.Value < 0)
            {
                return BadRequest(new
                {
                    message = "Minimum price cannot be negative."
                });
            }

            if (maxPrice.HasValue && maxPrice.Value < 0)
            {
                return BadRequest(new
                {
                    message = "Maximum price cannot be negative."
                });
            }

            if (minPrice.HasValue &&
                maxPrice.HasValue &&
                minPrice.Value > maxPrice.Value)
            {
                return BadRequest(new
                {
                    message = "Minimum price cannot be greater than maximum price."
                });
            }

            if (!string.IsNullOrWhiteSpace(sortBy) &&
                !new[] { "name", "price" }
                    .Contains(sortBy.ToLower()))
            {
                return BadRequest(new
                {
                    message = "sortBy must be either 'name' or 'price'."
                });
            }

            if (!string.IsNullOrWhiteSpace(sortOrder) &&
                !new[] { "asc", "desc" }
                    .Contains(sortOrder.ToLower()))
            {
                return BadRequest(new
                {
                    message = "sortOrder must be either 'asc' or 'desc'."
                });
            }

            var result = await _productService.GetPagedAsync(
                pageNumber,
                pageSize,
                search,
                category,
                minPrice,
                maxPrice,
                sortBy,
                sortOrder);

            return Ok(result);
        }
    }
}