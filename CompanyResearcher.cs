using System.Text;
using MimeKit;
using SerpApi;

namespace MailAssistant;

public sealed class CompanyResearcher(SerpApiClient serp, OllamaClient ollama)
{
    readonly Dictionary<string, string> _cache = new();

    // No company research for personal email addresses
    static readonly HashSet<string> FreeMailDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "hotmail.com", "outlook.com", "live.com",
        "yahoo.com", "icloud.com", "yandex.com", "proton.me", "protonmail.com"
    };

    const string SummaryPrompt = """
        Summarize what a company does in 2 short sentences.
        Rules:
        - Use ONLY the information in the search results provided.
        - Make no claim the search results don't explicitly state (fraud, phishing, complaints, etc.).
        - If the results are insufficient, write only "The search results don't contain enough information."
        - Write in English only; never use another language or alphabet.
        """;

    public static string? GetCompanyDomain(MimeMessage message)
    {
        var address = message.From.Mailboxes.FirstOrDefault()?.Address;
        var host = address?.Split('@').LastOrDefault()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(host)) return null;

        var domain = ToBaseDomain(host);
        return FreeMailDomains.Contains(domain) ? null : domain;
    }

    // trx.mail2.disneyplus.com -> disneyplus.com, info.company.com.tr -> company.com.tr
    static string ToBaseDomain(string host)
    {
        var parts = host.Split('.');
        if (parts.Length <= 2) return host;

        string[] secondLevel = { "com", "co", "org", "net", "gov", "edu", "ac", "gen", "web", "bel", "k12" };
        if (parts[^1].Length == 2 && secondLevel.Contains(parts[^2]))
            return string.Join('.', parts[^3..]);

        return string.Join('.', parts[^2..]);
    }

    public async Task<string> ResearchAsync(string domain, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(domain, out var cached)) return cached;

        string summary;
        try
        {
            var found = await SearchAsync(domain, ct);
            summary = string.IsNullOrWhiteSpace(found)
                ? "No search results found."
                : await ollama.ChatAsync(SummaryPrompt, $"Domain: {domain}\n\nSearch results:\n{found}", ct: ct);
        }
        catch (SerpApiKeyException)
        {
            summary = "(Invalid SerpApi key. Check SERPAPI_KEY.)";
        }
        catch (SerpApiHttpException ex) when (ex.StatusCode == 429)
        {
            summary = "(SerpApi quota or rate limit reached.)";
        }
        catch (SerpApiHttpException ex)
        {
            summary = $"(SerpApi HTTP {ex.StatusCode} error: {ex.Message})";
        }
        catch (SerpApiTimeoutException)
        {
            summary = "(SerpApi request timed out.)";
        }
        catch (SerpApiException ex)
        {
            summary = $"(Company research failed: {ex.Message})";
        }

        _cache[domain] = summary;
        return summary;
    }

    async Task<string> SearchAsync(string domain, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>
        {
            ["engine"] = "google_light",
            ["q"] = domain,
            ["hl"] = "en"
        };

        using var results = await serp.SearchAsync(parameters, ct);
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
}