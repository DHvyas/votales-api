using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoTales.API.DTOs;
using VoTales.API.Extensions;
using VoTales.API.Services;

namespace VoTales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Unable to determine user identity.");
        }

        // Get display name from JWT claims as fallback
        var username = User.GetAuthorName();

        var result = await _userService.GetUserProfileAsync(userId.Value, username);
        return Ok(result);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateUserProfileRequest request)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Unable to determine user identity.");
        }

        try
        {
            var user = await _userService.UpdateUserProfileAsync(userId.Value, request);
            return Ok(new
            {
                user.Id,
                user.DisplayName,
                user.Bio,
                user.AvatarStyle
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("profile"))
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> DeleteMyAccount()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Unable to determine user identity.");
        }

        try
        {
            await _userService.DeleteUserAsync(userId.Value);
            // Note: The caller should also delete the Supabase Auth account on the frontend
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPublicProfile(Guid id)
    {
        var profile = await _userService.GetPublicProfileAsync(id);
        if (profile is null)
        {
            return NotFound($"User with id {id} not found.");
        }

        return Ok(profile);
    }

    [HttpGet("{id:guid}/tales")]
    public async Task<IActionResult> GetUserTales(Guid id)
    {
        var tales = await _userService.GetUserTalesAsync(id);
        return Ok(tales);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(new List<UserSearchResultDto>());
        }

        var results = await _userService.SearchUsersAsync(query);
        return Ok(results);
    }

    [HttpDelete("/api/tales/{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteTale(Guid id)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Unable to determine user identity.");
        }

        try
        {
            await _userService.DeleteTaleAsync(id, userId.Value);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("branches"))
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("/api/tales/{id}")]
    [Authorize]
    public async Task<IActionResult> UpdateTale(Guid id, [FromBody] UpdateTaleRequest request)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Unable to determine user identity.");
        }

        try
        {
            var result = await _userService.UpdateTaleAsync(id, userId.Value, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, ex.Message);
        }
    }
}
