# Setup & Development Guide

## Prerequisites

- **Visual Studio 2022** (Community or higher) with ".NET desktop development" workload
- **OR** .NET 8 SDK + VS Code with C# Dev Kit
- Windows 10/11 (WPF is Windows-only)

## Quick Start

```bash
# Clone
git clone https://github.com/KATTO-GG/paintclone.git
cd paintclone

# Build
dotnet build -c Release

# Run
dotnet run --project src/PaintNetClone.csproj -c Release
```

## Visual Studio

1. Open `PaintNetClone.sln`
2. Set configuration to **Release** and platform to **x64**
3. Press F5 (Debug) or Ctrl+F5 (Release without debugger)

## Project Configuration

The project uses:
- .NET 8 (`net8.0-windows`)
- Nullable reference types enabled
- Implicit usings enabled
- Unsafe blocks allowed (for pixel manipulation)

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| System.Drawing.Common | 9.0.0 | Icon generation, GDI+ interop |

## Building a Distributable EXE

### Single-file, self-contained (recommended for releases)

```bash
dotnet publish src/PaintNetClone.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

Output: `./publish/PaintClone.exe` (~60-80 MB, no .NET runtime required)

### Framework-dependent (smaller, requires .NET 8 installed)

```bash
dotnet publish src/PaintNetClone.csproj -c Release -r win-x64 --self-contained false -o ./publish
```

Output: `./publish/PaintClone.exe` (~2-5 MB, needs .NET 8 Desktop Runtime)

## Creating a Release on GitHub

### Manual (via GitHub UI)

1. Go to repo → Releases → "Create a new release"
2. Tag: `v1.0.0` (or next version)
3. Title: `PaintClone v1.0.0`
4. Attach `PaintClone.zip` containing the published exe
5. Publish

### Automated (GitHub Actions)

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Build
        run: dotnet build -c Release
      - name: Publish
        run: |
          dotnet publish src/PaintNetClone.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
      - name: Zip
        run: Compress-Archive -Path ./publish/* -DestinationPath PaintClone.zip
      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: PaintClone.zip
```

Then push a tag: `git tag v1.0.0 && git push origin v1.0.0`

## Development Workflow

### Adding a New Effect

1. Create `src/Effects/MyEffect.cs` inheriting from `Effect`
2. Implement `OnRender(Surface dst, Surface src, RectInt32[] rois, int startIndex, int length)`
3. Register in `Effects.cs` → `EffectRegistry`
4. Add menu item in `MainWindow.xaml` → Effects menu
5. Handle in `MainWindow.xaml.cs` → `Effect_Click`

### Adding a New Tool

1. Create `src/Tools/MyTool.cs` inheriting from `Tool`
2. Implement `OnMouseDown`, `OnMouseMove`, `OnMouseUp`, `OnRender`
3. Register in `MainWindow.xaml.cs` → tool toolbar

### Adding a File Format

1. Create class in `src/FileFormats/` implementing `IFileFormat`
2. Implement `Load(Stream)`, `Save(Surface, Stream)`, `Extensions`
3. Register in `FileFormat.cs` → `FormatRegistry`

## Debugging Tips

- **Visual Studio**: Set breakpoints in effect `OnRender` loops — use "Parallel Watch" for pixel values
- **WriteableBitmap**: Use `BitmapSource.CopyPixels()` to inspect pixel data in Immediate Window
- **Layer bounds**: `RectInt32.Intersect/Union` — check `Document.Render()` for dirty rect logic
- **Memory**: Large images + many layers = high RAM; use `GC.GetTotalMemory(true)` to monitor

## Common Issues

| Issue | Fix |
|-------|-----|
| "ColorPicker not found" | We use custom RGB/HSV sliders — no external dependency |
| XAML parse error | Check `Generic.xaml` resource order (templates before styles) |
| NullReference in color UI | Controls initialize in `InitializeComponent()` — don't call `UpdateColorPreviews()` in constructor |
| Blend mode wrong | Verify `BlendMode` enum order matches `Layer.Blend()` switch |
| Effect slow | Use `Parallel.For` with `Partitioner.Create(0, length, 64)` for ROI processing |

## Architecture Notes

- **Single-threaded effects**: All effects run on UI thread (WPF requirement for WriteableBitmap)
- **Dirty rect rendering**: Only changed regions re-rendered via `Document.Invalidate(RectInt32)`
- **Memento undo**: `Document.Memento` captures layer stack serialization
- **Surface = WriteableBitmap wrapper**: Direct pixel access via `BackBuffer` pointer (unsafe)

## Testing Checklist

Before release, verify:
- [ ] New document creates 1 bitmap layer
- [ ] Open PNG/JPEG/BMP/TIFF/GIF works
- [ ] Save .pdnc preserves layers, blend modes, visibility
- [ ] Export flattens correctly
- [ ] All 10 effects apply without crash
- [ ] All 38 blend modes render correctly
- [ ] Undo/Redo works for: layer add/delete, paint strokes, effect application
- [ ] Tools: Brush, Eraser, Fill, Gradient, Selection, Move, Text, Color Picker
- [ ] Zoom/pan (mouse wheel + Ctrl, middle mouse drag)
- [ ] Layer reorder (drag-drop in panel)
- [ ] Icon shows in taskbar and window title