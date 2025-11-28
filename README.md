# Alpha Vantage Intraday Processor

A self-hosted .NET 8 Web API that consumes the Alpha Vantage API and exposes an endpoint to get intraday stock data aggregated by day.

**Note:** This implementation automatically detects your API key capabilities. It attempts to use premium features (full month data) first, and gracefully falls back to free tier (last 100 data points, ~6-7 days) if premium is not available.

## Features

- Fetches intraday stock data (15-minute intervals) from Alpha Vantage API
- Aggregates data by day (last ~6-7 trading days with free tier)
- Calculates daily averages for low and high prices
- Sums daily trading volume
- Returns JSON response in the specified format

**Limitation:** Free tier provides last 100 data points (~6-7 days), not a full month. For full month data, upgrade to premium at https://www.alphavantage.co/premium/

## Prerequisites

- .NET 8 SDK or later
- Alpha Vantage API key (get one free at https://www.alphavantage.co/support/#api-key)

## Setup

1. **Get your Alpha Vantage API Key**
   - Visit https://www.alphavantage.co/support/#api-key
   - Sign up for a free API key or obtain premium API key
   - Note: free API key has limitations (notably, a smaller output size)

2. **Configure the API Key**
   
   **Recommended: Use .NET User Secrets**
   ```bash
   dotnet user-secrets set "AlphaVantage:ApiKey" "your-actual-api-key-here"
   ```
   
   **Alternative: Edit appsettings.json directly**
   - Open `appsettings.json`
   - Replace `YOUR_API_KEY_HERE` with your actual API key

3. **Run the application**
   ```bash
   dotnet run
   ```
   
   Note: `dotnet run` automatically restores dependencies and builds the project.

   The API will be available at:
   - HTTP: `http://localhost:5031` (or check the console output for the actual port)
   - Swagger UI: `http://localhost:5031/swagger` (in development mode)

## API Endpoint

### GET `/api/intraday/{symbol}`

Retrieves intraday data aggregated by day. With free tier API keys, this returns data for the last ~6-7 trading days (100 data points at 15-minute intervals).

**Parameters:**
- `symbol` (path parameter): Stock ticker symbol (e.g., `IBM`, `AAPL`, `MSFT`)

**Response:**
```json
[
  {
    "day": "2024-10-15",
    "lowAverage": 40.2958,
    "highAverage": 49.7534,
    "volume": 49073348
  },
  {
    "day": "2024-10-16",
    "lowAverage": 41.1234,
    "highAverage": 50.5678,
    "volume": 52345678
  }
]
```

**Example Request:**
```bash
curl http://localhost:5031/api/intraday/IBM
```

## Project Structure

```
IntradayTransformer/
├── Controllers/
│   └── IntradayController.cs      # API endpoint controller
├── Models/
│   ├── DayAggregate.cs            # Response model
│   └── AlphaVantageResponse.cs    # Alpha Vantage API response model
├── Services/
│   └── AlphaVantageService.cs     # Service to fetch and process data
├── appsettings.json               # Configuration (add your API key here)
└── Program.cs                     # Application entry point
```

## How It Works

1. The API receives a stock symbol (e.g., `IBM`)
2. **Auto-detection:** First attempts to use premium mode:
   - `function=TIME_SERIES_INTRADAY`
   - `interval=15min`
   - `month=YYYY-MM` (last month)
   - `outputsize=full` (premium feature)
3. **Automatic fallback:** If premium is not available, automatically falls back to free tier:
   - `function=TIME_SERIES_INTRADAY`
   - `interval=15min`
   - `outputsize=compact` (last 100 data points)
4. Groups all 15-minute intervals by day
5. For each day, calculates:
   - `lowAverage`: Average of all "low" prices
   - `highAverage`: Average of all "high" prices
   - `volume`: Sum of all volumes
6. Returns the aggregated data sorted by day

**Note:** The API automatically detects if your API key supports premium features. If you have a premium key, it will use full month data. Otherwise, it gracefully falls back to free tier (last 100 data points) with a warning logged.

## Notes

- The API assumes data is updated every 15 minutes
- Free Alpha Vantage API keys have rate limits (5 API calls per minute, 500 per day)
- **Free tier limitation:** Returns last 100 data points (~6-7 trading days), not a full month
- For full month historical data, upgrade to premium at https://www.alphavantage.co/premium/
- The original requirement specified "last month" but free tier doesn't support `outputsize=full` for historical months

## Error Handling

The API returns appropriate HTTP status codes:
- `200 OK`: Success
- `400 Bad Request`: Invalid symbol or API configuration error
- `500 Internal Server Error`: Server error
