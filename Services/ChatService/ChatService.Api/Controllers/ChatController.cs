using System.Security.Claims;
using ChatService.Api.Foundry;
using ChatService.Api.Governance;
using ChatService.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ChatService.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    [EnableRateLimiting("chat")]
    public class ChatController : ControllerBase
    {
        private readonly ChatOrchestrator _orchestrator;
        private readonly AuditLog _audit;

        public ChatController(ChatOrchestrator orchestrator, AuditLog audit)
        {
            _orchestrator = orchestrator;
            _audit = audit;
        }

        // Anonymous visitors get FAQ and recommendations; order tools need a valid customer token.
        // The role comes only from the validated token, never from the request body.
        [HttpPost]
        public async Task<IActionResult> Send([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            var signedIn = User.Identity?.IsAuthenticated == true;

            var userId = signedIn ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;

            var bearer = signedIn
                ? Request.Headers.Authorization.ToString()["Bearer ".Length..]
                : null;

            try
            {
                return Ok(await _orchestrator.HandleAsync(request, userId, CurrentRole(), bearer, cancellationToken));
            }
            catch (ModelUnavailableException)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "The assistant is unavailable right now. Please try again shortly."
                });
            }
        }

        // Lets the widget show the right title, intro and suggested questions for the caller.
        [HttpGet("config")]
        public IActionResult Config() => Ok(ChatProfiles.For(CurrentRole()));

        private ChatRole CurrentRole()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return ChatRole.Anonymous;
            }

            return User.IsInRole("Admin") ? ChatRole.Admin : ChatRole.Customer;
        }

        [HttpPost("feedback")]
        public IActionResult Feedback([FromBody] FeedbackRequest request)
        {
            if (request.Rating is not ("up" or "down") || !Guid.TryParse(request.ConversationId, out var id))
            {
                return BadRequest(new { message = "Invalid feedback." });
            }

            _audit.Feedback(
                id.ToString("N"),
                User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
                request.Agent ?? "unknown",
                request.Rating);

            return NoContent();
        }
    }
}
