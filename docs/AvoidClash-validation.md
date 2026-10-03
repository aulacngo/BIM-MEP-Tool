# Avoid Clash: Pipe / Duct, U45 / U90 / Z45

Implemented on 2026-10-03 in both `net48` and `net8.0-windows`.

## Source behavior

- `AvoidClashGeometry.cs` exposes `BypassShapeMode { U45, U90, Z45 }` and contains document-free geometry.
- U45 creates five straight segments and four 45-degree elbows; U90 creates five straight segments and four 90-degree elbows. Both retain the original endpoint positions and external connections.
- Z45 retains endpoint 0, creates three straight segments and two 45-degree elbows, and translates endpoint 1 by the bypass offset. The final straight segment remains parallel to the original axis. The window shows endpoint 1 coordinates; for a connected endpoint the user must select the explicit disconnect checkbox. Switching away from Z45 resets that selection.
- Pipe, round Duct and rectangular Duct retain system type, curve type, reference level and dimensions. Rectangular section roll is transported through the bends and checked after regeneration.
- Existing insulation/duct lining types and thicknesses are copied to the new segments and elbows. Outside insulation contributes to clearance; internal duct lining does not enlarge the external envelope. Selected obstacle Pipe/Duct insulation also contributes to its envelope.
- SPACE switches Up/Down; ENTER applies; ESC closes. Invalid, nonfinite or less-than-10-mm clearance is rejected before mutation. The window scrolls when necessary and each shape/direction renders its own vector path, elbow markers, arrow and badge.
- Transactions run on the Revit API thread. Failure and commit status are checked, and a transaction group permits rollback if validation fails after commit. Start connections, replacement end connections, actual straight axes, profiles, fitting connections and obstacle clearance envelopes are verified.
- The legacy angle-based public overloads remain available for U45/U90 callers.

## Geometry and boundaries

45-degree diagonal run equals the offset. Offset direction must be perpendicular to the original axis. Global Up/Down therefore requires a horizontal source; sloped sources may use perpendicular Left/Right. Vertical sources, flex curves, oval Duct, pinned/grouped sources and curves with connected intermediate branches are rejected with a message.

Obstacle solids are represented by their transformed oriented bounding boxes, clipped to the transverse corridor occupied by the MEP section and clearance. This includes section contact when the centreline misses the obstacle and avoids projecting remote ends of an oblique beam into the routing span. Nested geometry and linked-element transforms are included. A bounding-box fallback is used when no solid is available.

This is a **conservative envelope calculation**, not an exact minimum-length solid-clearance solver. Concave obstacles or unusually large fitting envelopes can produce a rejected route even where a manual route is possible. The final envelope check includes actual fitting/insulation geometry and can reject a family whose body/flange exceeds the available clearance. Elbow families and routing preferences still determine whether Revit can create the planned fittings.

Clearance is checked against the selected obstacle. This command does not run a whole-model clash scan or translate downstream connected networks for Z45.

## Completed validation

- Both Release targets built with **0 warnings and 0 errors**, including after the insulation/lining and Ribbon updates.
- The paired command, window and geometry files are checked for byte-for-byte identity.
- `git diff --check` passed.
- Offline fixture: **94,010 assertions**, **7,200 randomized route cases**, and **12 rendered previews** (three modes × four directions). Checks include exact coordinates, angles, translated Z endpoint, short-length rejection, nonfinite inputs, rotated rectangular-section reach, an oblique-beam corridor, a centreline-miss/section-hit case, UI dispatch, disconnect reset, invalid input, SPACE, ENTER and ESC.
- WPF images were rendered from the actual window source and visually reviewed; the UI fixture substitutes Revit types and does not execute the command's model operations.

Reproduce the offline fixture:

```powershell
dotnet run --project scripts/tests/AvoidClash/AvoidClash.Tests.csproj -c Release -- 'D:\Tool Revit\scratch\AvoidClash-preview'
```

Build both targets:

```powershell
dotnet build src/net48/BIN.csproj -c Release --no-restore --nologo -v minimal
dotnet build src/net8.0-windows/BIN.csproj -c Release --no-restore --nologo -v minimal
```

## Revit runtime acceptance still required

No Revit process was running during this task. Correct-DLL hot-load, routing-preference fitting creation, real-model transaction/Undo behavior and linked-model execution have **not** been validated.

Use a disposable test model and verify:

1. Pipe, round Duct and rectangular Duct × all three modes, Up/Down and Left/Right. Include a rolled, non-square rectangular section; confirm four/four/two elbows and unchanged W/H.
2. Both U endpoints connected: confirm original remote connections and positions after success. For Z45 test an open end, a connected end without disconnect enabled (no mutation), and a connected end with disconnect enabled (old peer remains in place; new end open).
3. Narrow and oblique beams, nested geometry, translated/rotated/mirrored linked obstacles, and a section clash without a centreline intersection.
4. Missing 45/90 fitting family, insufficient straight length, oversized fitting flange, pinned/grouped source, connected branch, and forced transaction failure: confirm no partial route remains and one Undo item on success.
5. Insulated Pipe/Duct and lined Duct: confirm copied layer type/thickness on every new segment and elbow, and clearance measured outside insulation.
6. Verify original selected-obstacle clearance and inspect adjacent elements separately; Z45 intentionally leaves the downstream network at its original location.

API references: [Autodesk failure processing](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/f147e6e6-4b2e-d61c-df9b-8b8e5ebe3fcb.htm), [Autodesk MEP creation and insulation/lining](https://help.autodesk.com/cloudhelp/2024/PTB/Revit-API/files/Revit_API_Developers_Guide/Discipline_Specific_Functionality/MEP_Engineering/MEP_Element_Creation/Revit_API_Revit_API_Developers_Guide_Discipline_Specific_Functionality_MEP_Engineering_MEP_Element_Creation_Create_Pipes_and_Ducts_html.html).
