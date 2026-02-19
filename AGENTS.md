# AGENTS.md — PS Multi Tools Evolved

This file is the authoritative guide for AI coding agents working on this repository.
Read it top-to-bottom before writing any code. Sections are ordered from most to least critical.

---

## 1. Agent Rules & Anti-Slop Discipline

> Read this section first. Violations here are the most common cause of wasted effort.

- **Do NOT** create new helper or utility classes if `Utils.cs` or any existing class in `Classes/` already covers the need. Always search there first.
- **Do NOT** introduce MVVM ViewModels. The project uses **AXAML + code-behind only** (`partial class : Window` or `partial class : UserControl`).
- **Do NOT** add docstrings, XML comments, or type annotations to code you did not write.
- **Do NOT** introduce error handling for impossible scenarios. Only validate at real system boundaries: user input, external process output, file I/O, network responses.
- **Do NOT** create new dialog classes. Reuse the existing ones from `Dialogs/`: `InputDialog`, `CustomDialog`, `Downloader`, `Uploader`, `CopyWindow`, `SyncWindow`.
- **Do NOT** refactor working code outside the scope of the current task.
- **Do NOT** add feature flags, backwards-compat shims, or verbose logging for non-critical code paths.
- **Do NOT** remove, rename, or change execution paths of any binaries under `Tools/Windows/`, `Tools/Linux/`, or `Tools/macOS/`. These are third-party shipped executables. Only the C# wrapper code calling them should ever change.
- **Always read a file before editing it.** Never guess at its structure.
- **Prefer editing existing `.axaml` / `.cs` files** over creating new ones.
- **Keep solutions at the minimum complexity** that satisfies the task. Three similar lines beat a premature abstraction.

---

## 2. Architecture & Patterns

### AXAML + Code-Behind (the only UI pattern used)
Every window and user control consists of exactly two files:
- `Foo.axaml` — declarative Avalonia XML layout
- `Foo.axaml.cs` — `public partial class Foo : Window` (or `: UserControl`)

There are no ViewModels, no binding contexts, no reactive extensions. UI state is managed directly in code-behind via event handlers.

### Namespaces mirror directory structure
```
PSMultiTools.PS5.Tools.Editors   →   PSMultiTools/PS5/Tools/Editors/
PSMultiTools.Dialogs             →   PSMultiTools/Dialogs/
PSMultiTools.Classes             →   PSMultiTools/Classes/
```
Use file-scoped namespace declarations: `namespace PSMultiTools.PS5.Tools;`

### Application startup & drag-drop routing (`App.axaml.cs`)
The app routes CLI arguments / drag-dropped files by extension to specific windows:

| File / Extension | Opens |
|---|---|
| `.pkg` | `MultiPlatformTools.PKGInfo` |
| `param.json` | `PS5.Tools.Editors.PS5ParamEditor` |
| `manifest.json` | `PS5.Tools.Editors.PS5ManifestEditor` |
| `.elf` or `.bin` | `PS5.Tools.PS5Sender` |
| *(no args)* | `MainWindow` |

**Preserve these entry points in any GUI redesign.**

### Configuration persistence
- INI format via `ini-parser-netstandard`
- Config file: `psmt-config.ini` in the working directory
- Pattern: read on window `Loaded` event, write on value change or window `Closing`

### Async I/O
- All file I/O, network calls, and external process execution must use `async/await`
- Long-running background work: `await Task.Run(...)`
- Show progress to the user: open `Downloader` or `CopyWindow` from `Dialogs/` before the operation starts

### Shared utility hub
`Classes/Utils.cs` contains audio playback, network helpers, admin detection, FTP utilities, and common message dialogs. **Check here before writing any new helper.**

### Code style inside files
- `#region` / `#endregion` blocks for logical grouping within large files
- Nullable reference types are enabled — annotate nullable fields/params with `?`
- Suppressed diagnostics (already in `.editorconfig`): CS0618, IDE1006, IDE0059

---

## 3. GUI Development Guidelines

> The primary current goal is a new, intuitive GUI. Follow these rules strictly.

### Theme
- Every `<Window>` must have `RequestedThemeVariant="Dark"`
- Use `Avalonia.Themes.Fluent` (already configured in `App.axaml`)

### Navigation model (3 tiers)
```
MainWindow  →  [Platform]Library  →  *Menu (UserControl)  →  Tool Window
```
- Do not add more than 3 tiers of navigation depth
- The console selection on `MainWindow` uses clickable images; preserve this metaphor

### Adding a new tool window
1. Create `ToolName.axaml` + `ToolName.axaml.cs` in the matching platform folder (e.g., `PS5/Tools/`)
2. Add a `<MenuItem>` to the corresponding `*Menu.axaml.cs` UserControl
3. For cross-platform tools: place files in `MultiPlatformTools/` instead

