using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoTales.API.DTOs;
using VoTales.API.Extensions;
using VoTales.API.Services;

namespace VoTales.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TalesController : ControllerBase
    {
        private readonly ITaleService _taleService;

        public TalesController(ITaleService taleService)
        {
            _taleService = taleService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateTaleRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Unable to determine user identity.");
            }

            request.AuthorId = userId;
            request.AuthorName = User.GetAuthorName();

            var result = await _taleService.CreateTaleAsync(request);
            return Ok(new { id = result });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            // Get current user ID if authenticated (optional for this endpoint)
            Guid? currentUserId = null;
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                currentUserId = userId;
            }

            var result = await _taleService.GetTaleAsync(id, currentUserId);
            return Ok(result);
        }

        [HttpGet("roots")]
        public async Task<IActionResult> GetRoots([FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] string sortBy = "popular")
        {
            var result = await _taleService.GetRootTalesAsync(page, size, sortBy);
            return Ok(result);
        }

        [HttpGet("{id}/choices")]
        public async Task<IActionResult> GetChoices(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var result = await _taleService.GetTaleChoicesAsync(id, page, size);
            return Ok(result);
        }

        [HttpPost("{id}/vote")]
        [Authorize]
        public async Task<IActionResult> Vote(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Unable to determine user identity.");
            }

            var result = await _taleService.VoteForTaleAsync(id, userId);

            if (!result)
            {
                return Conflict("Already voted for this tale.");
            }

            return Ok();
        }

        [HttpGet("{id}/map")]
        public async Task<IActionResult> GetMap(Guid id)
        {
            var result = await _taleService.GetStoryMapAsync(id);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query)
        {
            var result = await _taleService.SearchTalesAsync(query);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? User.FindFirst("sub");

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Unable to determine user identity.");
            }

            var result = await _taleService.DeleteTaleAsync(id, userId);

            if (!result)
            {
                return NotFound("Tale not found or you are not the author.");
            }

            return NoContent();
        }
    }
}
