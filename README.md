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

```powershell
$env:MAIL_USER = "you@example.com"
$env:MAIL_APP_PASSWORD = "your-app-password"
$env:SERPAPI_KEY = "your-serpapi-key"
dotnet run
```

## Lessons learned

[Write in your own words: subdomain search caused a hallucinated "phishing" claim; the model drifted into Chinese; notifications were misclassified; how each was fixed.]

## Demo

[Video link]
