using Microsoft.EntityFrameworkCore;
using Neo4jClient;
using Newtonsoft.Json;
using VoTales.API.Data;
using VoTales.API.DTOs;
using VoTales.API.Models;

namespace VoTales.API.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _dbContext;
    private readonly IGraphClient _graphClient;

    public UserService(AppDbContext dbContext, IGraphClient graphClient)
    {
        _dbContext = dbContext;
        _graphClient = graphClient;
    }

    public async Task<UserProfileDto> GetUserProfileAsync(Guid userId, string username)
    {
        // Fetch user profile data
        var user = await _dbContext.Users.FindAsync(userId);

        // Fetch all tales by this author
        var userTales = await _dbContext.Tales
            .AsNoTracking()
            .Where(t => t.AuthorId == userId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title, t.Content, t.CreatedAt })
            .ToListAsync();

        if (userTales.Count == 0)
        {
            return new UserProfileDto
            {
                Id = userId,
                Username = user?.DisplayName ?? username,
                Bio = user?.Bio,
                AvatarStyle = user?.AvatarStyle ?? "initials",
                JoinedDate = user?.CreatedAt ?? DateTime.UtcNow,
                TotalTalesWritten = 0,
                TotalVotesReceived = 0,
                MyRoots = [],
                MyBranches = []
            };
        }

        var taleIds = userTales.Select(t => t.Id).ToList();

        // Get vote counts for each tale
        var voteCounts = await _dbContext.Votes
            .AsNoTracking()
            .Where(v => taleIds.Contains(v.TaleId))
            .GroupBy(v => v.TaleId)
            .Select(g => new { TaleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaleId, x => x.Count);

        // Get tale types from Neo4j
        var taleIdsStrings = taleIds.Select(id => id.ToString()).ToList();

        var nodeResults = await _graphClient.Cypher
            .Match("(t:Tale)")
            .Where("t.id IN $ids")
            .WithParam("ids", taleIdsStrings)
            .Return(t => new TaleNodeDto
            {
                Id = t.As<TaleNodeDto>().Id,
                Type = t.As<TaleNodeDto>().Type
            })
            .ResultsAsync;

        var typeDict = nodeResults.ToDictionary(n => Guid.Parse(n.Id), n => n.Type);

        // Build summaries
        var summaries = userTales.Select(t => new TaleSummaryDto
        {
            Id = t.Id,
            Title = t.Title,
            ContentPreview = t.Content.Length > 100 ? t.Content[..100] + "..." : t.Content,
            CreatedAt = t.CreatedAt,
            VotesReceived = voteCounts.GetValueOrDefault(t.Id, 0)
        }).ToList();

        var roots = summaries
            .Where(s => typeDict.GetValueOrDefault(s.Id, "BRANCH") == "ROOT")
            .ToList();

        var branches = summaries
            .Where(s => typeDict.GetValueOrDefault(s.Id, "BRANCH") == "BRANCH")
            .ToList();

        return new UserProfileDto
        {
            Id = userId,
            Username = user?.DisplayName ?? username,
            Bio = user?.Bio,
            AvatarStyle = user?.AvatarStyle ?? "initials",
            JoinedDate = user?.CreatedAt ?? DateTime.UtcNow,
            TotalTalesWritten = userTales.Count,
            TotalVotesReceived = voteCounts.Values.Sum(),
            MyRoots = roots,
            MyBranches = branches
        };
    }

    public async Task DeleteTaleAsync(Guid taleId, Guid userId)
    {
        // Fetch the tale from PostgreSQL
        var tale = await _dbContext.Tales.FindAsync(taleId)
            ?? throw new InvalidOperationException($"Tale with id {taleId} not found.");

        // Security check: ensure the user is the author
        if (tale.AuthorId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to delete this tale.");
        }

        // Safety check: does this tale have children in Neo4j?
        var childCount = await _graphClient.Cypher
            .Match("(t:Tale {id: $id})-[:LEADS_TO]->(child:Tale)")
            .WithParam("id", taleId.ToString())
            .Return(child => child.Count())
            .ResultsAsync;

        if (childCount.FirstOrDefault() > 0)
        {
            throw new InvalidOperationException("Cannot delete a chapter that has branches. Edit it instead.");
        }

        // Delete from Neo4j (remove the node and any incoming relationships)
        await _graphClient.Cypher
            .Match("(t:Tale {id: $id})")
            .WithParam("id", taleId.ToString())
            .DetachDelete("t")
            .ExecuteWithoutResultsAsync();

        // Delete associated votes from PostgreSQL
        var votes = await _dbContext.Votes
            .Where(v => v.TaleId == taleId)
            .ToListAsync();

        _dbContext.Votes.RemoveRange(votes);

        // Delete from PostgreSQL
        _dbContext.Tales.Remove(tale);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<TaleResponseDto> UpdateTaleAsync(Guid taleId, Guid userId, UpdateTaleRequest request)
    {
        // Fetch the tale from PostgreSQL
        var tale = await _dbContext.Tales.FindAsync(taleId)
            ?? throw new InvalidOperationException($"Tale with id {taleId} not found.");

        // Security check: ensure the user is the author
        if (tale.AuthorId != userId)
        {
            throw new UnauthorizedAccessException("You are not authorized to update this tale.");
        }

        // Update fields if provided
        if (request.Title is not null)
        {
            tale.Title = request.Title;
        }

        if (request.Content is not null)
        {
            tale.Content = request.Content;
        }

        await _dbContext.SaveChangesAsync();

        return new TaleResponseDto
        {
            Id = tale.Id,
            Title = tale.Title,
            AuthorName = tale.AuthorName,
            Content = tale.Content,
            AuthorId = tale.AuthorId,
            CreatedAt = tale.CreatedAt,
            Choices = []
        };
    }

    /// <summary>
    /// Gets the user profile entity. Profile is created automatically by Supabase trigger on signup.
    /// </summary>
    public async Task<User?> GetUserProfileEntityAsync(Guid userId)
    {
        return await _dbContext.Users.FindAsync(userId);
    }

    public async Task<User> UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request)
    {
        var user = await _dbContext.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User profile not found. Please try logging out and back in.");

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;

            // Update AuthorName on all user's tales
            await _dbContext.Tales
                .Where(t => t.AuthorId == userId && !t.IsDeleted)
                .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.AuthorName, request.DisplayName));
        }

        if (request.Bio is not null)
        {
            user.Bio = request.Bio;
        }

        if (request.AvatarStyle is not null)
        {
            user.AvatarStyle = request.AvatarStyle;
        }

        await _dbContext.SaveChangesAsync();
        return user;
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        // Note: The actual auth.users record should be deleted via Supabase Admin API or client
        // This will trigger CASCADE delete on the profile, but we handle tales first
        
        var user = await _dbContext.Users.FindAsync(userId);
        
        // Anonymize all tales by this user (preserve story graph structure)
        await _dbContext.Tales
            .Where(t => t.AuthorId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.AuthorName, "Ghost")
                .SetProperty(t => t.AuthorId, Guid.Empty));

        // Delete user's votes
        await _dbContext.Votes
            .Where(v => v.UserId == userId)
            .ExecuteDeleteAsync();

        // Delete user's notifications
        await _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .ExecuteDeleteAsync();

        // Delete user profile record (if exists)
        // Note: If auth.users is deleted first via Supabase, CASCADE will handle this
        if (user is not null)
        {
            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
        }
    }

    public async Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return null;
        }

        var taleCount = await _dbContext.Tales
            .CountAsync(t => t.AuthorId == userId && !t.IsDeleted);

        var voteCount = await _dbContext.Votes
            .Where(v => _dbContext.Tales.Any(t => t.Id == v.TaleId && t.AuthorId == userId && !t.IsDeleted))
            .CountAsync();

        return new PublicUserProfileDto
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarStyle = user.AvatarStyle,
            TaleCount = taleCount,
            VoteCount = voteCount,
            JoinedDate = user.CreatedAt
        };
    }

    public async Task<List<TaleSummaryDto>> GetUserTalesAsync(Guid userId)
    {
        var tales = await _dbContext.Tales
            .AsNoTracking()
            .Where(t => t.AuthorId == userId && !t.IsDeleted)
            .Select(t => new { t.Id, t.Title, t.Content, t.CreatedAt })
            .ToListAsync();

        if (tales.Count == 0)
        {
            return [];
        }

        var taleIds = tales.Select(t => t.Id).ToList();

        var voteCounts = await _dbContext.Votes
            .AsNoTracking()
            .Where(v => taleIds.Contains(v.TaleId))
            .GroupBy(v => v.TaleId)
            .Select(g => new { TaleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaleId, x => x.Count);

        return tales.Select(t => new TaleSummaryDto
        {
            Id = t.Id,
            Title = t.Title,
            ContentPreview = t.Content.Length > 100 ? t.Content[..100] + "..." : t.Content,
            CreatedAt = t.CreatedAt,
            VotesReceived = voteCounts.GetValueOrDefault(t.Id, 0)
        }).ToList();
    }

    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string query)
    {
        return await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.DisplayName.Contains(query))
            .Take(5)
            .Select(u => new UserSearchResultDto
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                AvatarStyle = u.AvatarStyle
            })
            .ToListAsync();
    }

    private class TaleNodeDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }
}
