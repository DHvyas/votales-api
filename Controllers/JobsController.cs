using Microsoft.AspNetCore.Mvc;
using Neo4jClient;

namespace VoTales.API.Controllers;

[ApiController]
[Route("[controller]")]
public class JobsController : ControllerBase
{
    private readonly IGraphClient _graphClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IGraphClient graphClient, IConfiguration configuration, ILogger<JobsController> logger)
    {
        _graphClient = graphClient;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("update-trending")]
    public async Task<IActionResult> UpdateTrending()
    {
        var expectedSecret = _configuration["JobSecret"];

        if (string.IsNullOrEmpty(expectedSecret))
        {
            _logger.LogError("JobSecret is not configured");
            return StatusCode(500, "Job secret not configured");
        }

        var providedSecret = Request.Headers["X-Job-Secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedSecret) || providedSecret != expectedSecret)
        {
            _logger.LogWarning("Unauthorized attempt to access update-trending endpoint");
            return Unauthorized("Invalid or missing X-Job-Secret header");
        }

        try
        {
            await _graphClient.Cypher
                .Match("(t:Tale)")
                .Where("t.IsRoot = true")
                .With("t, duration.inSeconds(t.CreatedAt, datetime()).seconds / 3600.0 AS ageInHours")
                .With("t, (t.SeriesVotes) / ((ageInHours + 2)^1.8) AS newScore")
                .Set("t.TrendingScore = newScore")
                .ExecuteWithoutResultsAsync();

            _logger.LogInformation("Successfully updated trending scores for all root tales");
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update trending scores");
            throw;
        }
    }
}
