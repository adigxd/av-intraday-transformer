using IntradayTransformer.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register HTTP client for Alpha Vantage API
builder.Services.AddHttpClient<IAlphaVantageService, AlphaVantageService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register the service
builder.Services.AddScoped<IAlphaVantageService, AlphaVantageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
