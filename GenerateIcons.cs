using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace GenerateIcons
{
    class Program
    {
        static void Main(string[] args)
        {
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Icons");
            Directory.CreateDirectory(outputDir);

            // Generate main app icon (256x256, 128x128, 64x64, 48x48, 32x32, 16x16)
            int[] sizes = { 256, 128, 64, 48, 32, 16 };

            foreach (int size in sizes)
            {
                using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);

                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                DrawPaintBrushIcon(graphics, size);

                string pngPath = Path.Combine(outputDir, $"icon_{size}.png");
                bitmap.Save(pngPath, ImageFormat.Png);
                Console.WriteLine($"Created {pngPath}");
            }

            // Create multi-resolution .ico file
            CreateIcoFile(outputDir, sizes);

            Console.WriteLine("Done!");
        }

        static void DrawPaintBrushIcon(Graphics g, int size)
        {
            float scale = size / 256f;
            int cx = size / 2;
            int cy = size / 2;

            // Background circle (dark)
            using (var bgBrush = new SolidBrush(Color.FromArgb(45, 45, 45))) // #2D2D2D
            {
                g.FillEllipse(bgBrush, 0, 0, size, size);
            }

            // Brush handle (blue)
            using (var handleBrush = new SolidBrush(Color.FromArgb(74, 144, 217))) // #4A90D9
            {
                var handlePath = new GraphicsPath();
                float handleWidth = 36 * scale;
                float handleHeight = 140 * scale;
                float handleX = cx - handleWidth / 2;
                float handleY = cy - handleHeight / 2 + 20 * scale;

                handlePath.AddRoundedRectangle(handleX, handleY, handleWidth, handleHeight, 8 * scale);
                g.FillPath(handleBrush, handlePath);
            }

            // Metal ferrule (silver/gray)
            using (var ferruleBrush = new LinearGradientBrush(
                new RectangleF(cx - 30 * scale, cy - 10 * scale, 60 * scale, 40 * scale),
                Color.FromArgb(200, 200, 200), Color.FromArgb(120, 120, 120), 90f))
            {
                var ferrulePath = new GraphicsPath();
                float ferruleTop = cy - 10 * scale;
                float ferruleHeight = 30 * scale;
                float ferruleWidthTop = 48 * scale;
                float ferruleWidthBottom = 40 * scale;

                ferrulePath.AddTrapezoid(cx - ferruleWidthTop / 2, ferruleTop, ferruleWidthTop, ferruleHeight, ferruleWidthBottom);
                g.FillPath(ferruleBrush, ferrulePath);

                // Ferrule highlight
                using (var highlightPen = new Pen(Color.FromArgb(230, 230, 230), 2 * scale))
                {
                    g.DrawLine(highlightPen, cx - ferruleWidthTop / 2 + 4 * scale, ferruleTop + 4 * scale, cx + ferruleWidthTop / 2 - 4 * scale, ferruleTop + 4 * scale);
                }
            }

            // Bristles (white with slight variation)
            using (var bristleBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
            {
                int bristleCount = Math.Max(5, (int)(14 * scale));
                float bristleWidth = 32 * scale / bristleCount;
                float bristleHeight = 50 * scale;
                float bristleTop = cy + 20 * scale;

                for (int i = 0; i < bristleCount; i++)
                {
                    float x = cx - 16 * scale + i * bristleWidth + bristleWidth / 2;
                    float variation = (float)(Math.Sin(i * 0.7) * 4 * scale);

                    using (var bristlePath = new GraphicsPath())
                    {
                        bristlePath.AddRoundedRectangle(x - bristleWidth / 2, bristleTop, bristleWidth * 0.8f, bristleHeight + variation, 1 * scale);
                        g.FillPath(bristleBrush, bristlePath);
                    }
                }
            }

            // Paint dab on bristles (blue accent)
            using (var paintBrush = new SolidBrush(Color.FromArgb(74, 144, 217)))
            {
                float dabWidth = 28 * scale;
                float dabHeight = 12 * scale;
                float dabX = cx - dabWidth / 2;
                float dabY = cy + 18 * scale;

                g.FillEllipse(paintBrush, dabX, dabY, dabWidth, dabHeight);
            }
        }

        static void CreateIcoFile(string outputDir, int[] sizes)
        {
            // Simple approach: save the largest as ICO (Windows can scale)
            // For a proper multi-res ICO, we'd need a more complex writer
            string largestPng = Path.Combine(outputDir, "icon_256.png");
            string icoPath = Path.Combine(outputDir, "app.ico");

            // Use System.Drawing to save as ICO (limited support, but works for basic case)
            using var bitmap = new Bitmap(largestPng);
            bitmap.Save(icoPath, ImageFormat.Icon);
            Console.WriteLine($"Created {icoPath}");
        }
    }

    // Extension methods for GraphicsPath
    public static class GraphicsPathExtensions
    {
        public static void AddRoundedRectangle(this GraphicsPath path, float x, float y, float width, float height, float radius)
        {
            float diameter = radius * 2;
            if (diameter > width) diameter = width;
            if (diameter > height) diameter = height;

            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
        }

        public static void AddTrapezoid(this GraphicsPath path, float x, float y, float topWidth, float height, float bottomWidth)
        {
            float topLeft = x;
            float topRight = x + topWidth;
            float bottomLeft = x + (topWidth - bottomWidth) / 2;
            float bottomRight = bottomLeft + bottomWidth;
            float bottomY = y + height;

            path.AddPolygon(new PointF[] {
                new PointF(topLeft, y),
                new PointF(topRight, y),
                new PointF(bottomRight, bottomY),
                new PointF(bottomLeft, bottomY)
            });
        }
    }
}