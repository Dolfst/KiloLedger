# Copilot instructions

Follow the repository-wide rules in [`AGENTS.md`](../AGENTS.md). Use the matching skill in `.github/skills/` for cross-platform changes, sync/data work, CI, or releases.

Before editing, inspect the worktree and the relevant implementation on every affected platform. Preserve unrelated changes, keep the .NET Compact Framework 3.5 client compatible, run focused verification, and state any remaining limitation explicitly.

## Autonomous improvement and release cycle

For a repository improvement requested by the user, proceed without routine confirmation:

1. Use [`docs/competitive-analysis.md`](../../docs/competitive-analysis.md) as the baseline for vehicle-expense features; do not repeat the external market analysis for ordinary iterations.
2. Select the first unfinished, measurable roadmap item and inspect the implementation on every affected client.
3. Make a focused cross-platform change, add smoke coverage, run existing checks and available builds.
4. Commit and push normally to `main`; do not rewrite shared history or force-push.
5. If the change is a release candidate, update `VERSION` and every platform/package metadata field, release documentation and `ROADMAP.md`, then publish the three platform artifacts through the existing GitHub Actions workflows.
6. Monitor the workflows and verify `dist/download` plus the rolling Android, Desktop and KPK releases. Report the final version, commits, workflow results and remaining roadmap items.
