using System.Text.Json;
using IntradayTransformer.Models;

namespace IntradayTransformer.Services;

public interface IAlphaVantageService
{
    Task<List<DayAggregate>> GetIntradayDataForLastMonthAsync(string symbol);
}

public class AlphaVantageService : IAlphaVantageService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlphaVantageService> _logger;

    public AlphaVantageService(HttpClient httpClient, IConfiguration configuration, ILogger<AlphaVantageService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<DayAggregate>> GetIntradayDataForLastMonthAsync(string symbol)
    {
        var apiKey = _configuration["AlphaVantage:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("Alpha Vantage API key is not configured. Please set AlphaVantage:ApiKey in appsettings.json");
        }

        // Try premium mode first (outputsize=full with month parameter for last month)
        var lastMonth = DateTime.Now.AddMonths(-1).ToString("yyyy-MM");
        var premiumUrl = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval=15min&month={lastMonth}&outputsize=full&apikey={apiKey}";

        _logger.LogInformation("Attempting to fetch intraday data for symbol: {Symbol}, month: {Month} with outputsize=full (premium mode)", symbol, lastMonth);

        try
        {
            return await FetchAndProcessData(premiumUrl, symbol, isPremium: true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("premium", StringComparison.OrdinalIgnoreCase))
        {
            // Premium feature not available, fall back to free tier
            _logger.LogWarning("⚠️ Premium feature not available. Falling back to free tier (compact mode - last 100 data points, ~6-7 days instead of full month)");
            var compactUrl = $"https://www.alphavantage.co/query?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval=15min&outputsize=compact&apikey={apiKey}";
            return await FetchAndProcessData(compactUrl, symbol, isPremium: false);
        }
    }

    private async Task<List<DayAggregate>> FetchAndProcessData(string url, string symbol, bool isPremium)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Alpha Vantage API response received. Length: {Length}", jsonContent.Length);
            
            var alphaVantageResponse = JsonSerializer.Deserialize<AlphaVantageResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (alphaVantageResponse == null)
            {
                _logger.LogError("Failed to deserialize Alpha Vantage response. Raw response: {Response}", jsonContent);
                throw new InvalidOperationException("Failed to deserialize Alpha Vantage response");
            }

            // Check for API errors
            if (!string.IsNullOrEmpty(alphaVantageResponse.ErrorMessage))
            {
                _logger.LogError("Alpha Vantage API error: {Error}", alphaVantageResponse.ErrorMessage);
                throw new InvalidOperationException($"Alpha Vantage API error: {alphaVantageResponse.ErrorMessage}");
            }

            // Check for premium feature requirement
            if (!string.IsNullOrEmpty(alphaVantageResponse.Information) && 
                alphaVantageResponse.Information.Contains("premium", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Alpha Vantage premium feature required: {Info}", alphaVantageResponse.Information);
                throw new InvalidOperationException(
                    $"Alpha Vantage API: {alphaVantageResponse.Information}");
            }

            if (!string.IsNullOrEmpty(alphaVantageResponse.Note))
            {
                _logger.LogWarning("Alpha Vantage API note: {Note}", alphaVantageResponse.Note);
                throw new InvalidOperationException($"Alpha Vantage API note: {alphaVantageResponse.Note}");
            }

            if (alphaVantageResponse.TimeSeries == null || alphaVantageResponse.TimeSeries.Count == 0)
            {
                _logger.LogWarning("No time series data returned for symbol: {Symbol}. Full response: {Response}", 
                    symbol, jsonContent);
                return new List<DayAggregate>();
            }

            // Group by day and calculate aggregates
            var dayGroups = alphaVantageResponse.TimeSeries
                .GroupBy(kvp => ExtractDate(kvp.Key))
                .Select(group => new DayAggregate
                {
                    Day = group.Key,
                    LowAverage = group
                        .Where(kvp => double.TryParse(kvp.Value.Low, out _))
                        .Select(kvp => double.Parse(kvp.Value.Low!))
                        .DefaultIfEmpty(0)
                        .Average(),
                    HighAverage = group
                        .Where(kvp => double.TryParse(kvp.Value.High, out _))
                        .Select(kvp => double.Parse(kvp.Value.High!))
                        .DefaultIfEmpty(0)
                        .Average(),
                    Volume = group
                        .Where(kvp => long.TryParse(kvp.Value.Volume, out _))
                        .Select(kvp => long.Parse(kvp.Value.Volume!))
                        .Sum()
                })
                .OrderBy(d => d.Day)
                .ToList();

            var mode = isPremium ? "premium (full month)" : "free tier (compact - last 100 data points)";
            _logger.LogInformation("✅ Processed {Count} days of data for symbol: {Symbol} using {Mode}", dayGroups.Count, symbol, mode);
            
            return dayGroups;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching data from Alpha Vantage");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Alpha Vantage data");
            throw;
        }
    }

    private static string ExtractDate(string timestamp)
    {
        // Timestamp format: "2024-10-15 09:30:00" or "2024-10-15 09:30"
        // Extract just the date part (YYYY-MM-DD)
        if (DateTime.TryParse(timestamp, out var dateTime))
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        // Fallback: try to extract date from string
        var parts = timestamp.Split(' ');
        return parts.Length > 0 ? parts[0] : timestamp;
    }
}
