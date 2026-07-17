# Logging status codes

This is the frozen specification for LabAssistant's 32-bit logging status codes.
It is the canonical reference for the bit layout, the facility/operation/status number assignments, and the registry authoring workflow.
The design rationale and prior-art survey live in the session design doc; this file is the contract that ships.

## Why

Today the structured log conflates several orthogonal ideas into two loosely-typed columns (`level` and `result`), and there is no stable identity for an event.
A user (or a developer) reading the log cannot reliably answer "what operation was this, did it succeed, how bad is it, and what do I do about it".

The status code gives every log event a single canonical 32-bit identity that is simultaneously:

- a stable identifier that never changes once shipped (like a Windows `HRESULT` / `winerror.h` code),
- a decomposable value whose bytes name the facility, operation, and outcome, and
- a bitmask the Configuration tab can filter on directly (for example "show me only Hyper-V errors").

The raw number is never shown alone.
It is always resolved through the registry into a plain-language message, a severity, and (when relevant) a remediation.
Showing a bare hex code to a user is the exact UX failure this design exists to avoid.

## Bit layout

A status code is an unsigned 32-bit value laid out as four octets:

```
  0x SS FF OO CC
     |  |  |  |
     |  |  |  +-- byte 0  status / outcome   (0x00 = OK)
     |  |  +----- byte 1  operation          (scoped within the facility)
     |  +-------- byte 2  facility / category
     +----------- byte 3  severity nibble (high) + flags nibble (low)
```

Octet alignment is deliberate and load-bearing: because each axis occupies exactly one byte (or nibble), the code doubles as a filter bitmask.
No axis is ever allowed to overflow its byte, because stealing bits across octet boundaries would break every mask below.
None of the axes need more than 256 values.

| Field    | Bits  | Mask         | Meaning                                                        |
| -------- | ----- | ------------ | ------------------------------------------------------------- |
| Severity | 28-31 | `0xF0000000` | How bad it is. Projects to the legacy `level` column.         |
| Flags    | 24-27 | `0x0F000000` | Orthogonal bitwise hints (retryable, transient, actionable).  |
| Facility | 16-23 | `0x00FF0000` | The subsystem / category.                                     |
| Operation| 8-15  | `0x0000FF00` | The specific operation within the facility.                   |
| Status   | 0-7   | `0x000000FF` | The outcome. `0x00` means OK (the `S_OK` of this scheme).     |

### Filter-mask examples

```
all Hyper-V machine events   (code & 0x00FF0000) == 0x00300000
errors and worse             ((code >> 28) & 0xF) >= 0x6
one exact event              code == 0x64410103   (guest credential rejected, error + user-actionable)
retryable events only        (code & 0x01000000) != 0
```

## Severity nibble (bits 28-31)

Severity is a single ordered value, not a bitfield.

| Value | Name     | Projected `level` |
| ----- | -------- | ----------------- |
| 0x0   | Success  | info              |
| 0x1   | Trace    | debug             |
| 0x2   | Debug    | debug             |
| 0x3   | Info     | info              |
| 0x4   | Notice   | info              |
| 0x5   | Warning  | warn              |
| 0x6   | Error    | error             |
| 0x7   | Critical | error             |
| 0x8   | Fatal    | error             |
| 0x9-F | reserved | -                 |

`Success` (0x0) is distinct from `Info` (0x3): it marks the successful completion of an operation, and it is what pairs with status `0x00`.
The legacy `level` column becomes a pure projection of this nibble, so `level` can never again disagree with the real severity.

## Flags nibble (bits 24-27)

Flags are bitwise and may be combined.

| Bit | Value | Name           | Meaning                                                              |
| --- | ----- | -------------- | -------------------------------------------------------------------- |
| 0   | 0x1   | Retryable      | The operation may be retried.                                        |
| 1   | 0x2   | Transient      | The condition is expected to clear on its own (still booting, etc.). |
| 2   | 0x4   | UserActionable | Resolving it needs a user action; surfaces the remediation.         |
| 3   | 0x8   | reserved       | -                                                                    |

## Phase is a separate field, not encoded in the number

An operation's lifecycle phase (`start`, `progress`, `end`, `atomic`) is an orthogonal structured field on the event, **not** part of the 32-bit code.
This removes the old `result = started / completed` versus `level = info` redundancy.

- A `start` row carries no outcome; its status byte is `0x00` and its severity is typically `Info`.
- An `end` (or `atomic`) row carries the real outcome in the status byte and the real severity.
- Start and end are kept as separate rows and distinguished in the UI by a phase chip.

Because a start row is `Info` severity while a successful end row is `Success` severity, the two never collide numerically even though both use status `0x00`.
The generator enforces that every composed 32-bit code is unique across the registry, so any accidental collision fails the build.

## Facility ranges (byte 2)

