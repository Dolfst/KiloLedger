---
name: kiloledger-ci
description: Modify or debug KiloLedger build scripts, validation, or GitHub Actions. Use when a workflow fails, an action is deprecated, or CI coverage changes.
---

# KiloLedger builds and CI

1. Read the affected workflow in `.github/workflows/` and its local build/verification script before changing it.
2. Keep workflow permissions minimal. Release workflows need `contents: write`; validation workflows should not gain write permissions.
3. Do not remove build or test checks merely to make a workflow green. Fix the underlying cause and surface failures explicitly.
4. Validate workflow YAML and run equivalent local checks where possible:
   - `python3 build/run_verifies.py`
   - `./build/build-kpk.sh Release`
   - Desktop/Android build commands from `AGENTS.md` when relevant.
5. For GitHub runs, inspect the failing step's logs, distinguish warnings from the actual failure, and verify the rerun after pushing a fix.
6. Avoid deleting a release or tag before a replacement is ready. Prefer updating existing release assets safely; verify the release and download assets after publication.
7. Report workflow run results and any remaining runner-only warnings.
