# Cleanup and Cancellation Policy

**Purpose:** Define deterministic runtime behavior for deployment failure and user cancellation, including cleanup order, status outcomes, and residual reporting.

## Policy Summary

- Cleanup is expected behavior.
- On failure or cancellation, the system performs best-effort cleanup for created resources.
- Cleanup runs in reverse dependency order.
- If cleanup cannot fully complete, the system reports residuals explicitly.

## Resource Cleanup Model

For each VM deployment operation, cleanup targets include:

1. VM power state (stop if running).
2. Hyper-V VM registration (remove if present).
3. VM filesystem directory (remove if present).
4. Differencing VHDX file (remove if present).

Cleanup should attempt all steps even if earlier cleanup actions fail.

## Cleanup Order

Use reverse operational order to reduce dependency conflicts:

1. Stop VM
2. Remove VM entry in Hyper-V
3. Remove VM folder artifacts
4. Remove differencing disk artifacts

For multi-VM lab operations, run per-VM cleanup and then aggregate results.

## Failure Status Outcomes

Allowed terminal operation statuses:

- `succeeded`
- `failed`
- `cancelled`
- `failed_with_residuals`
- `cancelled_with_residuals`

Residual statuses are required when at least one cleanup action fails and resources may remain.

## Residual Reporting

If residuals exist, the user must receive:

- What could not be cleaned.
- Location/path or resource name.
- Suggested manual cleanup action.

Logs must include each cleanup attempt result and a residual summary.

## Cancellation Model

Cancellation behavior is step-boundary based:

- Cancel request is acknowledged immediately.
- Running step is allowed to finish safely.
- No new steps begin after boundary.
- Cleanup starts after the boundary.

Targets:

- Acknowledge cancel request in <= 2 seconds.
- Stop forward progression at safe boundary in <= 10 seconds (best effort).

No hard-abort of in-flight external commands unless explicitly added in a future policy revision.

## UX Requirements

- Show operation state: running, cancelling, cleanup in progress, completed.
- Do not hide cleanup errors.
- Final message must distinguish:
  - operation failure reason
  - cleanup result

## Testing Requirements

Minimum scenarios:

1. Mid-operation failure with full cleanup success.
2. Mid-operation failure with partial cleanup failure.
3. User cancellation while a step is running.
4. Multi-VM run where one VM fails and global stop policy is enabled.

## Open Questions / TBDs

- Timeout strategy for long-running external commands during cancel.
- Whether to include optional retry cleanup action in UI.
