@AGENTS.md

## Claude Code

The repository guidance lives in [AGENTS.md](AGENTS.md) so every coding agent reads the same rules; add to it, not to this file. Directory-level `AGENTS.md` files are reached through a sibling `CLAUDE.md` containing `@AGENTS.md`, so give any new one that companion.

Skills for working on this repository live in `.agents/skills/`, which Claude Code does not load on its own. When a task matches one — `wayd-testing` for writing or reviewing tests, `vercel-react-best-practices` for React performance — read its `SKILL.md` first.
