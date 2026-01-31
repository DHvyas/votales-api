using Microsoft.EntityFrameworkCore;
using Neo4jClient;
using Newtonsoft.Json;
using VoTales.API.Data;
using VoTales.API.DTOs;
using VoTales.API.Models;

namespace VoTales.API.Services;

public class TaleService : ITaleService
{
    private readonly AppDbContext _dbContext;
    private readonly IGraphClient _graphClient;
    private readonly INotificationService _notificationService;

    public TaleService(AppDbContext dbContext, IGraphClient graphClient, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _graphClient = graphClient;
        _notificationService = notificationService;
    }

    public async Task<Guid> CreateTaleAsync(CreateTaleRequest request)
    {
        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Save to PostgreSQL
        var tale = new Tale
        {
            Id = newId,
            AuthorId = request.AuthorId,
            Title = request.Title,
            AuthorName = request.AuthorName,
            Content = request.Content
        };

        _dbContext.Tales.Add(tale);
        await _dbContext.SaveChangesAsync();

        // Save to Neo4j
        if (request.ParentTaleId is null)
        {
            // Create a root node - set LastActivityAt for new root tales
            tale.LastActivityAt = now;
            await _dbContext.SaveChangesAsync();

            await _graphClient.Cypher
                .Create("(t:Tale {id: $id, type: 'ROOT'})")
                .WithParam("id", newId.ToString())
                .ExecuteWithoutResultsAsync();
        }
        else
        {
            // Match parent, create child, and create relationship
            await _graphClient.Cypher
                .Match("(p:Tale {id: $parentId})")
                .Create("(c:Tale {id: $newId, type: 'BRANCH'})")
                .Create("(p)-[:LEADS_TO {votes: 0}]->(c)")
                .WithParam("parentId", request.ParentTaleId.Value.ToString())
                .WithParam("newId", newId.ToString())
                .ExecuteWithoutResultsAsync();

            // Find and update the root node's LastActivityAt
            await UpdateRootLastActivityAsync(request.ParentTaleId.Value, now);

            // Notify parent tale author about the new branch
            var parentTale = await _dbContext.Tales
                .AsNoTracking()
                .Where(t => t.Id == request.ParentTaleId.Value)
                .Select(t => new { t.AuthorId })
                .FirstOrDefaultAsync();

            if (parentTale is not null)
            {
                await _notificationService.CreateNotificationAsync(
                    parentTale.AuthorId,
                    NotificationType.Branch,
                    $"{request.AuthorName} continued your story",
                    newId,
                    request.AuthorId,
                    request.AuthorName);
            }
        }

        return newId;
    }

    public async Task<TaleResponseDto> GetTaleAsync(Guid id, Guid? currentUserId = null)
    {
        Console.WriteLine($"[DEBUG] Starting GetTaleAsync for id: {id}");

        // Fetch Main Story from PostgreSQL
        Console.WriteLine("[DEBUG] Fetching tale from PostgreSQL...");
        var tale = await _dbContext.Tales.FindAsync(id)
            ?? throw new InvalidOperationException($"Tale with id {id} not found.");
        Console.WriteLine($"[DEBUG] PostgreSQL fetch complete. Tale found: {tale.Id}");

        // Fetch top 10 choices from Neo4j ordered by votes DESC
        Console.WriteLine("[DEBUG] Fetching top 10 choices from Neo4j...");
        var graphResults = await _graphClient.Cypher
            .Match("(current:Tale {id: $id})-[r:LEADS_TO]->(next:Tale)")
            .WithParam("id", id.ToString())
            .Return((next, r) => new
            {
                Node = next.As<TaleNodeDto>(),
                Relationship = r.As<LeadsToRelationship>()
            })
            .OrderByDescending("r.votes")
            .Limit(10)
            .ResultsAsync;
        Console.WriteLine("[DEBUG] Neo4j fetch complete.");

        var choicesList = graphResults.ToList();
        Console.WriteLine($"[DEBUG] Found {choicesList.Count} choices.");

        // Fetch Previews from PostgreSQL (only if there are choices)
        var choiceIds = choicesList.Select(c => Guid.Parse(c.Node.Id)).ToList();
        Dictionary<Guid, (string Title, string Preview)> previews = [];

        if (choiceIds.Count > 0)
        {
            Console.WriteLine($"[DEBUG] Fetching previews for {choiceIds.Count} choices...");
            Console.WriteLine($"[DEBUG] Choice IDs: {string.Join(", ", choiceIds)}");
            
            // Batch fetch all previews and titles in a single query using AsNoTracking to avoid tracking conflicts
            var choiceTales = await _dbContext.Tales
                .AsNoTracking()
                .Where(t => choiceIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title, Preview = t.Content.Length > 100 ? t.Content.Substring(0, 100) : t.Content })
                .ToListAsync();
            
            previews = choiceTales.ToDictionary(t => t.Id, t => (t.Title, t.Preview));
            Console.WriteLine("[DEBUG] All previews fetch complete.");
        }

