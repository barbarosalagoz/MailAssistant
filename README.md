# MailAssistant

An AI email assistant in .NET that triages your inbox, drafts replies with a local LLM,
and researches the sender's company with [SerpApi](https://serpapi.com).

> Drafts are never sent automatically. A human always reviews them.

## What it does

1. Reads unread emails from the last 2 days over IMAP (read-only, nothing is marked or deleted).
2. Skips newsletters and automated notifications (`List-Unsubscribe` header and sender heuristics).
3. Classifies each remaining email and drafts a reply with a local model via [Ollama](https://ollama.com).
4. Looks up the sender's company with the official `serpapi` .NET package and summarises it.

## Tech

- .NET, C#
- [MailKit](https://github.com/jstedfast/MailKit) for IMAP
- [`serpapi`](https://www.nuget.org/packages/serpapi) .NET SDK (`google_light` engine)
- Ollama with `qwen2.5:7b` running locally: email content never leaves your machine for the LLM step

## Run it

Prerequisites: .NET SDK, Ollama (`ollama pull qwen2.5:7b`), a Gmail app password, a SerpApi API key (free plan works).

Store your secrets with .NET User Secrets (kept outside the repo, never committed):

```powershell
dotnet user-secrets init
dotnet user-secrets set "MAIL_USER" "you@example.com"
dotnet user-secrets set "MAIL_APP_PASSWORD" "your-app-password"
dotnet user-secrets set "SERPAPI_KEY" "your-serpapi-key"
dotnet run
```

## Lessons learned

- **The model invented a phishing scam.** A Disney+ payment email came from `trx.mail2.disneyplus.com`. I searched that exact subdomain, got almost nothing back, and the model decided Disney+ was a phishing operation. Now I search the base domain (`disneyplus.com`), and the prompt says: if the search results don't say it, don't say it.
- **Halfway through a summary, it switched to Chinese.** I'm running `qwen2.5:7b` locally, and one company summary started in Turkish and ended in Chinese. Adding "Turkish only, no other alphabet" to the prompt fixed it, but it was a good reminder that small models need very explicit rules.
- **Most of my inbox didn't need AI at all.** Out of 53 unread emails, almost all were newsletters, payment notices and GitHub alerts. Checking the `List-Unsubscribe` header and a few sender patterns first means the model only sees the emails that matter, and I'm not spending SerpApi searches on Discord receipts.

## Security

Mapped to the [OWASP Top 10 for LLM Applications](https://genai.owasp.org/llm-top-10/):

| Risk | Mitigation |
| --- | --- |
| Prompt injection (LLM01) | Email content is wrapped in `<email>` tags and treated as untrusted data; links in drafts are stripped in code, not just by prompt |
| Sensitive information disclosure (LLM02) | The LLM runs locally; only the sender's domain is sent to SerpApi; secrets live in User Secrets |
| Excessive agency (LLM06) | Mailbox is opened read-only; nothing is sent, marked or deleted; a human reviews every draft |
| Misinformation (LLM09) | Company summaries may only use facts from the search results; otherwise the model says the results are insufficient |

Dependencies are checked with `dotnet list package --vulnerable` and Dependabot.

## Demo

[Video link]