### Window sizing
- Prefer `CanResize="False"` with explicit `Height` and `Width`
- Only allow resize for views that display variable-length data (file browsers, log viewers)

### Large data lists
Use `ListBox` with a `DataTemplate` and `VirtualizingStackPanel`:
```xml
<ListBox>
  <ListBox.ItemsPanel>
    <ItemsPanelTemplate>
      <VirtualizingStackPanel />
    </ItemsPanelTemplate>
  </ListBox.ItemsPanel>
  <ListBox.ItemTemplate>
    <DataTemplate>
      <!-- your columns here -->
    </DataTemplate>
  </ListBox.ItemTemplate>
</ListBox>
```

### Progress dialogs
For any operation that takes more than a second, show a dialog from `Dialogs/` before starting:
```csharp
var dl = new Downloader() { ShowActivated = true };
dl.Show();
if (await dl.CreateNewDownload(url) == false)
{
    Utils.ShowDownloadErrorMessage();
    dl.Close();
}
```

### Image assets
Use existing PNGs from `Images/`. Do not add new image files without confirming they are needed and naming them consistently with the existing set (e.g., `actionname.png`, `CONSOLENAME.png`).

---

## 4. File Structure Map

```
PS-Multi-Tools-Evolved/
├── PSMultiTools/                        # C# source root
│   ├── App.axaml / App.axaml.cs         # Startup, theme, drag-drop routing
│   ├── MainWindow.axaml / .cs           # Home screen — console selection hub
│   ├── PSSettings.axaml / .cs           # Global app settings window
│   ├── Program.cs                       # Entry point + CEF (Chromium) initialization
│   │
│   ├── Classes/                         # Shared utilities & data models
│   │   ├── Utils.cs                     # ★ Shared utility hub — CHECK HERE FIRST
│   │   ├── PS1Game.cs … PS5Game.cs      # Game data models (one per platform)
│   │   ├── PSPGame.cs, PSVGame.cs
│   │   ├── AESEngine.cs                 # AES encryption/decryption
│   │   ├── XOREngine.cs                 # XOR cipher
│   │   ├── PKGDecryptor.cs              # PKG file decryption
│   │   ├── MD5Hash.cs                   # MD5 hashing helpers
│   │   ├── TelnetClient.cs              # Console network communication (PS3/PS4)
│   │   ├── SFONew.cs                    # PARAM.SFO read/write
│   │   ├── RCOExtractor.cs              # RCO resource extraction
│   │   ├── Structures.cs                # Shared data structures / enums
│   │   ├── EndianIO.cs                  # Big/little endian binary I/O
│   │   ├── AnyBitmapToAvaloniaConverter.cs
│   │   ├── ImportHelper.cs
│   │   └── DownloadHelper.cs
│   │
│   ├── Dialogs/                         # Reusable modal windows — DO NOT DUPLICATE
│   │   ├── InputDialog.axaml / .cs      # Single text input field
│   │   ├── CustomDialog.axaml / .cs     # Generic message dialog
│   │   ├── Downloader.axaml / .cs       # File download + progress bar
│   │   ├── Uploader.axaml / .cs         # File upload + progress bar
│   │   ├── CopyWindow.axaml / .cs       # File copy operation + progress
│   │   └── SyncWindow.axaml / .cs       # Synchronization operation
│   │
│   ├── Menus/                           # Platform-specific UserControl menus
│   │   ├── PS1Menu.axaml / .cs
│   │   ├── PS2Menu.axaml / .cs
│   │   ├── PS3Menu.axaml / .cs
│   │   ├── PS4Menu.axaml / .cs
│   │   ├── PS5Menu.axaml / .cs
│   │   ├── PSPMenu.axaml / .cs
│   │   └── PSVMenu.axaml / .cs
│   │
│   ├── MultiPlatformTools/              # Cross-platform tools
│   │   ├── FTPBrowser.axaml / .cs
│   │   ├── PKGInfo.axaml / .cs
│   │   ├── PKGBrowser.axaml / .cs
│   │   ├── PayloadDispatcher.axaml / .cs
│   │   ├── BDBurner.axaml / .cs
│   │   ├── SFOEditor.axaml / .cs
│   │   ├── BatchRename.axaml / .cs
│   │   └── PSClassicsfPKGBuilder.axaml / .cs
│   │
│   ├── MemoryCard/
│   │   └── PS2MCManager.axaml / .cs
│   │
│   ├── PS1/                             # PlayStation 1 module
│   │   ├── PS1Library.axaml / .cs
│   │   └── Tools/
│   │       ├── BINCUEConverter.axaml / .cs
│   │       └── MergeBinTool.axaml / .cs
│   │
│   ├── PS2/                             # PlayStation 2 module
│   │   ├── PS2Library.axaml / .cs
│   │   └── Tools/  (CUE2POPS, ELF2KELF, STAR, PAKer, ...)
│   │
│   ├── PS3/                             # PlayStation 3 module
│   │   ├── PS3Library.axaml / .cs
│   │   └── Tools/  (ISO, PKG, PUP, RCO, SELF, webMAN, VirtualFolder, ...)
│   │
│   ├── PS4/                             # PlayStation 4 module
│   │   ├── PS4Library.axaml / .cs
│   │   └── Tools/  (PPPwn, PKG, PUP, PSN, USBWriter, ...)
│   │
│   ├── PS5/                             # PlayStation 5 module (largest)
│   │   ├── PS5Library.axaml / .cs
│   │   └── Tools/
│   │       ├── Editors/                 # param.json, manifest.json editors
│   │       ├── GamePatches/             # Game patch downloader
│   │       ├── PKGBuilder/              # PKG build, merge, GP5
│   │       ├── WebSrv/                  # webMANBrowser (CEF)
│   │       └── (15+ more tool windows)
│   │
│   ├── PSP/                             # PlayStation Portable module
│   │   ├── PSPLibrary.axaml / .cs
│   │   └── Tools/  (CSO, ISO↔CSO, PBP)
│   │
│   ├── PSV/                             # PlayStation Vita module
│   │   ├── PSVLibrary.axaml / .cs
│   │   └── Tools/  (PKG, PFS, RCO)
│   │
│   ├── PSX/                             # PSX (PS2 HDD console) module
│   │   ├── PartitionManager/
│   │   ├── Projects/
│   │   └── XMB/
│   │
│   └── Images/                          # 74 PNG icon assets
│       ├── PS1.png … PS5.png, PSP.png, PSV.png, psx.png   # Console logos
│       ├── download.png, upload.png, convert.png, ...      # Action icons
│       └── de.png, fr.png, jp.png, us.png, ...             # Language flags
│
├── Libraries/
│   ├── Windows/                         # Windows native DLLs
│   ├── Linux/                           # Linux shared libraries
│   └── macOS/                           # macOS dylibs
│
├── Tools/                               # Third-party CLI executables — DO NOT MODIFY
│   ├── Windows/
│   ├── Linux/
│   └── macOS/
│
├── Drivers/Windows/                     # Optional device drivers
├── PSMultiTools.slnx                    # Solution file
├── README.md                            # User-facing docs (update version on release)
└── LatestBuild.txt                      # Current version: 16.2.0
```