        // Combine into response
        var choices = choicesList.Select(c => new TaleChoiceDto
        {
            Id = Guid.Parse(c.Node.Id),
            Title = previews.GetValueOrDefault(Guid.Parse(c.Node.Id)).Title ?? string.Empty,
            Votes = c.Relationship.Votes,
            PreviewText = previews.GetValueOrDefault(Guid.Parse(c.Node.Id)).Preview ?? string.Empty
        }).ToList();

        // Fetch vote count for this tale
        var voteCount = await _dbContext.Votes.CountAsync(v => v.TaleId == id);

        // Check if current user has voted
        var hasVoted = currentUserId.HasValue &&
            await _dbContext.Votes.AnyAsync(v => v.UserId == currentUserId.Value && v.TaleId == id);

        return new TaleResponseDto
        {
            Id = tale.Id,
            Title = tale.Title,
            AuthorName = tale.AuthorName,
            Content = tale.Content,
            AuthorId = tale.AuthorId,
            CreatedAt = tale.CreatedAt,
            Votes = voteCount,
            HasVoted = hasVoted,
            Choices = choices
        };
    }

    public async Task<PagedResult<TaleChoiceDto>> GetTaleChoicesAsync(Guid taleId, int pageNumber = 1, int pageSize = 10)
    {
        // Get total count of choices from Neo4j
        var countResult = await _graphClient.Cypher
            .Match("(current:Tale {id: $id})-[:LEADS_TO]->(next:Tale)")
            .WithParam("id", taleId.ToString())
            .Return(next => next.Count())
            .ResultsAsync;
        var totalCount = (int)countResult.FirstOrDefault();

        if (totalCount == 0)
        {
            return new PagedResult<TaleChoiceDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // Fetch paginated choices from Neo4j ordered by votes DESC
        int skip = (pageNumber - 1) * pageSize;
        var graphResults = await _graphClient.Cypher
            .Match("(current:Tale {id: $id})-[r:LEADS_TO]->(next:Tale)")
            .WithParam("id", taleId.ToString())
            .Return((next, r) => new
            {
                Node = next.As<TaleNodeDto>(),
                Relationship = r.As<LeadsToRelationship>()
            })
            .OrderByDescending("r.votes")
            .Skip(skip)
            .Limit(pageSize)
            .ResultsAsync;

        var choicesList = graphResults.ToList();

        // Fetch Previews from PostgreSQL
        var choiceIds = choicesList.Select(c => Guid.Parse(c.Node.Id)).ToList();
        Dictionary<Guid, (string Title, string Preview)> previews = [];

        if (choiceIds.Count > 0)
        {
            var choiceTales = await _dbContext.Tales
                .AsNoTracking()
                .Where(t => choiceIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Title, Preview = t.Content.Length > 100 ? t.Content.Substring(0, 100) : t.Content })
                .ToListAsync();
            
            previews = choiceTales.ToDictionary(t => t.Id, t => (t.Title, t.Preview));
        }

        // Combine into response
        var items = choicesList.Select(c => new TaleChoiceDto
        {
            Id = Guid.Parse(c.Node.Id),
            Title = previews.GetValueOrDefault(Guid.Parse(c.Node.Id)).Title ?? string.Empty,
            Votes = c.Relationship.Votes,
            PreviewText = previews.GetValueOrDefault(Guid.Parse(c.Node.Id)).Preview ?? string.Empty
        }).ToList();

        return new PagedResult<TaleChoiceDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private class TaleNodeDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;
    }

    private class LeadsToRelationship
    {
        [JsonProperty("votes")]
        public int Votes { get; set; }
    }

    public async Task<PagedResult<TaleResponseDto>> GetRootTalesAsync(int pageNumber = 1, int pageSize = 10, string sortBy = "popular")
    {
        // Get total count of root tales from Neo4j
        var countResult = await _graphClient.Cypher
            .Match("(n:Tale {type: 'ROOT'})")
            .Return(n => n.Count())
            .ResultsAsync;
        var totalCount = (int)countResult.FirstOrDefault();

        if (totalCount == 0)
        {
            return new PagedResult<TaleResponseDto>
            {
                Items = [],
                TotalCount = 0,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        // Query Neo4j for root tale IDs (no pagination here, we'll paginate in PostgreSQL for proper sorting)
        var graphResults = await _graphClient.Cypher
            .Match("(n:Tale {type: 'ROOT'})")
            .Return(n => n.As<TaleNodeDto>().Id)
            .ResultsAsync;

        var rootIds = graphResults
            .Select(id => Guid.Parse(id))
            .ToList();

        // Query PostgreSQL for tales matching these IDs, excluding soft-deleted ones
        var query = _dbContext.Tales
            .AsNoTracking()
            .Where(t => rootIds.Contains(t.Id) && !t.IsDeleted);

        // Apply sorting based on sortBy parameter
        query = sortBy.ToLowerInvariant() switch
        {
            "trending" or "recent" => query.OrderByDescending(t => t.LastActivityAt ?? t.CreatedAt),
            "newest" => query.OrderByDescending(t => t.CreatedAt),
            _ => query.OrderByDescending(t => t.SeriesVotes).ThenByDescending(t => t.LastActivityAt ?? t.CreatedAt) // "popular" is default
        };

        // Get actual count from PostgreSQL (may differ from Neo4j due to soft deletes)
        var actualCount = await query.CountAsync();

        // Apply pagination
        int skip = (pageNumber - 1) * pageSize;
        var tales = await query
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        // Map to TaleResponseDto with content preview
        var items = tales.Select(t => new TaleResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            AuthorName = t.AuthorName,
            Content = t.Content.Length > 100 ? t.Content[..100] : t.Content,
            AuthorId = t.AuthorId,
            CreatedAt = t.CreatedAt,
            SeriesVotes = t.SeriesVotes,
            LastActivityAt = t.LastActivityAt,
            Choices = []
        }).ToList();

        return new PagedResult<TaleResponseDto>
        {
            Items = items,
            TotalCount = actualCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> VoteForTaleAsync(Guid taleId, Guid userId)
    {
        // Check if user already voted for this tale
        var existingVote = await _dbContext.Votes
            .AnyAsync(v => v.UserId == userId && v.TaleId == taleId);

        if (existingVote)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        // Create and save the Vote record
        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TaleId = taleId,
            VotedAt = now
        };

        _dbContext.Votes.Add(vote);
        await _dbContext.SaveChangesAsync();

        // Update Neo4j scoreboard
        await _graphClient.Cypher
            .Match("()-[r:LEADS_TO]->(t:Tale {id: $id})")
            .WithParam("id", taleId.ToString())
            .Set("r.votes = r.votes + 1")
            .ExecuteWithoutResultsAsync();

        // Bubble up: Find root and update SeriesVotes and LastActivityAt
        await UpdateRootSeriesVotesAsync(taleId, now);

        // Notify tale author about the vote
        var tale = await _dbContext.Tales
            .AsNoTracking()
            .Where(t => t.Id == taleId)
            .Select(t => new { t.AuthorId })
            .FirstOrDefaultAsync();

        if (tale is not null)
        {
            await _notificationService.CreateNotificationAsync(
                tale.AuthorId,
                NotificationType.Vote,
                "Someone voted on your tale",
                taleId,
                userId,
                "A reader");
        }

        return true;
    }

    public async Task<StoryMapDto> GetStoryMapAsync(Guid currentTaleId)
    {
        // Step 1: Find the root of the story tree
        var rootResults = await _graphClient.Cypher
            .Match("(current:Tale {id: $id})")
            .WithParam("id", currentTaleId.ToString())
            .OptionalMatch("(root:Tale {type: 'ROOT'})-[:LEADS_TO*]->(current)")
            .Return((root, current) => new
            {
                RootId = root.As<TaleNodeDto>().Id,
                CurrentId = current.As<TaleNodeDto>().Id,
                CurrentType = current.As<TaleNodeDto>().Type
            })
            .ResultsAsync;

        var rootResult = rootResults.FirstOrDefault();
        if (rootResult == null)
        {
            return new StoryMapDto();
        }

        // Determine the story root (if no root found via path, current is the root)
        var storyRootId = rootResult.RootId ?? rootResult.CurrentId;

        // Step 2: Get all nodes in the tree from the root
        var nodesResults = await _graphClient.Cypher
            .Match("(root:Tale {id: $rootId})")
            .WithParam("rootId", storyRootId)
            .OptionalMatch("(root)-[:LEADS_TO*0..]->(descendant:Tale)")
            .Return(descendant => new TaleNodeDto
            {
                Id = descendant.As<TaleNodeDto>().Id,
                Type = descendant.As<TaleNodeDto>().Type
            })
            .ResultsAsync;

        var nodesList = nodesResults.Where(n => !string.IsNullOrEmpty(n.Id)).ToList();
        var nodeIds = nodesList.Select(n => n.Id).Distinct().ToList();

        if (nodeIds.Count == 0)
        {
            return new StoryMapDto();
        }

        // Step 3: Get all edges between nodes in this tree
        var edgesResults = await _graphClient.Cypher
            .Match("(source:Tale)-[r:LEADS_TO]->(target:Tale)")
            .Where("source.id IN $ids AND target.id IN $ids")
            .WithParam("ids", nodeIds)
            .Return((source, target, r) => new
            {
                SourceId = source.As<TaleNodeDto>().Id,
                TargetId = target.As<TaleNodeDto>().Id,
                Votes = r.As<LeadsToRelationship>().Votes
            })
            .ResultsAsync;

        var edges = edgesResults.Select(e => new MapEdgeDto
        {
            SourceId = Guid.Parse(e.SourceId),
            TargetId = Guid.Parse(e.TargetId),
            Votes = e.Votes
        }).ToList();

        // Step 4: Fetch node details (labels/previews) from PostgreSQL
        var nodeGuids = nodeIds.Select(Guid.Parse).ToList();
        var taleDetails = await _dbContext.Tales
            .AsNoTracking()
            .Where(t => nodeGuids.Contains(t.Id))
            .Select(t => new { t.Id, t.Title, t.Content })
            .ToListAsync();

        var typeDict = nodesList.DistinctBy(n => n.Id).ToDictionary(n => n.Id, n => n.Type);

        var nodes = nodeGuids.Select(id =>
        {
            var tale = taleDetails.FirstOrDefault(t => t.Id == id);
            var label = tale?.Title ?? (tale?.Content.Length > 50 ? tale.Content[..50] + "..." : tale?.Content ?? string.Empty);
            var type = typeDict.GetValueOrDefault(id.ToString(), "BRANCH");

            return new MapNodeDto
            {
                Id = id,
                Label = label,
                Type = type
            };
        }).ToList();

        return new StoryMapDto
        {
            Nodes = nodes,
            Edges = edges
        };
    }

    public async Task<List<TaleResponseDto>> SearchTalesAsync(string query)
    {
        var tales = await _dbContext.Tales
            .AsNoTracking()
            .Where(t => t.Title.Contains(query) || t.Content.Contains(query) || t.AuthorName.Contains(query))
            .Take(20)
            .ToListAsync();

        return tales.Select(t => new TaleResponseDto
        {
            Id = t.Id,
            Title = t.Title,
            AuthorName = t.AuthorName,
            Content = t.Content.Length > 100 ? t.Content[..100] : t.Content,
            AuthorId = t.AuthorId,
            CreatedAt = t.CreatedAt,
            Choices = []
        }).ToList();
    }

    public async Task<bool> DeleteTaleAsync(Guid taleId, Guid userId)
    {
        // Fetch the tale and verify ownership
        var tale = await _dbContext.Tales.FindAsync(taleId);
        if (tale is null || tale.AuthorId != userId)
        {
            return false;
        }

        // Check if this tale has children in Neo4j
        var childrenResults = await _graphClient.Cypher
            .Match("(t:Tale {id: $id})-[:LEADS_TO]->(child:Tale)")
            .WithParam("id", taleId.ToString())
            .Return(child => child.Count())
            .ResultsAsync;

        var hasChildren = childrenResults.FirstOrDefault() > 0;

        if (hasChildren)
        {
            // Scenario B: Soft delete - preserve structure for children
            tale.IsDeleted = true;
            tale.Content = "[This chapter has been deleted by the author]";
            tale.AuthorName = "Anonymous";
            await _dbContext.SaveChangesAsync();
        }
        else
        {
            // Scenario A: Hard delete - remove from both SQL and Neo4j

            // Remove from Neo4j (including incoming relationship)
            await _graphClient.Cypher
                .Match("(t:Tale {id: $id})")
                .WithParam("id", taleId.ToString())
                .DetachDelete("t")
                .ExecuteWithoutResultsAsync();

            // Remove votes associated with this tale
            var votes = await _dbContext.Votes
                .Where(v => v.TaleId == taleId)
                .ToListAsync();
            _dbContext.Votes.RemoveRange(votes);

            // Remove from PostgreSQL
            _dbContext.Tales.Remove(tale);
            await _dbContext.SaveChangesAsync();
        }

        return true;
    }

    /// <summary>
    /// Finds the root tale ID for a given tale by traversing up the Neo4j graph.
    /// </summary>
    private async Task<Guid?> FindRootTaleIdAsync(Guid taleId)
    {
        // First check if this tale is already a root
        var isRootResults = await _graphClient.Cypher
            .Match("(t:Tale {id: $id})")
            .WithParam("id", taleId.ToString())
            .Return(t => t.As<TaleNodeDto>())
            .ResultsAsync;

        var currentNode = isRootResults.FirstOrDefault();
        if (currentNode?.Type == "ROOT")
        {
            return taleId;
        }

        // Traverse up to find the root
        var rootResults = await _graphClient.Cypher
            .Match("(root:Tale {type: 'ROOT'})-[:LEADS_TO*]->(current:Tale {id: $id})")
            .WithParam("id", taleId.ToString())
            .Return(root => root.As<TaleNodeDto>().Id)
            .ResultsAsync;

        var rootIdString = rootResults.FirstOrDefault();
        return rootIdString != null ? Guid.Parse(rootIdString) : null;
    }

    /// <summary>
    /// Updates the root tale's SeriesVotes and LastActivityAt when a vote occurs.
    /// </summary>
    private async Task UpdateRootSeriesVotesAsync(Guid taleId, DateTime activityTime)
    {
        var rootId = await FindRootTaleIdAsync(taleId);
        if (rootId is null)
        {
            return;
        }

        var rootTale = await _dbContext.Tales.FindAsync(rootId.Value);
        if (rootTale is not null)
        {
            rootTale.SeriesVotes += 1;
            rootTale.LastActivityAt = activityTime;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Updates the root tale's LastActivityAt when a new branch is created.
    /// </summary>
    private async Task UpdateRootLastActivityAsync(Guid childTaleId, DateTime activityTime)
    {
        var rootId = await FindRootTaleIdAsync(childTaleId);
        if (rootId is null)
        {
            return;
        }

        var rootTale = await _dbContext.Tales.FindAsync(rootId.Value);
        if (rootTale is not null)
        {
            rootTale.LastActivityAt = activityTime;
            await _dbContext.SaveChangesAsync();
        }
    }
}
