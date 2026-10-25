using MailAssistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SerpApi;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// SerpApi: typed client managed by IHttpClientFactory
builder.Services.AddSerpApi(options =>
{
    options.ApiKey = builder.Configuration["SERPAPI_KEY"]
        ?? throw new InvalidOperationException("SERPAPI_KEY is not set.");
    options.Timeout = TimeSpan.FromSeconds(30);
});

// Typed HttpClient for the local model (Ollama)
builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddTransient<CompanyResearcher>();
builder.Services.AddTransient<InboxTriage>();

using var host = builder.Build();

// Check the SerpApi account first (the Account API does not use a search credit)
var serp = host.Services.GetRequiredService<SerpApiClient>();
using (var account = await serp.AccountAsync())
{
    Console.WriteLine($"SerpApi: {account["total_searches_left"]} searches left.");
}

await host.Services.GetRequiredService<InboxTriage>().RunAsync();