---

## 5. Coding Conventions

| Convention | Rule |
|---|---|
| Classes, methods, properties, namespaces | PascalCase |
| Local variables | camelCase |
| Namespace declaration | File-scoped: `namespace PSMultiTools.PS5.Tools;` |
| Event handlers | `private async void ButtonName_Click(object? sender, RoutedEventArgs e)` |
| Control lookup | Prefer `x:Name` in AXAML; avoid `FindControl<T>()` in new code |
| Grouping | `#region SectionName` / `#endregion` for logical blocks in large files |
| Nullable | Use `?` on all nullable reference types and parameters |

---

## 6. Tech Stack Reference

| Component | Library / Version |
|---|---|
| Language | C# with nullable enabled |
| Runtimes | .NET 8.0 and .NET 9.0 (multi-targeted) |
| UI Framework | Avalonia 11.3.11 |
| UI Theme | Avalonia.Themes.Fluent (dark) |
| Rendering | Avalonia.Skia |
| Code Editor | Avalonia.AvaloniaEdit 11.3.0 |
| Web Browser | CefGlue.Avalonia 120.6099.211 (webMAN views) |
| Media | LibVLCSharp.Avalonia 3.9.5 |
| FTP | FluentFTP 53.0.2 |
| Image processing | Magick.NET-Q8-x64 14.x, IronSoftware.System.Drawing |
| Database | Microsoft.Data.Sqlite |
| JSON | Newtonsoft.Json 13.0.4 |
| INI config | ini-parser-netstandard 2.5.3 |
| HTML parsing | HtmlAgilityPack 1.12.4 |
| Disc images | LTRData.DiscUtils.Iso9660 + UDF |
| Message dialogs | MessageBox.Avalonia 3.3.1.1 |
| Native DLLs | PS4_Tools.dll, LibOrbisPkg.dll, PARAM.SFO.dll, GameArchives.dll |
| COM (Windows only) | IMAPI2 (disc burning), NetFwTypeLib (Windows Firewall) |

---

## 7. External Tools & Platform Dependencies

The project ships 40+ third-party CLI executables under `Tools/`. **Do not modify them.**
The C# code calls them via `ProcessStartInfo`. Examples:

