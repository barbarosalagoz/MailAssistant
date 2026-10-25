using MailAssistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SerpApi;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// SerpApi: IHttpClientFactory ile yönetilen typed client
builder.Services.AddSerpApi(options =>
{
    options.ApiKey = builder.Configuration["SERPAPI_KEY"]
        ?? throw new InvalidOperationException("SERPAPI_KEY tanımlı değil.");
    options.Timeout = TimeSpan.FromSeconds(30);
});

// Yerel model (Ollama) için typed HttpClient
builder.Services.AddHttpClient<OllamaClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["OLLAMA_URL"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(3);
});

builder.Services.AddTransient<CompanyResearcher>();
builder.Services.AddTransient<InboxTriage>();

using var host = builder.Build();

// Başlamadan önce SerpApi hesabını kontrol et (Account API arama hakkı harcamaz)
var serp = host.Services.GetRequiredService<SerpApiClient>();
using (var account = await serp.AccountAsync())
{
    Console.WriteLine($"SerpApi: {account["total_searches_left"]} arama hakkı kaldı.");
}

await host.Services.GetRequiredService<InboxTriage>().RunAsync();