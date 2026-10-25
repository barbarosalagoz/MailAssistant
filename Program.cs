using System.Net.Http.Json;
using System.Text.Json;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using SerpApi;

var http = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(3)
};

var user = Environment.GetEnvironmentVariable("MAIL_USER")
    ?? throw new Exception("MAIL_USER tanımlı değil.");
var password = Environment.GetEnvironmentVariable("MAIL_APP_PASSWORD")
    ?? throw new Exception("MAIL_APP_PASSWORD tanımlı değil.");
var serpKey = Environment.GetEnvironmentVariable("SERPAPI_KEY")
    ?? throw new Exception("SERPAPI_KEY tanımlı değil.");

using var serp = new SerpApiClient(serpKey);
var companyCache = new Dictionary<string, string?>();

using var imap = new ImapClient();
await imap.ConnectAsync("imap.gmail.com", 993, true);
await imap.AuthenticateAsync(user, password);

var inbox = imap.Inbox;
await inbox.OpenAsync(FolderAccess.ReadOnly);

// Son 2 günün okunmamış e-postaları
var query = SearchQuery.NotSeen.And(SearchQuery.DeliveredAfter(DateTime.Today.AddDays(-2)));
var ids = await inbox.SearchAsync(query);
Console.WriteLine($"{ids.Count} okunmamış e-posta bulundu.");

var processed = 0;
foreach (var id in ids.Reverse()) // en yeni e-postadan başla
{
    if (processed >= 5) break;

    var message = await inbox.GetMessageAsync(id);

    // Bülten ve otomatik bildirimleri atla: taslak yazma, firma araştırma
    if (message.Headers.Contains("List-Unsubscribe") || IsAutomatedSender(message))
    {
        Console.WriteLine($"\n[otomatik] {message.Subject} (atlandı)");
        continue;
    }
    processed++;

    var body = message.TextBody ?? message.HtmlBody ?? "";
    if (body.Length > 4000) body = body[..4000];

    var result = await AnalyzeAsync(http, message.From.ToString(), message.Subject ?? "", body);

    Console.WriteLine($"\n[{result?.Category}] {message.Subject}");
    Console.WriteLine($"Gönderen: {message.From}");
    Console.WriteLine($"Özet: {result?.Summary}");

    if (result?.Category is "bildirim" or "reklam" || string.IsNullOrWhiteSpace(result?.DraftReply))
    {
        Console.WriteLine("Taslak: (cevap gerektirmiyor)");
        continue;
    }
    Console.WriteLine($"Taslak:\n{result.DraftReply}");

    // Firma araştırması (SerpApi + yerel model)
    var domain = CompanyResearch.GetCompanyDomain(message);
    if (domain is not null && result.Category != "reklam")
    {
        if (!companyCache.TryGetValue(domain, out var summary))
        {
            try
            {
                var found = await CompanyResearch.SearchAsync(serp, domain);
                summary = await CompanyResearch.SummarizeAsync(http, domain, found);
            }
            catch (SerpApiException ex)
            {
                summary = $"(Şirket araştırması yapılamadı: {ex.Message})";
            }
            companyCache[domain] = summary;
        }
        Console.WriteLine($"Şirket ({domain}): {summary}");
    }
}

await imap.DisconnectAsync(true);

static async Task<Triage?> AnalyzeAsync(HttpClient http, string from, string subject, string body)
{
    var system = """
        Sen bir şirketin e-posta asistanısın. Gelen e-postayı incele ve SADECE şu JSON'u döndür:
        {"category": "teklif_talebi | sikayet | fatura | toplanti | bildirim | reklam | diger",
         "summary": "tek cümlelik özet",
         "draftReply": "Türkçe, kibar ve kısa bir yanıt taslağı"}
        Kurallar:
        - Otomatik bilgilendirme, ödeme veya hesap bildirimi gibi cevap gerektirmeyen e-postalarda
          category "bildirim" olsun ve draftReply boş string ("") olsun.
        - Bilmediğin bilgileri (fiyat, tarih, isim) uydurma; [köşeli parantez] içinde boş bırak.
        - Yalnızca Türkçe yaz; başka dil veya alfabe kullanma.
        """;

    var request = new
    {
        model = "qwen2.5:7b",
        stream = false,
        format = "json",
        messages = new object[]
        {
            new { role = "system", content = system },
            new { role = "user", content = $"Gönderen: {from}\nKonu: {subject}\n\n{body}" }
        }
    };

    var response = await http.PostAsJsonAsync("/api/chat", request);
    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    var content = json.GetProperty("message").GetProperty("content").GetString()!;

    return JsonSerializer.Deserialize<Triage>(content,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}

static bool IsAutomatedSender(MimeKit.MimeMessage m)
{
    var address = m.From.Mailboxes.FirstOrDefault()?.Address?.ToLowerInvariant() ?? "";
    var parts = address.Split('@');
    var local = parts[0];
    var host = parts.Length > 1 ? parts[1] : "";

    string[] localMarkers = { "noreply", "no-reply", "do_not_reply", "donotreply", "notifications",
                              "failed-payments", "billing", "newsletter", "marketing" };
    string[] hostLabels = { "mail", "mail2", "trx", "info", "news", "email", "em", "mailer",
                            "notifications", "marketing", "bounce" };

    return localMarkers.Any(local.Contains)
        || host.Split('.').Any(label => hostLabels.Contains(label));
}

record Triage(string Category, string Summary, string DraftReply);