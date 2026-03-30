# Claude Usage Bar

A Windows desktop application that displays an always-on-top info bar with Claude API usage stats.

Features:
- Always-on-top borderless info bar
- 5-hour session usage, 7-day usage, cost/extra usage, model distribution
- Burn rate and session-out time predictions
- Peak vs standard rate indicator (US Eastern business hours)
- Auto-refreshes on a configurable interval (default 2 minutes)
- Dark theme UI
<img width="1193" height="111" alt="image" src="https://github.com/user-attachments/assets/646e9a85-fce6-4f66-86d8-0923004a169b" />

## Requirements

- .NET 8.0 Windows runtime
- **Claude Code CLI** installed and authenticated (provides OAuth credentials)

## Authentication

This app does **not** use an Anthropic API key or a session cookie. It authenticates using the same OAuth credentials that the Claude Code CLI stores locally after you log in.

### How to authenticate

1. Install the Claude Code CLI:
   ```
   npm install -g @anthropic-ai/claude-code
   ```
2. Log in:
   ```
   claude
   ```
   Follow the browser-based OAuth flow. Once complete, credentials are saved automatically.

3. Launch Claude Usage Bar — it will find and use the credentials with no further setup.

### Where credentials are stored

The CLI saves OAuth credentials to:

| Environment | Path |
|-------------|------|
| Windows (native) | `%USERPROFILE%\.claude\.credentials.json` |
| WSL (Debian/Ubuntu) | `\\wsl.localhost\<distro>\home\<user>\.claude\.credentials.json` |

The app searches Windows native first, then common WSL distros. If multiple credential files exist, it picks the most recently modified one.

The credentials file contains an access token and a refresh token. The app **automatically refreshes the access token** when it is within 5 minutes of expiry, writing the new token back to the same file.

## Setup

```
dotnet build
dotnet run
```

Or open `ClaudeUsageBar.sln` in Visual Studio and press F5.

## Configuration

Click the **⚙** button to open the settings panel where you can:
- See whether OAuth credentials were detected and which file is being used
- Adjust the refresh interval (1–60 minutes)

## Metrics displayed

| Metric | Source |
|--------|--------|
| Cost Usage | `extra_usage` — monthly add-on spend vs limit |
| 5h Session | `five_hour.utilization` — rolling 5-hour token window |
| 7-Day Usage | `seven_day.utilization` — rolling 7-day token window |
| Time to Reset | `five_hour.resets_at` |
| Model Distribution | `seven_day_sonnet` vs derived opus share |
| Burn Rate | Delta between successive readings (% pts/min) |
| Cost Rate | Delta in USD/min |
| Session Out | Estimated time until 5h session exhausted at current burn rate |
