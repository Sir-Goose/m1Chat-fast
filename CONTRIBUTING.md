# Contributing

## Development

- Use `.NET SDK 9`.
- Keep secrets in `m1Chat/m1Chat/.secrets` or an external file referenced by `M1CHAT_SECRETS_FILE`.
- Do not commit local databases, uploads, or generated build output.

## Pull requests

- Keep changes scoped.
- Include a short explanation of behavior changes.
- Run `dotnet build m1Chat.sln` before opening a PR.

## Security

- Do not open public issues for vulnerabilities.
- Follow the reporting process in `SECURITY.md`.
