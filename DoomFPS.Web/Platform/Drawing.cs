using System.Drawing;
using System.Globalization;
using System.Text;

namespace DoomFPS.Web.Platform;

// Sustitutos mínimos de Graphics/Font/Brush/Pen de GDI+. Mantienen la misma
// forma de uso que GameForm, pero en vez de pintar en una ventana de Windows
// acumulan órdenes que sector09.js ejecuta sobre un <canvas> 2D.
public enum FontStyle { Regular, Bold }
public enum GraphicsUnit { Pixel }

public sealed class Font : IDisposable
{
    public Font(string family, int size, FontStyle style, GraphicsUnit unit)
    {
        Size = size; Bold = style == FontStyle.Bold;
        Css = $"{(Bold ? "bold " : "")}{size}px \"Segoe UI\", system-ui, -apple-system, Roboto, \"Helvetica Neue\", Arial, sans-serif";
    }
    public int Size { get; }
    public bool Bold { get; }
    public string Css { get; }
    public void Dispose() { }
}

public sealed class SolidBrush : IDisposable
{
    public SolidBrush(Color color) => Color = color;
    public Color Color { get; }
    public void Dispose() { }
}

public sealed class Pen : IDisposable
{
    public Pen(Color color, float width) { Color = color; Width = width; }
    public Color Color { get; }
    public float Width { get; }
    public void Dispose() { }
}

public sealed class Graphics
{
    private const char Field = '\u001f', Record = '\u001e';
    private readonly IPlatform platform;
    private readonly StringBuilder commands = new(8192);
    private readonly Dictionary<int, string> colorCache = new();
    private readonly Dictionary<(string, string), float> measureCache = new();

    public Graphics(IPlatform platform) => this.platform = platform;

    public void Begin() => commands.Clear();
    public string Flush() => commands.ToString();

    public void FillRectangle(SolidBrush brush, float x, float y, float width, float height)
    {
        if (width <= 0 || height <= 0 || brush.Color.A == 0) return;
        Start('R'); Num(x); Num(y); Num(width); Num(height); Str(Css(brush.Color));
    }

    public void FillEllipse(SolidBrush brush, float x, float y, float width, float height)
    {
        Start('E'); Num(x); Num(y); Num(width); Num(height); Str(Css(brush.Color));
    }

    public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
    {
        Start('L'); Num(x1); Num(y1); Num(x2); Num(y2); Num(pen.Width); Str(Css(pen.Color));
    }

    public void DrawString(string text, Font font, SolidBrush brush, float x, float y)
    {
        if (string.IsNullOrEmpty(text) || brush.Color.A == 0) return;
        Start('T'); Num(x); Num(y); Str(font.Css); Str(Css(brush.Color));
        commands.Append(Field).Append(text.Replace(Field, ' ').Replace(Record, ' '));
    }

    public SizeF MeasureString(string text, Font font)
    {
        var key = (font.Css, text);
        if (!measureCache.TryGetValue(key, out float width))
        {
            width = (float)platform.MeasureText(font.Css, text);
            if (measureCache.Count > 4000) measureCache.Clear();
            measureCache[key] = width;
        }
        return new SizeF(width, font.Size * 1.3f);
    }

    private void Start(char op)
    {
        if (commands.Length > 0) commands.Append(Record);
        commands.Append(op);
    }

    private void Num(float value) => commands.Append(Field).Append(value.ToString("0.##", CultureInfo.InvariantCulture));
    private void Str(string value) => commands.Append(Field).Append(value);

    private string Css(Color c)
    {
        int argb = c.ToArgb();
        if (!colorCache.TryGetValue(argb, out var css))
        {
            css = $"rgba({c.R},{c.G},{c.B},{(c.A / 255.0).ToString("0.###", CultureInfo.InvariantCulture)})";
            colorCache[argb] = css;
        }
        return css;
    }
}
