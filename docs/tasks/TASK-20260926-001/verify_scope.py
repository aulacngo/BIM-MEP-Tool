"""Read-only source/scope self-check against the captured DSP-002 baseline."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = Path(__file__).resolve().parent
RUNTIMES = ("net48", "net8.0-windows")
MODIFIED = ("ElbowHelper.cs", "NaviateHelper.cs")
ADDED = ("DuctProfileData.cs", "ElbowGeometry.cs", "DuctElbowBuilder.cs")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


with (EVIDENCE / "baseline-source-hashes.csv").open(encoding="utf-8-sig") as stream:
    baseline = {Path(row["Path"]): row["SHA256"] for row in csv.DictReader(stream)}

changed = {p.relative_to(ROOT).as_posix() for p, old in baseline.items() if not p.exists() or sha(p) != old}
expected = {f"src/{runtime}/BIN/{name}" for runtime in RUNTIMES for name in MODIFIED}
assert changed == expected, ("Unexpected existing-file changes", changed ^ expected)
new_cs = {p.relative_to(ROOT).as_posix() for p in (ROOT / "src").rglob("*.cs") if p not in baseline}
expected_new = {f"src/{runtime}/BIN/{name}" for runtime in RUNTIMES for name in ADDED}
assert new_cs == expected_new, ("Unexpected new C# source", new_cs ^ expected_new)

hashes = {}
for name in MODIFIED + ADDED:
    paths = [ROOT / "src" / runtime / "BIN" / name for runtime in RUNTIMES]
    assert paths[0].read_bytes() == paths[1].read_bytes(), ("Target drift", name)
    hashes[name] = sha(paths[0])

commands = ("Up", "Down", "Left", "Right", "Up45", "Down45", "Left45", "Right45")
for runtime in RUNTIMES:
    for suffix in commands:
        p = ROOT / "src" / runtime / "BIN" / f"Elbow{suffix}Cmd.cs"
        assert sha(p) == baseline[p], ("Wrapper changed", p)
        assert f"ElbowDirection.{suffix}" in p.read_text(encoding="utf-8-sig")
    for name in ("Create2Elbows45.cs", "Join2Elbows.cs", "Panel.cs", "DevCommandProxy.cs"):
        p = ROOT / "src" / runtime / "BIN" / name
        assert sha(p) == baseline[p], ("Excluded file or mapping changed", p)

result = {
    "status": "PASS",
    "existing_files_changed": sorted(changed),
    "new_production_files": sorted(new_cs),
    "both_targets_identical_sha256": hashes,
    "unchanged": "8 wrappers per target, Panel, DevCommandProxy, Create2Elbows45, Join2Elbows; all other baseline source/resource files",
    "build_sha256": {
        runtime: sha(EVIDENCE / "build" / runtime / "BIN.dll") for runtime in RUNTIMES
    },
    "limitation": "Scope and source checks only. No Revit runtime, activation or deployment verification.",
}
print(json.dumps(result, indent=2))
