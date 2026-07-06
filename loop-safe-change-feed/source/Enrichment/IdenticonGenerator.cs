using System.Globalization;
using System.Text;

namespace Cosmos.ChangeFeedEnrichment;

/// <summary>
/// Turns a hex hash into a deterministic, GitHub-style 5x5 symmetric identicon rendered as an
/// inline SVG string. The same hash always produces the same image; a different hash produces a
/// visibly different one. It is pure and dependency-free, so it stands in for any real enrichment
/// step you might plug in instead (embeddings, translation, classification, summarization, ...).
/// Because the picture is derived from the hash, watching the identicon change in the UI is
/// literally watching the loop-guard hash change.
/// </summary>
public static class IdenticonGenerator
{
    /// <summary>Renders a square identicon SVG (default 120x120) from a hex hash.</summary>
    public static string ToSvg(string hashHex, int pixels = 120)
    {
        byte[] bytes = Convert.FromHexString(hashHex);
        string color = ColorFromBytes(bytes);

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixels}\" height=\"{pixels}\" viewBox=\"0 0 5 5\" shape-rendering=\"crispEdges\" role=\"img\">");
        sb.Append("<rect width=\"5\" height=\"5\" fill=\"#f4f4f5\"/>");

        // Fill the left three columns from the hash, then mirror columns 0 and 1 to 4 and 3 so
        // the result is vertically symmetric (the classic identicon look).
        for (int col = 0; col < 3; col++)
        {
            for (int row = 0; row < 5; row++)
            {
                int cell = col * 5 + row; // 0..14
                bool filled = (bytes[cell] & 1) == 0; // even byte => filled (~50%)
                if (!filled) continue;
                AppendCell(sb, col, row, color);
                if (col < 2) AppendCell(sb, 4 - col, row, color);
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static void AppendCell(StringBuilder sb, int x, int y, string color) =>
        sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"{y}\" width=\"1\" height=\"1\" fill=\"{color}\"/>");

    private static string ColorFromBytes(byte[] b)
    {
        // A single saturated hue chosen from the hash keeps the palette pleasant and varied.
        double hue = b[0] / 255.0 * 360.0;
        return HslToHex(hue, 0.60, 0.50);
    }

    private static string HslToHex(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60.0 % 2) - 1));
        double m = l - c / 2;
        (double r, double g, double bl) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return $"#{ToByte(r, m):x2}{ToByte(g, m):x2}{ToByte(bl, m):x2}";
    }

    private static int ToByte(double channel, double m) => (int)Math.Round((channel + m) * 255);
}
