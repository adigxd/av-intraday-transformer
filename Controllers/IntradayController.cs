using Microsoft.AspNetCore.Mvc;
using IntradayTransformer.Models;
using IntradayTransformer.Services;

namespace IntradayTransformer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IntradayController : ControllerBase
{
    private readonly IAlphaVantageService _alphaVantageService;
    private readonly ILogger<IntradayController> _logger;

    public IntradayController(IAlphaVantageService alphaVantageService, ILogger<IntradayController> logger)
    {
        _alphaVantageService = alphaVantageService;
        _logger = logger;
    }

    /// <summary>
    /// Gets intraday data aggregated by day. Attempts to fetch last month's data (premium), falls back to recent data (free tier) if premium not available.
    /// </summary>
    /// <param name="symbol">Stock ticker symbol (e.g., IBM, AAPL)</param>
    /// <returns>List of daily aggregates with lowAverage, highAverage, and volume</returns>
    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(List<DayAggregate>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<DayAggregate>>> GetIntradayData(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return BadRequest("Symbol parameter is required");
        }

        try
        {
            var result = await _alphaVantageService.GetIntradayDataForLastMonthAsync(symbol.ToUpper());
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation for symbol: {Symbol}", symbol);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request for symbol: {Symbol}", symbol);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}

