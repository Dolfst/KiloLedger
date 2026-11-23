# Instructions for AI agents

## Project map

- Read `README.md`, `ROADMAP.md`, and `VERSION` before making project-wide or release changes.
- The application brand is **KiloLedger** (Android `ApplicationId`: `com.kiloledger.app`). The three clients live in `KiloLedger/KiloLedger` (Windows Mobile, .NET Compact Framework 3.5), `KiloLedger.Android` (.NET 10 Android), and `KiloLedger.Desktop` (.NET Framework 4.8).
- Similar domain logic is intentionally implemented in more than one client. Keep behavior and file formats consistent across all affected clients.
- `build/verify_*.py` contains offline smoke checks. `build/run_verifies.py` runs the full set.

## Working rules

- Inspect `git status` first. Preserve unrelated user changes and do not rewrite shared history.
- Optimize for low context and credit usage:
  - Start by reading only `git status`, the latest commits and the smallest relevant section of `ROADMAP.md` or the implementation.
  - Do not reread `README.md`, `AGENTS.md`, full diffs or the conversation history unless the task explicitly requires it.
  - Batch independent searches and file reads; do not repeat the same search or status check after it has already produced a result.
  - Use subagents only for genuinely independent work that needs separate context. One narrow implementation or review task stays with the main agent.
  - Run only focused smoke/build checks first; run the full suite only before finalizing or publication.
  - End with a short handoff: completed work, commit(s), checks and remaining item. Do not repeat long tool output or the prior conversation.
  - Save only durable decisions in commits or repository instructions; do not create session logs or checkpoints that repeat the whole dialogue.
- Commit messages must be bilingual: include Russian and English text, for example `Исправить проверку импорта / Fix import validation`.
- Make small, focused changes. Read and follow existing platform-specific patterns.
- For behavior or data-format changes, update every affected client and add a focused regression check.
- Preserve compatibility with Compact Framework 3.5 in `KiloLedger/KiloLedger`; the `verify_cf_apis.py` check guards supported APIs.
- Do not commit credentials, signing passwords, private keys, or generated local secrets.
- Before finishing, run the smallest relevant checks and report what passed and what could not run.

## Relevant skills

- `.github/skills/kiloledger-cross-platform/SKILL.md` — feature and bug-fix work across clients.
- `.github/skills/kiloledger-sync-data/SKILL.md` — data formats, stable IDs, sync, and tombstones.
- `.github/skills/kiloledger-ci/SKILL.md` — builds, tests, and GitHub Actions.
- `.github/skills/kiloledger-release/SKILL.md` — version changes and staged GitHub publication.
