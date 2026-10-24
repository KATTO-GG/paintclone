using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using IOPath = System.IO.Path;
using PaintNetClone.Core.Document;
using PaintNetClone.Core.Geometry;
using PaintNetClone.Core.Rendering;
using PaintNetClone.Core.Color;
using PaintNetClone.Layers;
using PaintNetClone.Tools;
using PaintNetClone.Effects;
using PaintNetClone.FileFormats;

namespace PaintNetClone
{
    public partial class MainWindow : Window
    {
        private Document? _document;
        private Tool? _activeTool;
        private string _activeToolName = "PaintBrush";
        private WriteableBitmap? _writeableBitmap;
        private double _zoom = 1.0;
        private PointInt32 _lastMousePos;
        private bool _isMouseDown;
        private MouseButtons _mouseButtons;
        private KeyModifiers _keyModifiers;
        private Surface? _renderedSurface;
        private readonly ObservableCollection<LayerViewModel> _layerViewModels = new();
        private readonly List<string> _history = new();
        private string _currentFilePath = "";
        private bool _isUpdatingUI;

        // Colors
        private ColorBgra _primaryColor = ColorBgra.Black;
        private ColorBgra _secondaryColor = ColorBgra.White;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTools();
            InitializeColorUI();
            InitializeBlendModes();
            NewDocument(800, 600);
            UpdateWindowTitle();
        }

        private void InitializeTools()
        {
            foreach (var toolName in ToolRegistry.ToolNames)
            {
                ToolComboBox.Items.Add(toolName);
            }
            ToolComboBox.SelectedItem = _activeToolName;
            SelectTool(_activeToolName);
        }

