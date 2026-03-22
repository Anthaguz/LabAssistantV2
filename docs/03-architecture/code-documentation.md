# Code Documentation

**Purpose:** Define how code should be documented in this repository so composition seams, workflow coordination, and maintenance boundaries stay understandable without relying on milestone-specific context.

## Why This Exists
The codebase now relies heavily on:
- workspace compositions,
- controllers,
- shell bridges,
- long-lived route-aware workflows,
- and capability-local ownership boundaries.

Those patterns are maintainable only if the code explains the parts that are not obvious from syntax alone:
- why something lives in the shell,
- why something is capability-local,
- what must stay long-lived,
- and what sequencing or invariants must not be broken.

The goal is not to maximize comment count. The goal is to make future maintenance safer and faster.

## Core Rule
Comments must explain intent, boundaries, invariants, or non-obvious coordination.

Comments should not narrate mechanics that are already clear from the code.

## What Must Be Documented
Use XML documentation comments for:
- public classes, interfaces, methods, properties, and events,
- architecture seam contracts that act as important maintenance boundaries even when not public, especially:
  - workspace composition classes,
  - workspace controllers,
  - host interfaces,
  - shell bridge interfaces.

These comments should explain:
- the responsibility of the type or member,
- the boundary it owns,
- and, when relevant, what it intentionally does not own.

## What Should Be Documented
Use short inline comments when the code would otherwise hide:
- shell vs capability ownership rationale,
- route refresh vs recreate behavior,
- long-lived workspace assumptions,
- cleanup/cancellation expectations,
- ordering-sensitive workflow steps,
- non-obvious temporary bridge decisions,
- or behavior that looks odd but is intentional.

These comments are especially useful in:
- `MainWindow.xaml.cs`,
- workspace composition classes,
- workflow controllers,
- host bridge implementations,
- and complex UI-state synchronization methods.

## What Should Not Be Documented
Avoid comments that:
- restate a method or variable name,
- explain trivial assignments,
- narrate obvious control flow,
- document event hookups that are already self-explanatory,
- or turn small methods into prose blocks longer than the method itself.

Bad examples:
- "Sets the status text."
- "Calls the controller."
- "Updates the UI."

## Preferred Comment Size
Comment length should match the concept, not follow a fixed quota.

Preferred default:
- one short XML summary sentence for straightforward types and members,
- one to three short sentences when a method or type needs boundary or invariant context,
- one short inline comment at a non-obvious decision point when needed.

Longer comments are acceptable when they explain a real maintenance hazard, but they should still stay focused on:
- intent,
- boundaries,
- invariants,
- and consequences of changing the code.

## Practical Guidance By Code Shape
### Composition classes
Document:
- what local/shared ownership they coordinate,
- what they delegate,
- and what must remain outside their boundary.

### Controllers
Document:
- what workflow they coordinate,
- what state they mutate,
- and what external actions or services they orchestrate.

### Host interfaces and shell bridges
Document:
- why the bridge exists,
- what shell-owned help is intentionally exposed,
- and what should not be routed back through the shell.

### Private workflow methods
Document only when the method owns a non-obvious step such as:
- preserving long-lived state,
- reconciling selection after refresh,
- sequencing cleanup-sensitive operations,
- or preventing route-triggered recreation.

## Rollout Strategy
Apply this standard incrementally:
- first in files already being changed,
- then in high-value maintenance seams,
- and only later as opportunistic cleanup elsewhere.

Do not open broad "document everything" sweeps unless they are explicitly approved and narrowly staged.

Recommended priority order:
1. shell boundaries and shared compositions,
2. capability-local compositions/controllers,
3. host interfaces and shell bridges,
4. workflow-heavy private methods,
5. opportunistic cleanup in touched files.

## Open Questions / TBDs
- TBD: whether style-analyzer enforcement for XML documentation is worth adding later for selected projects or folders.
