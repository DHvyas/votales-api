using VoTales.API.DTOs;

namespace VoTales.API.Services;

public interface ITaleService
{
    Task<Guid> CreateTaleAsync(CreateTaleRequest request);
    Task<TaleResponseDto> GetTaleAsync(Guid id, Guid? currentUserId = null);
    Task<PagedResult<TaleResponseDto>> GetRootTalesAsync(int pageNumber = 1, int pageSize = 10, string sortBy = "popular");
    Task<PagedResult<TaleChoiceDto>> GetTaleChoicesAsync(Guid taleId, int pageNumber = 1, int pageSize = 10);
    Task<bool> VoteForTaleAsync(Guid taleId, Guid userId);
    Task<StoryMapDto> GetStoryMapAsync(Guid currentTaleId);
    Task<List<TaleResponseDto>> SearchTalesAsync(string query);
    Task<bool> DeleteTaleAsync(Guid taleId, Guid userId);
}
