# M1 Chat

Blazor-based multi-provider chat application with Google sign-in, persistent chat history, file attachments, and streamed model responses.

## Requirements

- .NET SDK 9
- Google OAuth credentials
- At least one provider API key

## Local setup

1. Copy `m1Chat/m1Chat/.secrets.example` to `m1Chat/m1Chat/.secrets`.
2. Fill in the Google credentials and whichever model provider keys you want to use.
3. Restore and run:

```bash
dotnet restore m1Chat.sln
dotnet run --project m1Chat/m1Chat
```

The app defaults to a local SQLite database at `m1Chat/m1Chat/chat.db` and local uploads in `m1Chat/m1Chat/uploads`.

## Secrets

Secrets are not stored in tracked config files. The server reads a JSON `.secrets` file either:

- from `m1Chat/m1Chat/.secrets`, or
- from the path in `M1CHAT_SECRETS_FILE`

## Notes

- Production deployment details in this repo are intentionally parameterized; host-specific secrets and paths belong in the server-side `.secrets` file and deployment environment.
- Uploaded files are private to the authenticated user who uploaded them.
- Google sign-in is optional in development; if `Google:ClientId` and `Google:ClientSecret` are absent, the Google login endpoint returns `503` instead of silently failing.