| Tool | Used for |
|---|---|
| `ffplay` (FFmpeg) | Audio/video playback in libraries |
| `hdl_dump` | PSX internal HDD operations |
| `pppwn` | PS4 PPPwn exploit |
| `pkg2zip` | PSVita PKG extraction |
| `pkg_merge` | PKG file merging |
| `pup_unpacker` | PS4/PS5 PUP firmware unpacking |
| `rcomage` | PS3/PSV RCO resource tool |
| `sngre` | PS Vita RCO extractor |
| `psvpfstools` | PS Vita PFS file system |
| `make_fself` / `make_fself_python3-1` | PS5 SELF fake-signing |
| `maxcso` / `mCiso` | PSP CSO compression |
| `scetool` / `costool` | PS3 SELF decryption |
| `ps3dec` | PS3 ISO decryption |
| `ps3mca-tool` | PS2/PS3 Memory Card access |
| `send_elf` | PS5 ELF payload sender |

**Linux / macOS note:** Some Windows-only tools run through `wine`. Users must have installed:
```
winetricks vcrun2008 vcrun2010 vcrun2012
```
and all platform-specific system packages listed in `README.md`.

---

## 8. Feature Inventory

All tools listed below must remain accessible after any GUI redesign. Use `README.md` as the canonical feature list.

| Platform | Count | Key tools |
|---|---|---|
| PS1 | 2 | BIN/CUE→ISO, Merge BIN |
| PS2 | 5 | BIN/CUE, CUE2POPS, ELF2KELF, STAR Extractor, PAKerUtility |
| PSX | 4 | HDD Partition Manager, XMB Files Explorer, PS2/PS1 game install |
| PS3 | 20+ | ISO tools, PKG extractor/info, PUP unpacker, webMAN, RCO, SELF reader |
| PS4 | 11 | PPPwn, PKG tools, payload sender, fPKG creators (PS1/PS2/PSP), USB writer |
| PS5 | 25+ | PKG builder/merger/extractor, FTP grabber, param/manifest editors, SELF tools, game patches |
| PSP | 4 | CSO decompress, ISO↔CSO, PBP pack/unpack |
| PSVita | 5 | PKG extractor, PKG info, PFS tools, RCO extractor |
| Memory Cards | 6 | PS2 MC browse/add/extract/remove/format/FMCB install |

---

## 9. Documentation Self-Maintenance

Agents **must update this file** when any of the following occur:

| Trigger | Section(s) to update |
|---|---|
| New top-level source folder added | §4 File Structure Map |
| New utility added to `Classes/` | §4 (add entry), §1 (add to "check before creating" note) |
| New reusable dialog added to `Dialogs/` | §4 and §1 |
| New platform module or tool added | §4, §8 Feature Inventory |
| Tech stack version bumped | §6 Tech Stack table |
| New NuGet package added | §6 |
| Namespace or routing changed | §2 Architecture, §4 |
| New entry-point file extension added | §2 Drag-drop routing table |

**On every release:**
1. Update `<Version>` in `PSMultiTools/PSMultiTools.csproj`
2. Update `LatestBuild.txt`
3. Update the feature list in `README.md` if tools were added or removed

---

## 10. Verification Checklist

Run these steps after any non-trivial change before considering work complete:

```bash
# Build
dotnet build PSMultiTools/PSMultiTools.csproj

# Run
dotnet run --project PSMultiTools/PSMultiTools.csproj
```

Manual smoke tests:
- [ ] MainWindow opens with dark theme (no light-mode flash)
- [ ] All 8 console icons (PS1, PS2, PSX, PS3, PS4, PS5, PSP, PSVita) are clickable and open their Library windows
- [ ] Drag a `.pkg` file onto the app → `PKGInfo` window opens
- [ ] Settings window opens without errors

Cross-platform notes:
- Windows is the primary test target
- Linux / macOS require the native libraries in `Libraries/` and Wine-based tools
- FreeBSD requires Linux binary compatibility (`sysrc linux_enable="YES"`)

---

## 11. Transitional 2026 Web UI Stack (In Progress)

The repository now also contains a staged UI migration scaffold:

```
PS-Multi-Tools-Evolved/
├── apps/
│   ├── desktop-electron/      # Electron desktop shell that starts local ApiHost
│   └── web-ui/                # React + MUI frontend
└── src/
    ├── PSMultiTools.Core/         # Shared DTOs/contracts for ApiHost + UI
    ├── PSMultiTools.Infrastructure/ # Config/services/process wrappers
    └── PSMultiTools.ApiHost/      # Local HTTP API host
```

Added stack components for the migration layer:
- Electron 40+
- React 19
- MUI 7
- ASP.NET Core Minimal API (.NET 10)

Important: the existing Avalonia app remains the current production UI until parity is complete.

Generated artifacts hygiene for this migration layer:
- Do not commit `apps/**/dist` or `apps/**/.vite`.
- Do not commit `*.tsbuildinfo`.
- Do not commit local runtime config from ApiHost (`src/PSMultiTools.ApiHost/psmt-config.ini`).
