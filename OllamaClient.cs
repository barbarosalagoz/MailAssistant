using System.Net.Http.Json;
using System.Text.Json;

namespace MailAssistant;

public sealed class OllamaClient(HttpClient http)
{
    public const string Model = "qwen2.5:7b";

    public async Task<string> ChatAsync(string system, string user, bool json = false, CancellationToken ct = default)
    {
        object request = json
            ? new { model = Model, stream = false, format = "json", messages = Messages(system, user) }
            : new { model = Model, stream = false, messages = Messages(system, user) };

        var response = await http.PostAsJsonAsync("/api/chat", request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return body.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    static object[] Messages(string system, string user) =>
    [
        new { role = "system", content = system },
        new { role = "user", content = user }
    ];
}