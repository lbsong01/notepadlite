using ICSharpCode.AvalonEdit.Rendering;
using NotepadLite.Core;
using System.Windows;
using System.Windows.Media;

namespace NotepadLite.App;

/// <summary>
/// Background renderer that paints all current search matches behind the editor's text,
/// emphasising the active match.
/// </summary>
internal sealed class SearchHighlightRenderer : IBackgroundRenderer
{
    private static readonly SolidColorBrush AllMatchesBrush = CreateFrozen(Color.FromArgb(120, 0xFF, 0xE2, 0x80));
    private static readonly SolidColorBrush CurrentMatchBrush = CreateFrozen(Color.FromArgb(180, 0xFF, 0xB3, 0x47));
    private static readonly Pen CurrentMatchPen = CreateFrozen(new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x80, 0x00)), 1.0));

    private IReadOnlyList<SearchMatch> matches = Array.Empty<SearchMatch>();
    private int currentMatchIndex = -1;

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetMatches(IReadOnlyList<SearchMatch> newMatches, int currentIndex)
    {
        matches = newMatches ?? Array.Empty<SearchMatch>();
        currentMatchIndex = currentIndex;
    }

    public void Clear()
    {
        matches = Array.Empty<SearchMatch>();
        currentMatchIndex = -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (matches.Count == 0 || textView.VisualLines.Count == 0)
        {
            return;
        }

        textView.EnsureVisualLines();

        var viewStart = textView.VisualLines[0].FirstDocumentLine.Offset;
        var lastLine = textView.VisualLines[^1].LastDocumentLine;
        var viewEnd = lastLine.Offset + lastLine.Length;

        for (var i = 0; i < matches.Count; i++)
        {
            var m = matches[i];
            var matchEnd = m.Offset + m.Length;
            if (matchEnd < viewStart || m.Offset > viewEnd)
            {
                continue;
            }

            var builder = new BackgroundGeometryBuilder
            {
                AlignToWholePixels = true,
                CornerRadius = 2,
            };

            builder.AddSegment(textView, new ICSharpCode.AvalonEdit.Document.TextSegment
            {
                StartOffset = m.Offset,
                Length = m.Length,
            });

            var geometry = builder.CreateGeometry();
            if (geometry is null)
            {
                continue;
            }

            if (i == currentMatchIndex)
            {
                drawingContext.DrawGeometry(CurrentMatchBrush, CurrentMatchPen, geometry);
            }
            else
            {
                drawingContext.DrawGeometry(AllMatchesBrush, null, geometry);
            }
        }
    }

    private static SolidColorBrush CreateFrozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozen(Pen pen)
    {
        if (pen.Brush is Freezable b && b.CanFreeze)
        {
            b.Freeze();
        }

        pen.Freeze();
        return pen;
    }
}
