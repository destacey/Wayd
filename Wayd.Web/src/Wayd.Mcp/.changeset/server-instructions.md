---
"@wayd/mcp": patch
---

The server now sends instructions when a client connects, so clients that don't load the Wayd skills — Claude Desktop, Cursor, ChatGPT and claude.ai connectors — still learn the rules that apply to every tool: updates overwrite the whole record and an omitted or empty role list removes everyone in that role, which parameters accept a key and which take only a UUID, and that status changes and deletes need the user's confirmation.
