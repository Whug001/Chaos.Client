## Key Dependencies

| Package                                    | Purpose                                                                   |
|--------------------------------------------|---------------------------------------------------------------------------|
| DALib                                      | Dark Ages file format support, SkiaSharp rendering                        |
| MonoGame.Framework.DesktopGL 3.8.4.1       | Cross-platform graphics/windowing                                         |
| Chaos.Networking (Chaos-Server submodule)            | Complete protocol library: packet converters, crypto, opcodes, args types |
| Chaos.Common (Chaos-Server submodule)                | Shared extension methods                                          |
| Chaos.DarkAges (Chaos-Server submodule)              | Dark Ages protocol types                                          |
| Chaos.Geometry (Chaos-Server submodule)              | Geometry types -- rectangles, points                              |
| Chaos.Pathfinding (Chaos-Server submodule)           | A* pathfinding                                                    |
| Microsoft.Extensions.Caching.Memory 10.0.5 | MemoryCache infrastructure                                                |
| TextCopy 6.2.1                             | Cross-platform clipboard access (used by `Utilities/Clipboard`)           |

## Build Configuration

Centralized in `Directory.Build.props`: C# 14, net10.0, nullable enabled, implicit usings, TieredPGO + TieredCompilation (+ QuickJit) enabled, WarningLevel 4, EnforceCodeStyleInBuild. Package versions managed centrally in `Directory.Packages.props`. Versioning via Nerdbank.GitVersioning. The `Chaos.*` shared libraries are referenced as projects from the `Chaos-Server` git submodule (path set by the `UnoraServerPath` MSBuild property, default `./Chaos-Server`); a build-time check errors if the submodule has not been initialized.
