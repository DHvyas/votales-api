using VoTales.API.DTOs;
using VoTales.API.Models;

namespace VoTales.API.Services;

public interface IUserService
{
    Task<UserProfileDto> GetUserProfileAsync(Guid userId, string username);
    Task DeleteTaleAsync(Guid taleId, Guid userId);
    Task<TaleResponseDto> UpdateTaleAsync(Guid taleId, Guid userId, UpdateTaleRequest request);

    // User profile management (profile created automatically via Supabase trigger)
    Task<User?> GetUserProfileEntityAsync(Guid userId);
    Task<User> UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request);
    Task DeleteUserAsync(Guid userId);

    // Public profile endpoints
    Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId);
    Task<List<TaleSummaryDto>> GetUserTalesAsync(Guid userId);
    Task<List<UserSearchResultDto>> SearchUsersAsync(string query);
}
