# Framework source snapshots

Captured read-only on 2026-10-22 from the owner's local framework repositories.
The source files included here are the reproducible build inputs; builds do not
read sibling repositories or require private/local NuGet feeds. Reported HEADs
are research metadata, not a claim that the source working trees were clean.

| Library | Project version | Reported HEAD | Dependencies |
| --- | --- | --- | --- |
| Assets | 0.1.4 | bc2b688 | Raylib-cs 8.0.0 |
| Configuration | 0.1.0 | b77fce6 | .NET 9 / System.Text.Json |
| Input | 0.2.0 | 324d85e (working-tree snapshot; see below) | Raylib-cs 8.0.0 |
| Logging | 0.1.0 | 8ca8f87 | .NET 9 |
| Menus | 0.1.3 | b40b789 | Raylib-cs 8.0.0, Input >= 0.1.4 |
| ThreeD | 0.1.0 | 53a1bdd | Raylib-cs 8.0.0, Assets 0.1.4 |

Origins: https://github.com/Clivej13/RaylibGameFramework.{library-name}

All implementation .cs files and available READMEs were copied verbatim.
Project-file integration changes: Menus' Input 0.1.4 package reference becomes
a relative project reference to the included Input 0.2.0; ThreeD's Assets 0.1.4
package reference becomes a relative project reference to the included Assets.
Upstream tests and build outputs are not vendored. Original repositories were
not modified. Configuration's inspected source project is labelled 0.1.0 and
includes JsonSettingsStore; TackleAlley references a locally available 0.1.1
package. CCM deliberately uses the included source, not that cached binary.

A clean NuGet.org-only restore on 2026-10-22 reported NU1101 for all five framework
package IDs. Raylib-cs remains a public PackageReference; application and test
lock files record the resolved dependency graph. NuGet.Config excludes
machine-local feeds. Replace snapshots with exact package references when an
accessible feed publishes the required APIs, updating locks and running both
test suites. Until then, update snapshots explicitly and review their source diff.

Also reviewed but not integrated:
- Character 0.1.0: .NET 9 / System.Numerics ragdolls, skeleton mapping and recovery.
Character is not needed for the block placeholder.

ThreeD was added for the dungeon on 2026-10-22 from the source checkout
reporting HEAD 53a1bdd. Its five implementation files are unchanged; no README
was present. CCM uses Transform3D. Animation, material overrides and particles
are available but unused. No external framework repository was modified.

Read-only integration reference: RaylibTackleAlley project references,
Application/GameApplication.cs and Application/MenuDisplay.cs. CCM retains
graphics-thread asset ownership and menu/input integration, with incremental
loading instead of TackleAlley's blocking startup load loop.

Input 0.2.0 migration: source was read from the owner's
RaylibGameFramework.Input checkout on branch feat/selected-gameplay-input.
The project declares Version 0.2.0; the reported HEAD remains 324d85e, so that
commit alone does not identify this release. The exact included source snapshot
is the reproducible input. All four production .cs files, project and README
were copied without modification. A fresh NuGet.org-only restore for exact
0.2.0 returned NU1101, so CCM retains project references rather than relying on
a machine-local package cache. Menus and CCM resolve the same Input project.
The external framework checkout was not modified.
