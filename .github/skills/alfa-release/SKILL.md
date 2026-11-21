---
name: alfa-release
description: Prepare and publish an Alfa 156 application release or staged project improvements to GitHub. Use when changing the app version, publishing builds, or updating rolling releases.
---

# Alfa 156 staged releases

1. Read `ROADMAP.md`, `README.md`, `VERSION`, and all release workflows before planning a release.
2. For a versioned application release, update `VERSION` and every version field shown in the app or package metadata:
   - `Alfa156.Android/Alfa156.Android.csproj` (`ApplicationDisplayVersion` and `ApplicationVersion`)
   - Android About fallback in `Alfa156.Android/MainActivity.cs`
   - Desktop assembly version and `MainForm.AppVersion`
   - KPK assembly version and About text
   - `README.md` and `dist/download/README.md`
3. Run `python3 build/run_verifies.py`; `build/verify_version_consistency.py` checks the version fields. Build all affected clients before publishing.
4. Update `ROADMAP.md` with what is done and the next measurable step. Do not mark planned work as completed before validation.
5. Review the complete staged diff, check for accidental secrets and generated files, then use a focused commit. Never force-push or rewrite shared history.
6. Push staged improvements to `main` and monitor the affected workflows with `gh run list` / `gh run watch`. Wait for release artifacts and any automated commits to settle; rebase over workflow-generated commits if needed, then push normally.
7. Verify the rolling release and `dist/download` assets. Do not delete an existing release before replacement assets have been built and the replacement operation is ready.
8. State the published version, commit(s), workflow results, and remaining roadmap items.
