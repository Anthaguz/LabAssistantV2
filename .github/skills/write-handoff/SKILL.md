---
name: write-handoff
description: Format for handing off work between sessions or agents in LabAssistantV2. Use when writing a completion summary or a brief for the next session/agent, especially when coordinating multiple agents in parallel.
---

# write-handoff

A handoff is for a human or another agent to pick up cleanly.
Keep it compact and link everything with one click.

## Link-first

Every handoff references the work item with clickable links, not bare numbers:

- Issue: `[#123](https://github.com/Anthaguz/LabAssistantV2/issues/123)`
- PR: `[#124](https://github.com/Anthaguz/LabAssistantV2/pull/124)`
- Milestone (if relevant): `[Name](https://github.com/Anthaguz/LabAssistantV2/milestone/<n>)`

## Structure

```
## Handoff - <short title>

### Status
- Issue / PR / milestone links
- Branch name, commit hash if committed
- Labels applied
- Validation run and result

### What landed
- Touched files or major artifacts

### Outcome
- The behavioral or architectural result
- Important non-goals left untouched, when relevant

### Next
- Suggested next slice or open questions

### Notes
- Repo hygiene, leftover local artifacts, anything the next agent should know
```

## Rules

- State clearly what kind of change it was: runtime, docs-only, tests-only, or another narrow category.
- If labels or milestone were missing and could not be applied, say so.
- Do not replay the whole prior conversation.
  Reduce context, do not restate it.
- Shorten within this structure when a lighter handoff is enough, rather than dropping into loose prose.