Facilities are grouped into reserved ranges, the way `winerror.h` reserves facility numbers and HTTP reserves status classes.
The range a facility falls in tells you its family at a glance, and leaves room to add siblings without renumbering.

| Range       | Family                              |
| ----------- | ----------------------------------- |
| 0x00 - 0x0F | Infrastructure / platform           |
| 0x10 - 0x1F | Assets (catalog, base disks, switches) |
| 0x20 - 0x2F | Templates                           |
| 0x30 - 0x3F | Machines (VM lifecycle)             |
| 0x40 - 0x5F | Deployment (two blocks)             |
| 0x60 - 0x6F | Networking                          |
| 0x70 - 0x7F | Data / persistence                  |
| 0x80 - 0x8F | UI / shell                          |
| 0x90 - 0xDF | Reserved for the future             |
| 0xE0 - 0xEF | Diagnostics / raw (folded log.txt)  |
| 0xF0 - 0xFF | Vendor / experimental               |

### Assigned facilities

Only facilities that exist today are assigned a number.
New facilities are appended within their range; numbers are never reused or renumbered.

| Code | Name                | Family        |
| ---- | ------------------- | ------------- |
| 0x00 | infra.app           | Infrastructure |
| 0x01 | infra.powershell    | Infrastructure |
| 0x02 | infra.filesystem    | Infrastructure |
| 0x10 | assets.catalog      | Assets        |
| 0x11 | assets.basedisk     | Assets        |
| 0x12 | assets.switch       | Assets        |
| 0x20 | templates           | Templates     |
| 0x30 | machines            | Machines      |
| 0x40 | deploy.orchestration| Deployment    |
| 0x41 | deploy.guest        | Deployment    |
| 0x42 | deploy.domain       | Deployment    |
| 0x43 | deploy.router       | Deployment    |
| 0x44 | deploy.trust        | Deployment    |
| 0x45 | deploy.cleanup      | Deployment    |
| 0x60 | net                 | Networking    |
| 0x70 | data                | Data          |
| 0x80 | ui                  | UI            |
| 0xE0 | diag.ps_timing      | Diagnostics   |
| 0xE1 | diag.debug          | Diagnostics   |

The two diagnostics facilities (`0xE0`, `0xE1`) are where the raw `log.txt` loggers fold in (see the fold-in migration).
They default to `Debug` severity so they are off in the log view unless explicitly enabled.

## Naming

The numeric code is the canonical identity.
Every other name is derived from it and therefore cannot drift:

- The dotted name `facility.operation[.phase]` is generated from the fields (for example `deploy.guest.transport-ready.end`).
- An optional friendly `title` per code is human-authored for display.
- The generated `LaStatus.<Name>` constant is `Facility_TitlePascalCase` (falling back to the operation name when a code has no title). Because the constant name incorporates the title, the title carries part of the developer-facing identity: renaming a shipped title renames the constant and breaks references. Titles are therefore frozen on the same terms as the code (see stability rules).

The dotted name is a human-friendly grouping, not a discriminator: it omits the status byte, so a success and a failure of the same operation and phase share one dotted name.
For example "VM removed" and "VM remove failed" both render as `machines.remove.end`, and "Session created" and "Session retired" both render as `infra.powershell.session.end`.
They are told apart by their distinct 32-bit `code` (and the severity, level, and result the code carries), never by the `event` string alone.
Anything that must select one exact event - an alert, a filter, a metric - keys on the `code`; the dotted `event` is only for reading and coarse grouping.

This replaces today's inconsistent mix of dotted (`hyperv.*`) and flat PascalCase (`CleanupStarted`) event names.

## The registry

All codes live in one authoritative data file, `LabAssistant.Services/Diagnostics/status-codes.yaml`.
A Roslyn source generator turns that file into:

1. `LaStatus` - strongly-typed `const uint` code constants,
2. a `StatusCodeCatalog` - the full in-memory table (code, names, severity, flags, phase, message, remediation),
3. a `StatusCodes.Describe(uint)` resolver that returns the resolved description for any code, and
4. facility / operation lookup tables the log UI uses for its filter.

Because code, name, message, and remediation all come from the one file, they are provably in sync.

### Stability rules

- Codes are **append-only**. Once a code ships, its number, meaning, and severity never change.
- A shipped code's `title` is frozen too, because the generated `LaStatus` constant name is derived from it. Reword display text by shipping a new code and retiring the old one, not by editing a live title.
- A retired event keeps its code reserved forever; the number is never reused.
- The generator fails the build on a duplicate composed code, a duplicate facility/operation/severity/flag definition, or an out-of-range field.

### Authoring workflow

To add an event: add one entry to `status-codes.yaml` (facility, operation, status, severity, optional flags, phase, title, message, optional remediation), then build.
The generator produces the constant and catalog entry; the emit site references the generated `LaStatus` constant.
