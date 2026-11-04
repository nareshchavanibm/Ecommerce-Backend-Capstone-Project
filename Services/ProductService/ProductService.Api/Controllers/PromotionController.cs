using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs.Promotions;
using ProductService.Application.Interfaces.Services;

namespace ProductService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromotionController : ControllerBase
    {
        private readonly IPromotionService _promotionService;

        public PromotionController(IPromotionService promotionService)
        {
            _promotionService = promotionService;
        }

        // Public: what shoppers see on the storefront.
        [HttpGet("active")]
        public async Task<IActionResult> GetActive()
        {
            return Ok(await _promotionService.GetActiveAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _promotionService.GetAllAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var promotion = await _promotionService.GetByIdAsync(id);

            if (promotion == null)
                return NotFound(new { message = "Promotion not found." });

            return Ok(promotion);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PromotionCreateDto request)
        {
            var promotion = await _promotionService.CreateAsync(request);

            return CreatedAtAction(nameof(GetById), new { id = promotion.Id }, promotion);
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] PromotionCreateDto request)
        {
            var promotion = await _promotionService.UpdateAsync(id, request);

            if (promotion == null)
                return NotFound(new { message = "Promotion not found." });

            return Ok(promotion);
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _promotionService.DeleteAsync(id);

            if (!deleted)
                return NotFound(new { message = "Promotion not found." });

            return NoContent();
        }
    }
}
