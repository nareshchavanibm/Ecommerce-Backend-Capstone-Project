using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoryController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var categories = await _categoryService.GetAllAsync();

            return Ok(categories);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var category = await _categoryService.GetByIdAsync(id);

            if (category == null)
                return NotFound(new
                {
                    message = "Category not found."
                });

            return Ok(category);
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CategoryCreateDto request)
        {
            var category = await _categoryService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.Id },
                category);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] CategoryCreateDto request)
        {
            var category = await _categoryService.UpdateAsync(id, request);

            if (category == null)
                return NotFound(new
                {
                    message = "Category not found."
                });

            return Ok(category);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _categoryService.DeleteAsync(id);

            if (!deleted)
                return NotFound(new
                {
                    message = "Category not found."
                });

            return NoContent();
        }
    }
}