param(
    [string]$Source = (Join-Path $PSScriptRoot '../src/DayPlanner.UI/Assets/app-icon-source.png'),
    [string]$Output = (Join-Path $PSScriptRoot '../src/DayPlanner.UI/Assets/app-icon-source.png')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$drawingReferences = if ($PSVersionTable.PSEdition -eq 'Core') { @('System.Drawing.Common', 'System.Drawing.Primitives', 'System.Collections', 'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core') } else { @('System.Drawing', 'System') }
Add-Type -ReferencedAssemblies $drawingReferences -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
public static class IconBackground
{
    public static void Remove(string source, string destination)
    {
        int w, h;
        Color[] pixels;
        using (var input = new Bitmap(source))
        {
            w = input.Width; h = input.Height;
            pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) pixels[y * w + x] = input.GetPixel(x, y);
        }
        if (pixels[0].A == 0) throw new InvalidOperationException("The source already has transparency; use it directly.");
        var outside = new bool[pixels.Length];
        var queue = new Queue<int>();
        Action<int> visit = i => {
            if (outside[i]) return;
            var c = pixels[i];
            // Stop at the teal silhouette; enclosed white clock pixels are never visited.
            if (c.G - c.R > 70 && c.B - c.R > 70) return;
            outside[i] = true; queue.Enqueue(i);
        };
        for (int x = 0; x < w; x++) { visit(x); visit((h - 1) * w + x); }
        for (int y = 0; y < h; y++) { visit(y * w); visit(y * w + w - 1); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % w, y = i / w;
            if (x > 0) visit(i - 1); if (x + 1 < w) visit(i + 1);
            if (y > 0) visit(i - w); if (y + 1 < h) visit(i + w);
        }
        using (var output = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool boundary = false;
                for (int dy = -2; dy <= 2 && !boundary; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && outside[ny * w + nx] != outside[i]) { boundary = true; break; }
                    }
                Color c = pixels[i];
                if (!boundary) { output.SetPixel(x, y, outside[i] ? Color.Transparent : c); continue; }
                Color foreground = Color.Empty;
                for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    var f = pixels[ny * w + nx];
                    if (f.R < 40 && f.G > 90 && f.B > 90 && (foreground.IsEmpty || f.R < foreground.R)) foreground = f;
                }
                if (foreground.IsEmpty) { output.SetPixel(x, y, outside[i] ? Color.Transparent : c); continue; }
                // Recover edge coverage from teal chroma and remove the former light matte.
                double alpha = Math.Max(0, Math.Min(1, (c.G + c.B - 2.0 * c.R - 3) / (foreground.G + foreground.B - 2.0 * foreground.R - 3)));
                if (alpha < 0.04) { output.SetPixel(x, y, Color.Transparent); continue; }
                Func<int, int, int> unmatte = (v, bg) => (int)Math.Round(Math.Max(0, Math.Min(255, (v - bg * (1 - alpha)) / alpha)));
                output.SetPixel(x, y, Color.FromArgb((int)Math.Round(255 * alpha), unmatte(c.R, 244), unmatte(c.G, 244), unmatte(c.B, 247)));
            }
            output.Save(destination, ImageFormat.Png);
        }
    }
}
"@
[IconBackground]::Remove([IO.Path]::GetFullPath($Source), [IO.Path]::GetFullPath($Output))
Write-Output "Created transparent icon: $Output"



