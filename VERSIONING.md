# FlipsiColor Versioning

We follow **Semantic Versioning** (`vMAJOR.MINOR.PATCH`).

## Format

```
vMAJOR.MINOR.PATCH
  │     │     │
  │     │     └── Bug fixes, small corrections
  │     └──────── New features, larger changes
  └────────────── Breaking changes, major milestones
```

## When to bump what?

| Change type | What to bump | Example |
|---|---|---|
| **Major update** (many new features OR a single large feature) | MINOR +1, PATCH = 0 | `v0.5.3` → **`v0.6.0`** |
| **Bug fix / small correction** | only PATCH +1 | `v0.5.0` → **`v0.5.1`** |
| **MINOR reaches 9 + new major update** | MAJOR +1, MINOR = 0, PATCH = 0 | `v0.9.4` → **`v1.0.0`** |
| **PATCH reaches 99** (edge case) | MINOR +1, PATCH = 0 | `v0.5.99` → **`v0.6.0`** |

## Examples

```
v0.5.0  → v0.5.1    (1 bug fix)
v0.5.1  → v0.5.2    (another bug fix)
v0.5.2  → v0.6.0    (large feature update — e.g. 13 languages, system language detection)
v0.6.0  → v0.6.1    (bug fix)
...
v0.9.3  → v1.0.0    (MINOR was at 9, new major update → MAJOR bumps)
```

## Patch numbers can exceed 9

- `v0.5.10` is valid (two digits)
- `v0.5.99` is the maximum patch number
- At `v0.5.99` → next major update → `v0.6.0` (edge case)

## Release checklist

Before tagging a new release, ALL of these must be updated to the new version:

1. **All `.csproj` files** — `<Version>`, `<AssemblyVersion>`, `<FileVersion>`
2. **`installer/installer.iss`** — `#define AppVersion`
3. **`installer/windows/installer.nsi`** — version string
4. **`installer/build-script.sh`** — `APP_VERSION=`
5. **`FlipsiColor/App.xaml.cs`** — version log string
6. **`MainViewModel.cs`** (WPF + Avalonia) — `_title` field
7. **`Lokalisierung.cs`** — version references (if any)
8. **JSON i18n files** (all 4) — `App.Titel` key
9. **`.github/workflows/ci.yml`** — release notes body

## Tag push rules (CRITICAL)

1. Make code changes → `dotnet build` → verify 0 errors, 0 warnings
2. `git commit` + `git push` to `main`
3. **Wait for main CI to be GREEN** (`gh run list`)
4. **ONLY THEN** `git tag vX.Y.Z` + `git push origin vX.Y.Z`
5. Wait for tag CI to complete → verify release assets

> ⚠️ **NEVER** push a tag before the code is committed and CI is green!
> (v0.5.1 was once built with v0.5.0 code because the tag was pushed before the commit — the tag captured the old state.)

> ⚠️ **NEVER** re-tag an existing version for new features — always use a new version number!
> (If v0.5.5 already exists and you have new features, the next release is v0.6.0, not a re-tagged v0.5.5.)