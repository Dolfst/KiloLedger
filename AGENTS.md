# Instructions for AI agents

## Project map

- Read `README.md`, `ROADMAP.md`, and `VERSION` before making project-wide or release changes.
- The application has three clients: `Alfa156/Alfa156` (Windows Mobile, .NET Compact Framework 3.5), `Alfa156.Android` (.NET 10 Android), and `Alfa156.Desktop` (.NET Framework 4.8).
- Similar domain logic is intentionally implemented in more than one client. Keep behavior and file formats consistent across all affected clients.
- `build/verify_*.py` contains offline smoke checks. `build/run_verifies.py` runs the full set.

## Working rules

- Inspect `git status` first. Preserve unrelated user changes and do not rewrite shared history.
- Make small, focused changes. Read and follow existing platform-specific patterns.
- For behavior or data-format changes, update every affected client and add a focused regression check.
- Preserve compatibility with Compact Framework 3.5 in `Alfa156/Alfa156`; the `verify_cf_apis.py` check guards supported APIs.
- Do not commit credentials, signing passwords, private keys, or generated local secrets.
- Before finishing, run the smallest relevant checks and report what passed and what could not run.

## Relevant skills

- `.github/skills/alfa-cross-platform/SKILL.md` — feature and bug-fix work across clients.
- `.github/skills/alfa-sync-data/SKILL.md` — data formats, stable IDs, sync, and tombstones.
- `.github/skills/alfa-ci/SKILL.md` — builds, tests, and GitHub Actions.
- `.github/skills/alfa-release/SKILL.md` — version changes and staged GitHub publication.
