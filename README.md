# PaintClone

A functional Paint.NET clone built with WPF (.NET 8). Features layer-based editing, 38 blend modes, 10 image effects, and full file format support.

## Features

- **Layer System**: Unlimited layers with 38 blend modes (Normal, Multiply, Screen, Overlay, Soft Light, Hard Light, Color Dodge, Color Burn, Darken, Lighten, Difference, Exclusion, Hue, Saturation, Color, Luminosity, and more)
- **Image Effects**: Gaussian Blur, Sharpen, Brightness/Contrast, Hue/Saturation/Lightness, Levels, Invert, Grayscale, Sepia, Emboss, Oil Painting
- **File Formats**: PNG, JPEG, BMP, TIFF, GIF, and custom `.pdnc` format (preserves layers)
- **Tools**: Brush, Eraser, Fill, Gradient, Selection (Rectangle, Ellipse, Lasso), Move, Text, Color Picker
- **Undo/Redo**: Full history with memento pattern
- **Custom Color Picker**: RGB/HSV sliders with hex input

## Screenshots

*(Add screenshots here)*

## Requirements

- Windows 10/11
- .NET 8 Runtime or SDK

## Installation

### Option 1: Download Release (Recommended)
1. Go to [Releases](https://github.com/KATTO-GG/paintclone/releases)
2. Download `PaintClone.zip` from the latest release
3. Extract and run `PaintClone.exe`

### Option 2: Build from Source
```bash
git clone https://github.com/KATTO-GG/paintclone.git
cd paintclone
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```
The executable will be at `./publish/PaintClone.exe`

## Usage

1. **New Document**: File → New (or Ctrl+N)
2. **Open Image**: File → Open (or Ctrl+O) — supports PNG, JPEG, BMP, TIFF, GIF
3. **Save**: File → Save (or Ctrl+S) — saves as `.pdnc` to preserve layers
4. **Export**: File → Export → Export As... — exports flattened image
5. **Layers**: Use the Layers panel (right) to add, delete, reorder, hide, and change blend modes
6. **Effects**: Effects menu → select effect → adjust parameters → OK
7. **Tools**: Select from the left toolbar; hold Shift for constraints (straight lines, perfect circles)

## Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| New | Ctrl+N |
| Open | Ctrl+O |
| Save | Ctrl+S |
| Save As | Ctrl+Shift+S |
| Undo | Ctrl+Z |
| Redo | Ctrl+Y |
| Copy | Ctrl+C |
| Paste | Ctrl+V |
| Cut | Ctrl+X |
| Select All | Ctrl+A |
| Deselect | Ctrl+D |
| Zoom In | Ctrl++ |
| Zoom Out | Ctrl+- |
| Fit to Window | Ctrl+0 |

## Project Structure

```
src/
├── Core/
│   ├── Color/          # ColorBgra, ColorRgba structs
│   ├── Document/       # Document model, rendering
│   ├── Geometry/       # Point, RectInt32
│   └── Rendering/      # Surface (WriteableBitmap wrapper)
├── Effects/            # All image effects
├── FileFormats/        # PNG, JPEG, BMP, TIFF, GIF, PDNC
├── Layers/             # Layer, BitmapLayer, GroupLayer, AdjustmentLayer
├── Tools/              # Brush, Eraser, Selection, Move, etc.
├── Themes/             # Dark theme resources
├── MainWindow.xaml     # Main UI
└── MainWindow.xaml.cs  # Code-behind
```

## Building Icons

```bash
cd GenerateIcons
dotnet run
```
Generates PNG icons (16-256px) and `app.ico` in `src/Icons/`.

## License

MIT License — free for personal and commercial use.

## Acknowledgments

Inspired by [Paint.NET](https://www.getpaint.net/) by Rick Brewster. This is an independent clone for educational purposes.