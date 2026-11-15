# Git Workflow Rules

## Branch Strategy

When the user requests work on a feature (e.g. "test 브랜치로 작업해줘", "xxx 기능 만들어줘"):

1. Create `epic/{name}` branch from `main`
2. Create `claude/{name}` as the working branch from `epic/{name}`
3. Do all work on `claude/{name}`
4. Push both branches to remote

```
main
 └── epic/{name}        ← created first, base for PRs
      └── claude/{name} ← actual working branch
```

### Example

User says: "test 브랜치로 작업해줘"

```bash
git checkout main
git checkout -b epic/test
git push -u origin epic/test

git checkout -b claude/test
git push -u origin claude/test
```

---

## Branch Naming Rules

| Prefix | Used by | Example |
|--------|---------|---------|
| `epic/` | feature 집합 | `epic/bottle-system` |
| `claude/` | Claude Code | `claude/bottle-presenter` |
| `codex/` | GitHub Codex | `codex/bottle-view` |
| `feature/` | manual work | `feature/stage-data` |
| `fix/` | bug fix | `fix/slot-color-bug` |

---

## Commit Rules

- Stage only relevant files — never `git add .` or `git add -A`
- One commit per logical change

```bash
git add {specific-file}
git commit -m "{message}"
git push origin claude/{name}
```

---

## PR Automation Rules

When the user says "PR 넣어줘", "PR 만들어줘", "PR 올려줘", or similar:

1. Detect current branch (e.g. `claude/test`)
2. Automatically find the matching `epic/*` as the base (e.g. `epic/test`)
   - Match by name suffix: `claude/test` → `epic/test`
   - If no matching `epic/*` exists: **STOP — never fall back to `main`.** Report the missing epic branch and ask the user.
3. Create PR: `claude/{name}` → `epic/{name}`
4. Merge immediately after creation
5. Report result with PR URL and merge status

**Do not ask for confirmation — create and merge in one flow.**

### Example

```
Current branch: claude/test
→ Detected base: epic/test
→ gh pr create --base epic/test ...
→ gh pr merge {number} --merge
→ Report: PR #X merged into epic/test ✓
```

> **Only an `epic/*` branch may target `main`.** A non-`epic/*` branch (`claude/*`, `codex/*`, `feature/*`, `fix/*`, …) may NEVER be PR'd or pushed into `main` — under any circumstance.
> `epic → main` merge is NOT automated. Always wait for explicit user instruction before merging into main.
> Hard-enforced by `.claude/hooks/block-dangerous-git.py` (blocks `git push` to main/master and `gh pr create --base main` whose head — `--head`, or the current branch when omitted — is not `epic/*`; `gh pr merge` is not checked by the hook — follow this rule).
> First push: when the remote has no branches yet (a brand-new repository), the hook allows the one push that creates `main`. After that, `main` only changes through `epic/*` PRs.

### Merging `epic/*` into `main` (a person does this)

When the user asks for the epic to go into `main` ("main에 합쳐줘", "3번 진행"):

1. Create the PR `epic/{name}` → `main` (`gh pr create --base main --head epic/{name}`) and report its URL.
2. Try the merge only on that explicit instruction. **Claude Code auto mode refuses it**: its safety check treats `gh pr merge` into the default branch as a merge without review and denies the command, even when the user has just asked for it. This is the permission layer, not this repository's hook, and it is not an error to retry or work around (no direct push, no different command for the same merge).
3. When it is refused, stop and tell the user in plain words that the last step needs them, with the two ways:
   - **Merge on the web**: open the PR link and press "Merge pull request". Nothing to install or configure.
   - **Allow it once for this project**: add a permission rule for the merge command (for example `Bash(gh pr merge:*)` under `permissions.allow` in `.claude/settings.local.json`), or run the session in a mode that asks before each command, then ask again. Offer to add the rule; do not add it without being asked. Whether an allow rule lifts the auto-mode refusal has not been verified with this kit — if it is still refused, use the web merge.
4. After the user says it is merged, verify (`gh pr view {number} --json state,mergedAt`) and continue. Until then report the work as "PR open, not merged into main".

---

## Why This Structure

| Problem | Solution |
|---------|----------|
| AI 두 개가 같은 파일 충돌 | 브랜치 분리로 격리 |
| 어느 AI가 뭘 했는지 추적 어려움 | 브랜치 prefix로 명확히 구분 |
| AI 코드 리뷰 없이 main 반영 | epic에서 한 번 더 검토 후 merge |
| 롤백 범위 불명확 | PR 단위로 revert 가능 |
