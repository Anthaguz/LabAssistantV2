---
name: ship-change
description: The change lifecycle for LabAssistantV2 - branch naming, commits, when to open a GitHub issue vs. not, simple vs. umbrella branch, PR creation, PR body format, and labels. Use whenever committing work, creating or updating a branch, or opening/editing a pull request.
---

# ship-change

How work ships in this repo.
The durable unit of work is branch -> commits -> PR.
Keep it lean, do not add ceremony that the PR itself already carries.

## When to open a GitHub issue

Default: do not open one.
The PR is the record.

Open an issue only when it earns its keep:

- The work is being deferred (real backlog, a promise to future-me).
- A bug is found now but fixed later.
- A decision or discussion needs a durable home.
- Multiple sessions or agents need to coordinate on it.

Never open an issue just to have something for a PR to close.
Never open an issue to narrate work already done.

## Branches

- One branch per change, created from current `origin/master`.
  Fetch and confirm the base is current before starting.
- Naming: `issue-<number>-<short-slug>` when there is an issue, otherwise `<short-slug>` in kebab-case.
- Use a single feature branch by default.
- Use an umbrella integration branch (for example `goal/<name>`) only for an explicitly approved multi-PR goal.
  Sub-branches then branch off the umbrella branch and PR back into it, and only the final umbrella PR targets `master`.

### Temporary worktrees

When the current session sits on a messy umbrella branch, ship a discrete change from a fresh worktree off `origin/master`.

- Create it outside the repository working tree, never inside it (for example a sibling scratch directory such as `../_worktrees/<slug>`).
- Make the change, push the branch, and open the PR from there.
- Remove it with `git worktree remove <path>` once the PR is pushed.
  The branch and PR live on the remote, so the local worktree is disposable.

## Commits

- Small, focused, one reason to change.
- Do not reformat unrelated files.
- Never auto-add an agent name as co-author.
- Do not commit local scratch, `HANDOFF.md`, or `.local/` PR drafts.

## Pull requests

- Draft the PR body in a local file under `.local/` first, then pass it to `gh pr create`/`gh pr edit` so Markdown survives shell escaping.
  Remove the draft afterward.
- Link the driving issue when one exists, with a closing keyword when the PR should close it.

### PR body format

Use these exact level-2 headers, each with bullet content:

- `## What`
- `## Why`
- `## Scope`
- `## Out of scope`
- `## Validation`
- `## Traceability`

In `## Traceability` use native issue references (`- Closes #123` or `- Issue: #123`) and a Markdown milestone link when milestone-scoped.

## Labels

Apply the dominant reason-to-change label to both the issue (if any) and the PR:

- `bug`, `feature`, `refactor`, `docs`, `test`, `chore`.

Keep it to the one label that is defensible from the scope.
Add a second only when the scope genuinely spans two categories.
Assign a milestone only when the work clearly belongs to a planned delivery slice.

## Before handoff

- Run the relevant build/tests and state the result.
- Confirm the PR and issue carry matching labels and milestone when applicable.
