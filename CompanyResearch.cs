using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MimeKit;
using SerpApi;

static class CompanyResearch
{
    // Kişisel e-posta adreslerinde firma araştırması yapmıyoruz
    static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "hotmail.com", "outlook.com", "live.com",
        "yahoo.com", "icloud.com", "yandex.com", "proton.me", "protonmail.com"
    };

    public static string? GetCompanyDomain(MimeMessage message)
    {
        var address = message.From.Mailboxes.FirstOrDefault()?.Address;
        var host = address?.Split('@').LastOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(host)) return null;

        var domain = ToBaseDomain(host);
        return FreeMailDomains.Contains(domain) ? null : domain;
    }

    // trx.mail2.disneyplus.com -> disneyplus.com, info.firma.com.tr -> firma.com.tr
    static string ToBaseDomain(string host)
    {
        var parts = host.Split('.');
        if (parts.Length <= 2) return host;

        string[] secondLevel = { "com", "co", "org", "net", "gov", "edu", "ac", "gen", "web", "bel", "k12" };
        if (parts[^1].Length == 2 && secondLevel.Contains(parts[^2]))
            return string.Join('.', parts[^3..]);

        return string.Join('.', parts[^2..]);
    }

    public static async Task<string> SearchAsync(SerpApiClient serp, string domain)
    {
        var parameters = new Dictionary<string, string>
        {
            ["engine"] = "google_light",
            ["q"] = domain,
            ["hl"] = "en"
        };

        using var results = await serp.SearchAsync(parameters);
        var sb = new StringBuilder();

        if (results.OrganicResults is { } organic)
        {
            foreach (var r in organic.EnumerateArray().Take(5))
            {
                var title = r.TryGetProperty("title", out var t) ? t.GetString() : "";
                var snippet = r.TryGetProperty("snippet", out var s) ? s.GetString() : "";
                sb.AppendLine($"- {title}: {snippet}");
            }
        }
        return sb.ToString();
    }

    public static async Task<string?> SummarizeAsync(HttpClient ollama, string domain, string searchResults)
    {
        if (string.IsNullOrWhiteSpace(searchResults)) return null;

        var system = """
            Bir şirketin ne iş yaptığını 2 kısa cümleyle özetle.
            Kurallar:
            - SADECE verilen arama sonuçlarındaki bilgileri kullan.
            - Arama sonuçlarında açıkça yazmayan hiçbir iddiada bulunma (dolandırıcılık, phishing, şikâyet vb.).
            - Sonuçlar yetersizse sadece "Arama sonuçları yeterli bilgi içermiyor." yaz.
            - Yalnızca Türkçe yaz; başka dil veya alfabe kullanma.
            """;

        var request = new
        {
            model = "qwen2.5:7b",
            stream = false,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = $"Alan adı: {domain}\n\nArama sonuçları:\n{searchResults}" }
            }
        };

        var response = await ollama.PostAsJsonAsync("/api/chat", request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("message").GetProperty("content").GetString();
    }
}