# Test fixtures

Small, committable inputs for the UI automation harness live here.
Large binaries (ISOs, VHDX base disks) do NOT live here - they are git-ignored and referenced by absolute path from `testenv.json`.

## Layout

- `testenv.json` - machine-local environment config: where base disks / ISOs live, which virtual switch to borrow, and which resource-provider mode to run (`discover-existing` today, `dedicated` later).
- `templates/` - pre-authored template JSON fixtures the harness can load, save, and deploy.
- `expected/` - expected-configuration manifests a scenario validates the real Hyper-V state against.

## Big binaries (git-ignored)

Drop your ISOs and base VHDX anywhere on disk (a subfolder here is fine - `*.iso` / `*.vhdx` are ignored), then point `testenv.json` at them by absolute path.
Keep them out of source control; they are huge and machine-specific.

## Resource-provider modes

- `discover-existing` (current): the harness borrows an existing base disk + virtual switch already known to the app, and only ever deletes VMs/differencing disks it created (tagged `LAT-<runId>-`). It never deletes the borrowed base disk or switch.
- `dedicated` (future): the harness owns a dedicated `LAT-` base disk + switch sourced from fixtures, for a fully isolated sandbox.