        private void InitializeColorUI()
        {
            UpdateColorPreviews();

            // Populate palette
            var paletteColors = new[]
            {
                ColorBgra.Black, ColorBgra.White, ColorBgra.Red, ColorBgra.Green, ColorBgra.Blue,
                ColorBgra.Yellow, ColorBgra.Cyan, ColorBgra.Magenta,
                new ColorBgra(128, 128, 128), new ColorBgra(192, 192, 192),
                new ColorBgra(255, 128, 0), new ColorBgra(128, 0, 128),
                new ColorBgra(0, 128, 128), new ColorBgra(128, 128, 0),
                new ColorBgra(255, 0, 128), new ColorBgra(128, 255, 0)
            };

            foreach (var color in paletteColors)
            {
                var border = new Border
                {
                    Width = 24,
                    Height = 24,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B)),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    ToolTip = $"#{color.R:X2}{color.G:X2}{color.B:X2}",
                    Tag = color
                };
                border.MouseLeftButtonDown += PaletteColor_MouseLeftButtonDown;
                border.MouseRightButtonDown += PaletteColor_MouseRightButtonDown;
                ColorPalettePanel.Children.Add(border);
            }
        }

        private void InitializeBlendModes()
        {
            var blendModes = Enum.GetValues(typeof(BlendMode)).Cast<BlendMode>().ToList();
            BlendModeComboBox.ItemsSource = blendModes;
            BlendModeComboBox.SelectedItem = BlendMode.Normal;
            LayerBlendModeComboBox.ItemsSource = blendModes;
            LayerBlendModeComboBox.SelectedItem = BlendMode.Normal;
        }

        private void SelectTool(string toolName)
        {
            _activeTool?.Deactivate();
            _activeTool = ToolRegistry.Create(toolName);
            _activeToolName = toolName;
            _activeTool.StatusChanged += Tool_StatusChanged;

            if (_document != null)
            {
                var activeLayer = GetActiveBitmapLayer();
                if (activeLayer != null)
                {
                    _activeTool.Activate(_document, activeLayer);
                }
            }

            ToolComboBox.SelectedItem = toolName;
            UpdateToolPropertiesUI();
        }

        private BitmapLayer? GetActiveBitmapLayer()
        {
            if (_document == null) return null;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer is BitmapLayer selectedBmp) return selectedBmp;

            // Find first visible bitmap layer from top
            foreach (var layer in _document.Layers.Reverse())
            {
                if (layer.Visible && layer is BitmapLayer bmp) return bmp;
                if (layer is GroupLayer group)
                {
                    var found = FindBitmapLayerInGroup(group);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private BitmapLayer? FindBitmapLayerInGroup(GroupLayer group)
        {
            foreach (var child in group.Children.Reverse())
            {
                if (child.Visible && child is BitmapLayer bmp) return bmp;
                if (child is GroupLayer g)
                {
                    var found = FindBitmapLayerInGroup(g);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private void UpdateToolPropertiesUI()
        {
            _isUpdatingUI = true;

            if (_activeTool is BrushToolBase brushTool)
            {
                BrushSizeSlider.Value = brushTool.BrushSize;
                BrushSizeTextBox.Text = brushTool.BrushSize.ToString("F1");
                BrushHardnessSlider.Value = brushTool.BrushHardness * 100;
                BrushOpacitySlider.Value = brushTool.BrushOpacity * 100;
                BlendModeComboBox.SelectedItem = brushTool.BlendMode;
            }
            else if (_activeTool is FillTool fillTool)
            {
                // Fill tool specific properties
            }
            else if (_activeTool is GradientTool gradTool)
            {
                // Gradient tool specific properties
            }

            _isUpdatingUI = false;
        }

        private void Tool_StatusChanged(object? sender, ToolEventArgs e)
        {
            Dispatcher.Invoke(() => StatusText.Text = e.Message);
        }

        private void NewDocument(int width, int height)
        {
            _document?.Dispose();
            _document = new Document(width, height);
            _document.PropertyChanged += Document_PropertyChanged;

            var bgLayer = _document.AddBitmapLayer("Background");
            bgLayer.IsBackground = true;
            bgLayer.Surface?.Fill(ColorBgra.White);

            _layerViewModels.Clear();
            RebuildLayerViewModels();

            _history.Clear();
            AddHistoryEntry("New Document");
            UpdateHistoryUI();

            RenderCanvas();
            UpdateWindowTitle();
        }

        private void Document_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Document.Layers) || e.PropertyName == nameof(Document.LayerCount))
            {
                Dispatcher.Invoke(RebuildLayerViewModels);
            }
        }

        private void RebuildLayerViewModels()
        {
            _layerViewModels.Clear();
            if (_document == null) return;

            foreach (var layer in _document.Layers.Reverse()) // Top to bottom
            {
                AddLayerViewModel(layer, 0);
            }

            LayersListView.ItemsSource = _layerViewModels;
        }

        private void AddLayerViewModel(Layer layer, int indent)
        {
            var vm = new LayerViewModel(layer, indent);
            vm.PropertyChanged += LayerViewModel_PropertyChanged;
            _layerViewModels.Add(vm);

            if (layer is GroupLayer group)
            {
                foreach (var child in group.Children.Reverse())
                {
                    AddLayerViewModel(child, indent + 1);
                }
            }
        }

        private void LayerViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isUpdatingUI) return;

            if (sender is LayerViewModel vm)
            {
                if (e.PropertyName == nameof(LayerViewModel.Visible))
                {
                    vm.Layer.Visible = vm.Visible;
                    RenderCanvas();
                }
                else if (e.PropertyName == nameof(LayerViewModel.Opacity))
                {
                    vm.Layer.Opacity = vm.Opacity;
                    RenderCanvas();
                }
                else if (e.PropertyName == nameof(LayerViewModel.BlendMode))
                {
                    vm.Layer.BlendMode = vm.BlendMode;
                    RenderCanvas();
                }
                else if (e.PropertyName == nameof(LayerViewModel.Name))
                {
                    vm.Layer.Name = vm.Name;
                }
            }
        }

        private void RenderCanvas()
        {
            if (_document == null) return;

            _renderedSurface?.Dispose();
            _renderedSurface = _document.Render();

            if (_writeableBitmap == null ||
                _writeableBitmap.PixelWidth != _renderedSurface.Width ||
                _writeableBitmap.PixelHeight != _renderedSurface.Height)
            {
                _writeableBitmap = new WriteableBitmap(
                    _renderedSurface.Width, _renderedSurface.Height, 96, 96,
                    PixelFormats.Bgra32, null);
                CanvasImage.Source = _writeableBitmap;
            }

            _writeableBitmap.Lock();
            try
            {
                unsafe
                {
                    var ptr = (byte*)_writeableBitmap.BackBuffer;
                    int stride = _writeableBitmap.BackBufferStride;

                    for (int y = 0; y < _renderedSurface.Height; y++)
                    {
                        byte* row = ptr + y * stride;
                        var surfaceRow = _renderedSurface.GetRowSpan(y);
                        for (int x = 0; x < _renderedSurface.Width; x++)
                        {
                            var p = surfaceRow[x];
                            row[x * 4] = p.B;
                            row[x * 4 + 1] = p.G;
                            row[x * 4 + 2] = p.R;
                            row[x * 4 + 3] = p.A;
                        }
                    }
                }
                _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _renderedSurface.Width, _renderedSurface.Height));
            }
            finally
            {
                _writeableBitmap.Unlock();
            }

            UpdateRulers();
        }

        private void UpdateRulers()
        {
            if (_renderedSurface == null) return;

            // Top ruler
            TopRulerCanvas.Children.Clear();
            double rulerWidth = _renderedSurface.Width * _zoom;
            for (int x = 0; x <= _renderedSurface.Width; x += 50)
            {
                double px = x * _zoom;
                var line = new Line { X1 = px, Y1 = 15, X2 = px, Y2 = 20, Stroke = Brushes.Black, StrokeThickness = 1 };
                TopRulerCanvas.Children.Add(line);
                var text = new TextBlock { Text = x.ToString(), FontSize = 8, Foreground = Brushes.Black };
                Canvas.SetLeft(text, px + 2);
                Canvas.SetTop(text, 0);
                TopRulerCanvas.Children.Add(text);
            }

            // Left ruler
            LeftRulerCanvas.Children.Clear();
            for (int y = 0; y <= _renderedSurface.Height; y += 50)
            {
                double py = y * _zoom;
                var line = new Line { X1 = 15, Y1 = py, X2 = 20, Y2 = py, Stroke = Brushes.Black, StrokeThickness = 1 };
                LeftRulerCanvas.Children.Add(line);
                var text = new TextBlock { Text = y.ToString(), FontSize = 8, Foreground = Brushes.Black };
                Canvas.SetLeft(text, 0);
                Canvas.SetTop(text, py + 2);
                LeftRulerCanvas.Children.Add(text);
            }
        }

        private void AddHistoryEntry(string entry)
        {
            _history.Insert(0, $"{DateTime.Now:HH:mm:ss} - {entry}");
            if (_history.Count > 50) _history.RemoveAt(_history.Count - 1);
            UpdateHistoryUI();
        }

        private void UpdateHistoryUI()
        {
            HistoryListView.ItemsSource = null;
            HistoryListView.ItemsSource = _history;
        }

        private void UpdateWindowTitle()
        {
            string title = "Paint.NET Clone";
            if (!string.IsNullOrEmpty(_currentFilePath))
                title = $"{IOPath.GetFileName(_currentFilePath)} - {title}";
            if (_document != null)
                title += $" ({_document.Width}x{_document.Height})";
            Title = title;
        }

        private void UpdateColorPreviews()
        {
            if (PrimaryColorPreview == null || PrimaryColorHex == null || SecondaryColorPreview == null || SecondaryColorHex == null) return;
            if (RedSlider == null || RedTextBox == null || GreenSlider == null || GreenTextBox == null ||
                BlueSlider == null || BlueTextBox == null || AlphaSlider == null || AlphaTextBox == null) return;
            if (HueSlider == null || HueTextBox == null || SaturationSlider == null || SaturationTextBox == null ||
                ValueSlider == null || ValueTextBox == null) return;

            PrimaryColorPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_primaryColor.A, _primaryColor.R, _primaryColor.G, _primaryColor.B));
            PrimaryColorHex.Text = $"#{_primaryColor.R:X2}{_primaryColor.G:X2}{_primaryColor.B:X2}";
            SecondaryColorPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(_secondaryColor.A, _secondaryColor.R, _secondaryColor.G, _secondaryColor.B));
            SecondaryColorHex.Text = $"#{_secondaryColor.R:X2}{_secondaryColor.G:X2}{_secondaryColor.B:X2}";

            // Update sliders
            _isUpdatingUI = true;
            RedSlider.Value = _primaryColor.R; RedTextBox.Text = _primaryColor.R.ToString();
            GreenSlider.Value = _primaryColor.G; GreenTextBox.Text = _primaryColor.G.ToString();
            BlueSlider.Value = _primaryColor.B; BlueTextBox.Text = _primaryColor.B.ToString();
            AlphaSlider.Value = _primaryColor.A; AlphaTextBox.Text = _primaryColor.A.ToString();

            var hsv = _primaryColor.ToHsv();
            HueSlider.Value = hsv.H; HueTextBox.Text = ((int)hsv.H).ToString();
            SaturationSlider.Value = hsv.S * 100; SaturationTextBox.Text = ((int)(hsv.S * 100)).ToString();
            ValueSlider.Value = hsv.V * 100; ValueTextBox.Text = ((int)(hsv.V * 100)).ToString();
            _isUpdatingUI = false;
        }

        // Event handlers
        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new NewDocumentDialog();
            if (dialog.ShowDialog() == true)
            {
                NewDocument(dialog.Width, dialog.Height);
            }
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tiff;*.tif;*.gif;*.pdnc|All Files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _document?.Dispose();
                    _document = FileFormat.LoadFromFile(dialog.FileName);
                    _document.PropertyChanged += Document_PropertyChanged;
                    _currentFilePath = dialog.FileName;

                    RebuildLayerViewModels();
                    _history.Clear();
                    AddHistoryEntry($"Opened {IOPath.GetFileName(dialog.FileName)}");
                    UpdateHistoryUI();

                    RenderCanvas();
                    UpdateWindowTitle();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsCommand_Executed(sender, e);
            }
            else
            {
                SaveToFile(_currentFilePath);
            }
        }

        private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Paint.NET Clone|*.pdnc|PNG|*.png|JPEG|*.jpg|BMP|*.bmp|TIFF|*.tiff|GIF|*.gif",
                DefaultExt = ".pdnc"
            };
            if (dialog.ShowDialog() == true)
            {
                SaveToFile(dialog.FileName);
                _currentFilePath = dialog.FileName;
                UpdateWindowTitle();
            }
        }

        private void SaveToFile(string path)
        {
            try
            {
                FileFormat.SaveToFile(_document!, path);
                AddHistoryEntry($"Saved {IOPath.GetFileName(path)}");
                UpdateHistoryUI();
                StatusText.Text = "Saved successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _document?.Undo();
            RenderCanvas();
            RebuildLayerViewModels();
            AddHistoryEntry("Undo");
        }

        private void UndoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _document?.CanUndo == true;
        }

        private void RedoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _document?.Redo();
            RenderCanvas();
            RebuildLayerViewModels();
            AddHistoryEntry("Redo");
        }

        private void RedoCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _document?.CanRedo == true;
        }

        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e) { }
        private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e) { }
        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e) { }
        private void SelectAllCommand_Executed(object sender, ExecutedRoutedEventArgs e) { }
        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e) { }

        private void Exit_Click(object sender, RoutedEventArgs e) => Close();

        private void Preferences_Click(object sender, RoutedEventArgs e) { }

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.25);
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.25);
        private void ActualSize_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);
        private void FitToWindow_Click(object sender, RoutedEventArgs e) { /* TODO */ }

        private void ToggleRulers_Click(object sender, RoutedEventArgs e)
        {
            var mi = (MenuItem)sender;
            TopRuler.Visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
            LeftRuler.Visibility = mi.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleGrid_Click(object sender, RoutedEventArgs e) { }
        private void TogglePixelGrid_Click(object sender, RoutedEventArgs e) { }

        private void ResizeImage_Click(object sender, RoutedEventArgs e) { }
        private void CanvasSize_Click(object sender, RoutedEventArgs e) { }
        private void CropToSelection_Click(object sender, RoutedEventArgs e) { }
        private void FlipHorizontal_Click(object sender, RoutedEventArgs e) { }
        private void FlipVertical_Click(object sender, RoutedEventArgs e) { }
        private void Rotate90Cw_Click(object sender, RoutedEventArgs e) { }
        private void Rotate90Ccw_Click(object sender, RoutedEventArgs e) { }
        private void Rotate180_Click(object sender, RoutedEventArgs e) { }
        private void Flatten_Click(object sender, RoutedEventArgs e) { }

        private void AddNewLayer_Click(object sender, RoutedEventArgs e) => AddLayerButton_Click(sender, e);
        private void AddGroup_Click(object sender, RoutedEventArgs e) => AddGroupButton_Click(sender, e);
        private void DuplicateLayer_Click(object sender, RoutedEventArgs e) => DuplicateLayerButton_Click(sender, e);
        private void DeleteLayer_Click(object sender, RoutedEventArgs e) => DeleteLayerButton_Click(sender, e);
        private void MoveLayerUp_Click(object sender, RoutedEventArgs e) => MoveLayerUpButton_Click(sender, e);
        private void MoveLayerDown_Click(object sender, RoutedEventArgs e) => MoveLayerDownButton_Click(sender, e);
        private void LayerProperties_Click(object sender, RoutedEventArgs e) { }
        private void MergeDown_Click(object sender, RoutedEventArgs e) => MergeDownButton_Click(sender, e);
        private void MergeLayerDown_Click(object sender, RoutedEventArgs e) { }

        private void BrightnessContrast_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new BrightnessContrastEffect());
        private void HueSaturationLightness_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new HueSaturationLightnessEffect());
        private void Levels_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new LevelsEffect());
        private void InvertColors_Click(object sender, RoutedEventArgs e) => ApplyEffect(new InvertColorsEffect());
        private void Grayscale_Click(object sender, RoutedEventArgs e) => ApplyEffect(new GrayscaleEffect());
        private void Sepia_Click(object sender, RoutedEventArgs e) => ApplyEffect(new SepiaEffect());
        private void AutoLevel_Click(object sender, RoutedEventArgs e) { }

        private void GaussianBlur_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new GaussianBlurEffect());
        private void MotionBlur_Click(object sender, RoutedEventArgs e) { }
        private void RadialBlur_Click(object sender, RoutedEventArgs e) { }
        private void ZoomBlur_Click(object sender, RoutedEventArgs e) { }
        private void Bulge_Click(object sender, RoutedEventArgs e) { }
        private void Twist_Click(object sender, RoutedEventArgs e) { }
        private void Dents_Click(object sender, RoutedEventArgs e) { }
        private void FrostedGlass_Click(object sender, RoutedEventArgs e) { }
        private void AddNoise_Click(object sender, RoutedEventArgs e) { }
        private void Median_Click(object sender, RoutedEventArgs e) { }
        private void Emboss_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new EmbossEffect());
        private void OilPainting_Click(object sender, RoutedEventArgs e) => ShowEffectDialog(new OilPaintingEffect());
        private void PencilSketch_Click(object sender, RoutedEventArgs e) { }

        private void SelectTool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string toolName)
            {
                SelectTool(toolName);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Paint.NET Clone\nBuilt with .NET 9 and WPF", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ToolComboBox.SelectedItem is string toolName && !_isUpdatingUI)
            {
                SelectTool(toolName);
            }
        }

        private void BrushSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            if (_activeTool is BrushToolBase brush)
            {
                brush.BrushSize = (float)e.NewValue;
                BrushSizeTextBox.Text = e.NewValue.ToString("F1");
            }
        }

        private void BrushSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (double.TryParse(BrushSizeTextBox.Text, out double val) && _activeTool is BrushToolBase brush)
            {
                brush.BrushSize = (float)Math.Clamp(val, 1, 500);
                BrushSizeSlider.Value = brush.BrushSize;
            }
        }

        private void BrushHardnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            if (_activeTool is BrushToolBase brush)
            {
                brush.BrushHardness = (float)(e.NewValue / 100.0);
            }
        }

        private void BrushOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            if (_activeTool is BrushToolBase brush)
            {
                brush.BrushOpacity = (float)(e.NewValue / 100.0);
            }
        }

        private void BlendModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (_activeTool is BrushToolBase brush && BlendModeComboBox.SelectedItem is BlendMode mode)
            {
                brush.BlendMode = mode;
            }
        }

        private void AddLayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null) return;
            var layer = _document.AddBitmapLayer($"Layer {_document.LayerCount + 1}");
            AddHistoryEntry($"Added layer: {layer.Name}");
            RenderCanvas();
        }

        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null) return;
            var group = _document.AddGroupLayer($"Group {_document.LayerCount + 1}");
            AddHistoryEntry($"Added group: {group.Name}");
            RenderCanvas();
        }

        private void DuplicateLayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null) return;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer is BitmapLayer bmp)
            {
                var clone = new BitmapLayer($"{bmp.Name} copy", bmp.Surface!.Clone());
                int index = _document.Layers.ToList().IndexOf(bmp);
                _document.AddLayer(clone, index + 1);
                AddHistoryEntry($"Duplicated layer: {bmp.Name}");
                RenderCanvas();
            }
            else if (selected?.Layer is GroupLayer group)
            {
                // TODO: Duplicate group
            }
        }

        private void DeleteLayerButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer != null && _document != null)
            {
                if (selected.Layer.IsBackground) return;
                string name = selected.Layer.Name;
                _document.RemoveLayer(selected.Layer);
                AddHistoryEntry($"Deleted layer: {name}");
                RenderCanvas();
            }
        }

        private void MoveLayerUpButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer != null && _document != null)
            {
                int idx = _document.Layers.ToList().IndexOf(selected.Layer);
                if (idx > 0)
                {
                    _document.MoveLayer(selected.Layer, idx - 1);
                    RenderCanvas();
                }
            }
        }

        private void MoveLayerDownButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer != null && _document != null)
            {
                int idx = _document.Layers.ToList().IndexOf(selected.Layer);
                if (idx < _document.LayerCount - 1)
                {
                    _document.MoveLayer(selected.Layer, idx + 1);
                    RenderCanvas();
                }
            }
        }

        private void MergeDownButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer is BitmapLayer bmp && _document != null)
            {
                int idx = _document.Layers.ToList().IndexOf(bmp);
                if (idx < _document.LayerCount - 1)
                {
                    var below = _document.Layers[idx + 1];
                    if (below is BitmapLayer belowBmp)
                    {
                        var merged = belowBmp.Surface!.Clone();
                        merged.CopyFrom(bmp.Surface!);
                        belowBmp.SetSurface(merged);
                        _document.RemoveLayer(bmp);
                        AddHistoryEntry($"Merged down: {bmp.Name}");
                        RenderCanvas();
                    }
                }
            }
        }

        private void FlattenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_document == null) return;
            var flattened = _document.Render();
            foreach (var layer in _document.Layers.ToArray())
            {
                if (!layer.IsBackground) _document.RemoveLayer(layer);
            }
            var bgLayer = _document.Layers.OfType<BitmapLayer>().FirstOrDefault();
            if (bgLayer != null) bgLayer.SetSurface(flattened);
            else _document.AddBitmapLayer("Background").SetSurface(flattened);
            AddHistoryEntry("Flattened image");
            RenderCanvas();
        }

        private void LayersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected?.Layer is BitmapLayer bmp && _activeTool != null)
            {
                _activeTool.Deactivate();
                _activeTool.Activate(_document!, bmp);
                UpdateLayerPropertiesUI(selected);
            }
        }

        private void UpdateLayerPropertiesUI(LayerViewModel vm)
        {
            _isUpdatingUI = true;
            LayerOpacitySlider.Value = vm.Layer.Opacity * 100;
            LayerBlendModeComboBox.SelectedItem = vm.Layer.BlendMode;
            LayerVisibleCheckBox.IsChecked = vm.Layer.Visible;
            _isUpdatingUI = false;
        }

        private void LayersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected != null)
            {
                // Rename layer
            }
        }

        private void LayersListView_ContextMenuOpening(object sender, ContextMenuEventArgs e) { }

        private void VisibilityCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is LayerViewModel vm)
            {
                vm.Visible = true;
            }
        }

        private void VisibilityCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is LayerViewModel vm)
            {
                vm.Visible = false;
            }
        }

        private void LayerOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected != null)
            {
                selected.Opacity = e.NewValue / 100.0;
            }
        }

        private void LayerBlendModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected != null && LayerBlendModeComboBox.SelectedItem is BlendMode mode)
            {
                selected.BlendMode = mode;
            }
        }

        private void LayerVisibleCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected != null) selected.Visible = true;
        }

        private void LayerVisibleCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var selected = LayersListView.SelectedItem as LayerViewModel;
            if (selected != null) selected.Visible = false;
        }

        private void PrimaryColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorPickerDialog(_primaryColor);
            if (dialog.ShowDialog() == true)
            {
                _primaryColor = dialog.SelectedColor;
                UpdateColorPreviews();
                if (_activeTool is BrushToolBase) { /* brush uses PrimaryColor property */ }
            }
        }

        private void SecondaryColorPreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorPickerDialog(_secondaryColor);
            if (dialog.ShowDialog() == true)
            {
                _secondaryColor = dialog.SelectedColor;
                UpdateColorPreviews();
            }
        }

        private void PaletteColor_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is ColorBgra color)
            {
                _primaryColor = color;
                UpdateColorPreviews();
            }
        }

        private void PaletteColor_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border b && b.Tag is ColorBgra color)
            {
                _secondaryColor = color;
                UpdateColorPreviews();
            }
        }

        private void SwapColorsButton_Click(object sender, RoutedEventArgs e)
        {
            (_primaryColor, _secondaryColor) = (_secondaryColor, _primaryColor);
            UpdateColorPreviews();
        }

        private void ResetColorsButton_Click(object sender, RoutedEventArgs e)
        {
            _primaryColor = ColorBgra.Black;
            _secondaryColor = ColorBgra.White;
            UpdateColorPreviews();
        }

        private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            byte r = (byte)RedSlider.Value;
            byte g = (byte)GreenSlider.Value;
            byte b = (byte)BlueSlider.Value;
            byte a = (byte)AlphaSlider.Value;
            _primaryColor = new ColorBgra(r, g, b, a);
            UpdateColorPreviews();
        }

        private void RgbTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (RedTextBox == null || GreenTextBox == null || BlueTextBox == null || AlphaTextBox == null) return;
            if (byte.TryParse(RedTextBox.Text, out byte r) &&
                byte.TryParse(GreenTextBox.Text, out byte g) &&
                byte.TryParse(BlueTextBox.Text, out byte b) &&
                byte.TryParse(AlphaTextBox.Text, out byte a))
            {
                _primaryColor = new ColorBgra(r, g, b, a);
                UpdateColorPreviews();
            }
        }

        private void HsvSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingUI) return;
            double h = HueSlider.Value;
            double s = SaturationSlider.Value / 100.0;
            double v = ValueSlider.Value / 100.0;
            _primaryColor = ColorBgra.FromHsv(h, s, v, _primaryColor.A / 255.0);
            UpdateColorPreviews();
        }

        private void HsvTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingUI) return;
            if (HueTextBox == null || SaturationTextBox == null || ValueTextBox == null) return;
            if (double.TryParse(HueTextBox.Text, out double h) &&
                double.TryParse(SaturationTextBox.Text, out double s) &&
                double.TryParse(ValueTextBox.Text, out double v))
            {
                _primaryColor = ColorBgra.FromHsv(h, s / 100.0, v / 100.0, _primaryColor.A / 255.0);
                UpdateColorPreviews();
            }
        }

        private void CanvasScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateRulers();
        }

        private void CanvasScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                double factor = e.Delta > 0 ? 1.1 : 1 / 1.1;
                SetZoom(_zoom * factor);
                e.Handled = true;
            }
        }

        private void CanvasImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            CanvasScrollViewer_MouseWheel(sender, e);
        }

        private void CanvasImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _mouseButtons = MouseButtons.Left;
            _keyModifiers = GetKeyModifiers();
            _lastMousePos = GetCanvasPoint(e.GetPosition(CanvasImage));
            CanvasImage.CaptureMouse();

            _activeTool?.OnMouseDown(_lastMousePos, _mouseButtons, _keyModifiers);
            RenderCanvas();
        }

        private void CanvasImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var pos = GetCanvasPoint(e.GetPosition(CanvasImage));
            _activeTool?.OnMouseUp(pos, _mouseButtons, _keyModifiers);
            _isMouseDown = false;
            _mouseButtons = MouseButtons.None;
            CanvasImage.ReleaseMouseCapture();
            RenderCanvas();
        }

        private void CanvasImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            _mouseButtons = MouseButtons.Right;
            _keyModifiers = GetKeyModifiers();
            _lastMousePos = GetCanvasPoint(e.GetPosition(CanvasImage));
            CanvasImage.CaptureMouse();

            _activeTool?.OnMouseDown(_lastMousePos, _mouseButtons, _keyModifiers);
            RenderCanvas();
        }

        private void CanvasImage_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var pos = GetCanvasPoint(e.GetPosition(CanvasImage));
            _activeTool?.OnMouseUp(pos, _mouseButtons, _keyModifiers);
            _isMouseDown = false;
            _mouseButtons = MouseButtons.None;
            CanvasImage.ReleaseMouseCapture();
            RenderCanvas();
        }

        private void CanvasImage_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = GetCanvasPoint(e.GetPosition(CanvasImage));
            CursorPositionText.Text = $"X: {pos.X}, Y: {pos.Y}";

            if (_isMouseDown)
            {
                _activeTool?.OnMouseMove(pos, _mouseButtons, _keyModifiers);
                RenderCanvas();
            }

            _lastMousePos = pos;
        }

        private PointInt32 GetCanvasPoint(Point wpfPoint)
        {
            if (_renderedSurface == null) return new PointInt32(0, 0);
            double x = wpfPoint.X / _zoom;
            double y = wpfPoint.Y / _zoom;
            return new PointInt32((int)Math.Round(x), (int)Math.Round(y));
        }

        private KeyModifiers GetKeyModifiers()
        {
            KeyModifiers mods = KeyModifiers.None;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= KeyModifiers.Shift;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= KeyModifiers.Control;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= KeyModifiers.Alt;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= KeyModifiers.Windows;
            return mods;
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Clamp(zoom, 0.1, 16.0);
            ZoomText.Text = $"{_zoom * 100:F0}%";
            ZoomSlider.Value = _zoom * 100;

            if (_writeableBitmap != null && _renderedSurface != null)
            {
                CanvasImage.Width = _renderedSurface.Width * _zoom;
                CanvasImage.Height = _renderedSurface.Height * _zoom;
            }
            UpdateRulers();
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isUpdatingUI)
            {
                SetZoom(e.NewValue / 100.0);
            }
        }

        private void ActualSizeButton_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

        private void ShowEffectDialog(Effect effect)
        {
            var dialog = effect.CreateConfigDialog();
            var window = new Window
            {
                Title = effect.Name,
                Content = CreateEffectConfigUI(dialog),
                Owner = this,
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize
            };
            if (window.ShowDialog() == true)
            {
                ApplyEffect(effect);
            }
        }

        private FrameworkElement CreateEffectConfigUI(EffectConfigDialog dialog)
        {
            // Simple panel with a text showing effect name and OK/Cancel buttons
            var panel = new StackPanel { Margin = new Thickness(20) };
            var txt = new TextBlock { Text = $"Effect: {dialog.Effect.Name}", Margin = new Thickness(0, 0, 0, 20) };
            panel.Children.Add(txt);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            okBtn.Click += (s, e) => { ((Window)panel.Parent!).DialogResult = true; };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            panel.Children.Add(buttons);

            return panel;
        }

        private void ApplyEffect(Effect effect)
        {
            if (_document == null) return;

            var activeLayer = GetActiveBitmapLayer();
            if (activeLayer?.Surface == null) return;

            var source = activeLayer.Surface;
            var dest = new Surface(source.Width, source.Height);

            var rois = new[] { source.Bounds };
            effect.Render(source, dest, rois, 0, 1);

            activeLayer.SetSurface(dest);
            AddHistoryEntry($"Applied effect: {effect.Name}");
            RenderCanvas();
        }

        protected override void OnClosed(EventArgs e)
        {
            _document?.Dispose();
            _renderedSurface?.Dispose();
            _activeTool?.Dispose();
            base.OnClosed(e);
        }
    }

    public class LayerViewModel : INotifyPropertyChanged
    {
        public Layer Layer { get; }
        public int Indent { get; }
        public IEnumerable<BlendMode> BlendModes => Enum.GetValues(typeof(BlendMode)).Cast<BlendMode>();

        private bool _visible;
        private double _opacity;
        private BlendMode _blendMode;
        private string _name;

        public bool Visible { get => _visible; set { _visible = value; OnPropertyChanged(); } }
        public double Opacity { get => _opacity; set { _opacity = value; OnPropertyChanged(); } }
        public BlendMode BlendMode { get => _blendMode; set { _blendMode = value; OnPropertyChanged(); } }
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

        public LayerViewModel(Layer layer, int indent)
        {
            Layer = layer;
            Indent = indent;
            _visible = layer.Visible;
            _opacity = layer.Opacity;
            _blendMode = layer.BlendMode;
            _name = layer.Name;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class NewDocumentDialog : Window
    {
        public int Width { get; private set; } = 800;
        public int Height { get; private set; } = 600;

        public NewDocumentDialog()
        {
            Title = "New Document";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new Label { Content = "Width:", Margin = new Thickness(0,0,0,4) });
            var widthBox = new TextBox { Text = "800", Margin = new Thickness(0,0,0,8) };
            Grid.SetRow(widthBox, 0);
            grid.Children.Add(widthBox);

            grid.Children.Add(new Label { Content = "Height:", Margin = new Thickness(0,8,0,4) });
            Grid.SetRow(new Label { Content = "Height:" }, 1);
            var heightBox = new TextBox { Text = "600", Margin = new Thickness(0,0,0,16) };
            Grid.SetRow(heightBox, 1);
            grid.Children.Add(heightBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0,0,8,0), IsDefault = true };
            okBtn.Click += (s, e) =>
            {
                if (int.TryParse(widthBox.Text, out int w) && int.TryParse(heightBox.Text, out int h))
                {
                    Width = Math.Clamp(w, 1, 10000);
                    Height = Math.Clamp(h, 1, 10000);
                    DialogResult = true;
                }
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }
    }

    public class ColorPickerDialog : Window
    {
        public ColorBgra SelectedColor { get; private set; }

        public ColorPickerDialog(ColorBgra initialColor)
        {
            SelectedColor = initialColor;
            Title = "Color Picker";
            SizeToContent = SizeToContent.WidthAndHeight;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Variables for color sliders
            byte alpha = initialColor.A;
            byte red = initialColor.R;
            byte green = initialColor.G;
            byte blue = initialColor.B;

            // Simple color sliders instead of ColorPicker
            var slidersPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            // Alpha slider
            var alphaSlider = CreateColorSlider("Alpha:", initialColor.A, 0, 255, val => alpha = val);
            slidersPanel.Children.Add(alphaSlider);

            // Red slider
            var redSlider = CreateColorSlider("Red:", initialColor.R, 0, 255, val => red = val);
            slidersPanel.Children.Add(redSlider);

            // Green slider
            var greenSlider = CreateColorSlider("Green:", initialColor.G, 0, 255, val => green = val);
            slidersPanel.Children.Add(greenSlider);

            // Blue slider
            var blueSlider = CreateColorSlider("Blue:", initialColor.B, 0, 255, val => blue = val);
            slidersPanel.Children.Add(blueSlider);

            grid.Children.Add(slidersPanel);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            okBtn.Click += (s, e) =>
            {
                SelectedColor = new ColorBgra(red, green, blue, alpha);
                DialogResult = true;
            };
            var cancelBtn = new Button { Content = "Cancel", Width = 80, IsCancel = true };
            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            Grid.SetRow(buttons, 1);
            grid.Children.Add(buttons);

            Content = grid;
        }

        private StackPanel CreateColorSlider(string label, byte initialValue, int min, int max, Action<byte> onValueChanged)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var lbl = new TextBlock { Text = label, Width = 50, VerticalAlignment = VerticalAlignment.Center };
            var slider = new Slider { Minimum = min, Maximum = max, Value = initialValue, Width = 200, Margin = new Thickness(8, 0, 8, 0) };
            var txtBox = new TextBox { Text = initialValue.ToString(), Width = 40, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };

            slider.ValueChanged += (s, e) =>
            {
                byte val = (byte)Math.Round(e.NewValue);
                txtBox.Text = val.ToString();
                onValueChanged(val);
            };

            txtBox.TextChanged += (s, e) =>
            {
                if (byte.TryParse(txtBox.Text, out byte val) && val >= min && val <= max)
                {
                    slider.Value = val;
                    onValueChanged(val);
                }
            };

            panel.Children.Add(lbl);
            panel.Children.Add(slider);
            panel.Children.Add(txtBox);
            return panel;
        }
    }
}