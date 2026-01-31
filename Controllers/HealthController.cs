using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Neo4jClient;
using VoTales.API.Data;

namespace VoTales.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IGraphClient _graphClient;

    public HealthController(AppDbContext dbContext, IGraphClient graphClient)
    {
        _dbContext = dbContext;
        _graphClient = graphClient;
    }

    [HttpGet("heartbeat")]
    [AllowAnonymous]
    public async Task<IActionResult> Heartbeat()
    {
        var postgresHealthy = false;
        var neo4jHealthy = false;

        // Postgres Check
        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
            postgresHealthy = true;
        }
        catch
        {
            // Postgres is not healthy
        }

        // Neo4j Check
        try
        {
            await _graphClient.Cypher
                .Match("(n)")
                .Return(n => n.Count())
                .Limit(1)
                .ResultsAsync;
            neo4jHealthy = true;
        }
        catch
        {
            // Neo4j is not healthy
        }

        var response = new
        {
            status = postgresHealthy && neo4jHealthy ? "Alive" : "Degraded",
            postgres = postgresHealthy,
            neo4j = neo4jHealthy,
            timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }
}
