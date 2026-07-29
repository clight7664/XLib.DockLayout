# Contributing to XLib.DockLayout

## Prerequisites

- .NET SDK version declared in `global.json`.
- Git 2.40 or later.
- Node.js only for JavaScript syntax checks; the repository has no npm runtime dependency.
- Playwright browser binaries for E2E execution.

## Branches

- `main` must remain releasable.
- Use short-lived branches such as `feat/tab-keyboard-navigation` or `fix/split-tree-prune`.
- Do not commit directly to protected `main` once branch protection is enabled.

## Commit format

Use Conventional Commits. Examples:

```text
fix(splitter): prevent collapse button from starting pointer drag
feat(tabs): add roving keyboard activation
ci(pages): deploy standalone wasm playground
```

A commit must not mix formatting-only changes with functional behavior unless required by `dotnet format`.

## Pull request acceptance

A pull request should include:

1. Problem statement and affected requirement IDs.
2. Design impact, especially object semantics and C#/JS authority.
3. Tests added or updated.
4. Commands executed and their exact result.
5. Screenshot or E2E evidence for visible UI changes.
6. Known limitations and migration impact.

Required checks:

```bash
dotnet restore XLib.DockLayout.sln
dotnet format XLib.DockLayout.sln --verify-no-changes
dotnet build XLib.DockLayout.sln -c Release --no-restore
dotnet test XLib.DockLayout.sln -c Release --no-build
python3 scripts/static-verify.py
node --check src/XLib.DockLayout/wwwroot/js/xlib-dock-layout.js
```

## Dependency policy

- Runtime RCL may reference `Microsoft.AspNetCore.Components.Web` only unless the architecture baseline is formally changed.
- Do not add third-party docking, layout, drag-drop or complete UI frameworks.
- Test-only dependencies must use Central Package Management and `PrivateAssets` where appropriate.
- Update `THIRD-PARTY-NOTICES.md` and `SBOM.spdx.json` for dependency changes.

## Public API policy

- Add XML Documentation to every public member.
- Prefer additive changes during `0.x`; document breaking changes explicitly.
- Preserve XML layout major-version compatibility.
- Keep `Region`, `DockPanel`, `EditorView`, `EditorTab` and `ToolBarEntry` separate.

## Security

Never commit API keys, NuGet keys, GitHub tokens, connection strings or private URLs. GitHub Actions uses repository secrets such as `NUGET_API_KEY`.
