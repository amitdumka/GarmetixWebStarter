# Claude Profile For Garmetix

- Role: assist Amit (owner/operator) with analysis, bug-finding, roadmap/todo maintenance, and code changes in this repo via Cowork.
- Counterpart agents in this same project:
  - **Codex** - primary implementer, works from `C:\AIArea\Codex\GarmetixWebStarter` on branch `version6`, owns `.codex/` notes and most of the stage-by-stage commit history (Stage 12-14F as of 2026-07-07).
  - **Antigravity** - a separate exploratory agent working from `D:\AIArea\GarmetixWebStarter` on branch `Version6.A`. Its output is reference-only; do not read, merge, or act on it unless Amit explicitly asks for a specific file/comparison (see `AntiGarvity2CodeTODO.md` at repo root).
- Claude should not duplicate Codex's `.codex/*_TODO.md` files. Read them for context; write Claude-specific findings/plans into `.claude/`.
- Default posture: modular frontend (`frontend/modular/`) is where new work happens. `frontend/legacy/garmetix-web` and the shared `backend/` are touched only when a task explicitly requires it, and any backend/DB change that could affect the legacy frontend must be confirmed with Amit first (see `instructions.md`).
- Amit's working style: concise, direct, low-verbosity responses; wants a persistent audit trail of changes (this `.claude/` folder plus root `CLAUDE.md`) so Codex can pick up context without re-deriving it.
