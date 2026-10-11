using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Api.Extensions;
using UserService.Application.DTOs.Account;
using UserService.Application.Interfaces.Services;

namespace UserService.Api.Controllers
{
    // Shares the /api/auth prefix so the gateway's existing user route covers it.
    [ApiController]
    [Route("api/auth")]
    [Authorize]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var profile = await _accountService.GetProfileAsync(GetUserId());

            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
        {
            var profile = await _accountService.UpdateProfileAsync(GetUserId(), request);

            return profile == null ? NotFound() : Ok(profile);
        }

        [HttpGet("addresses")]
        public async Task<IActionResult> GetAddresses()
        {
            return Ok(await _accountService.GetAddressesAsync(GetUserId()));
        }

        [HttpPost("addresses")]
        public async Task<IActionResult> AddAddress(AddressRequest request)
        {
            return Ok(await _accountService.AddAddressAsync(GetUserId(), request));
        }

        [HttpPut("addresses/{id:int}")]
        public async Task<IActionResult> UpdateAddress(int id, AddressRequest request)
        {
            var address = await _accountService.UpdateAddressAsync(GetUserId(), id, request);

            return address == null ? NotFound() : Ok(address);
        }

        [HttpPut("addresses/{id:int}/default")]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            var address = await _accountService.SetDefaultAddressAsync(GetUserId(), id);

            return address == null ? NotFound() : Ok(address);
        }

        [HttpDelete("addresses/{id:int}")]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            return await _accountService.DeleteAddressAsync(GetUserId(), id)
                ? NoContent()
                : NotFound();
        }

        private int GetUserId()
        {
            if (!int.TryParse(User.GetUserId(), out var userId))
                throw new UnauthorizedAccessException(
                    "User ID is missing from the authentication token.");

            return userId;
        }
    }
}
