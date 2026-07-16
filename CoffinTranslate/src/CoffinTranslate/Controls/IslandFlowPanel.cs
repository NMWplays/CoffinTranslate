using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CoffinTranslate.Controls;

/// <summary>
/// Lays out the editor's top bars as "super-islands". Children are bare content groups tagged with
/// two attached properties:
///   • <see cref="IslandProperty"/> — the super-island a group belongs to (same id ⇒ same island).
///   • <see cref="TrailingProperty"/> — a group that hugs the right edge of its island (e.g. the
///     action buttons, or the zoom/filter chips); non-trailing groups pack to the left.
///
/// Each super-island is drawn as one full-width rounded island bar. While all its groups fit on one
/// line they share that single bar (primary groups left, trailing groups right). When they no longer
/// fit, the island splits: every group drops onto its own full-width bar with its content centered.
/// The panel paints the island chrome itself (matching the <c>Border.island</c> style) via a back
/// layer, so the same groups can visually merge into one island or separate into several without
/// re-parenting.
/// </summary>
public sealed class IslandFlowPanel : Panel
{
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<IslandFlowPanel, double>(nameof(Spacing), 10);

    public static readonly StyledProperty<Thickness> IslandPaddingProperty =
        AvaloniaProperty.Register<IslandFlowPanel, Thickness>(nameof(IslandPadding), new Thickness(16, 8));

    public static readonly StyledProperty<double> IslandCornerRadiusProperty =
        AvaloniaProperty.Register<IslandFlowPanel, double>(nameof(IslandCornerRadius), 14);

    /// <summary>Which super-island a child belongs to. Children sharing an id share one island bar.</summary>
    public static readonly AttachedProperty<int> IslandProperty =
        AvaloniaProperty.RegisterAttached<IslandFlowPanel, Control, int>("Island");

    /// <summary>A trailing (right-hugging) group of its super-island; others pack from the left.</summary>
    public static readonly AttachedProperty<bool> TrailingProperty =
        AvaloniaProperty.RegisterAttached<IslandFlowPanel, Control, bool>("Trailing");

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public Thickness IslandPadding
    {
        get => GetValue(IslandPaddingProperty);
        set => SetValue(IslandPaddingProperty, value);
    }

    public double IslandCornerRadius
    {
        get => GetValue(IslandCornerRadiusProperty);
        set => SetValue(IslandCornerRadiusProperty, value);
    }

    public static int GetIsland(Control c) => c.GetValue(IslandProperty);
    public static void SetIsland(Control c, int v) => c.SetValue(IslandProperty, v);
    public static bool GetTrailing(Control c) => c.GetValue(TrailingProperty);
    public static void SetTrailing(Control c, bool v) => c.SetValue(TrailingProperty, v);

    // Panel.Render is sealed (it paints Background), so a back layer draws the island chrome instead.
    private readonly IslandChrome _chrome = new();
    private readonly List<Rect> _islandRects = [];

    public IslandFlowPanel()
    {
        _chrome.Rects = _islandRects;
        Children.Add(_chrome);
    }

    static IslandFlowPanel()
    {
        AffectsMeasure<IslandFlowPanel>(SpacingProperty, IslandPaddingProperty);
        AffectsArrange<IslandFlowPanel>(SpacingProperty, IslandPaddingProperty, IslandCornerRadiusProperty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children)
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));

        return Layout(availableSize.Width, arrange: false);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _islandRects.Clear();
        var size = Layout(finalSize.Width, arrange: true);

        _chrome.CornerRadius = IslandCornerRadius;
        _chrome.Arrange(new Rect(0, 0, finalSize.Width, size.Height));
        _chrome.InvalidateVisual();
        return new Size(finalSize.Width, size.Height);
    }

    /// <summary>
    /// Shared measure/arrange pass. Groups children into super-islands, lays each out as one
    /// full-width bar (or, when its groups don't fit, splits it into per-group centered bars),
    /// and — when <paramref name="arrange"/> — positions the children and records the island rects.
    /// </summary>
    private Size Layout(double availableWidth, bool arrange)
    {
        var pad = IslandPadding;
        double spacing = Spacing;
        bool boundless = double.IsInfinity(availableWidth);
        double y = 0, maxWidth = 0;

        foreach (var island in GroupIntoIslands())
        {
            double contentW = -spacing;
            double maxSegH = 0;
            foreach (var seg in island)
            {
                contentW += spacing + seg.DesiredSize.Width;
                maxSegH = Math.Max(maxSegH, seg.DesiredSize.Height);
            }

            double needed = contentW + pad.Left + pad.Right;
            maxWidth = Math.Max(maxWidth, needed);
            bool shared = boundless || needed <= availableWidth;

            if (shared)
            {
                double rowH = maxSegH + pad.Top + pad.Bottom;
                double barWidth = boundless ? needed : availableWidth;
                if (arrange)
                {
                    _islandRects.Add(new Rect(0, y, barWidth, rowH));
                    ArrangeSharedBar(island, barWidth, y, rowH, pad, spacing);
                }
                y += rowH + spacing;
            }
            else
            {
                // island splits: each group becomes its own full-width, centered bar
                foreach (var seg in island)
                {
                    double rowH = seg.DesiredSize.Height + pad.Top + pad.Bottom;
                    if (arrange)
                    {
                        _islandRects.Add(new Rect(0, y, availableWidth, rowH));
                        double segW = Math.Min(seg.DesiredSize.Width, availableWidth - pad.Left - pad.Right);
                        double x = Math.Max(pad.Left, (availableWidth - segW) / 2);
                        seg.Arrange(new Rect(x, y + pad.Top, segW, seg.DesiredSize.Height));
                    }
                    y += rowH + spacing;
                }
            }
        }

        if (y > 0)
            y -= spacing; // trailing gap after the last bar
        return new Size(boundless ? maxWidth : availableWidth, y);
    }

    /// <summary>Arranges one island bar: non-trailing groups packed left, trailing groups anchored right.</summary>
    private static void ArrangeSharedBar(List<Control> island, double barWidth, double y, double rowH,
                                         Thickness pad, double spacing)
    {
        double left = pad.Left;
        foreach (var seg in island)
        {
            if (GetTrailing(seg))
                continue;
            PlaceCentered(seg, left, y, rowH);
            left += seg.DesiredSize.Width + spacing;
        }

        double trailW = -spacing;
        foreach (var seg in island)
            if (GetTrailing(seg))
                trailW += spacing + seg.DesiredSize.Width;

        double x = barWidth - pad.Right - Math.Max(0, trailW);
        foreach (var seg in island)
        {
            if (!GetTrailing(seg))
                continue;
            PlaceCentered(seg, x, y, rowH);
            x += seg.DesiredSize.Width + spacing;
        }
    }

    private static void PlaceCentered(Control seg, double x, double y, double rowH)
    {
        double h = seg.DesiredSize.Height;
        seg.Arrange(new Rect(x, y + (rowH - h) / 2, seg.DesiredSize.Width, h));
    }

    /// <summary>Groups visible children into ordered super-islands by their <see cref="IslandProperty"/> id.</summary>
    private List<List<Control>> GroupIntoIslands()
    {
        var islands = new List<List<Control>>();
        var byId = new Dictionary<int, List<Control>>();

        foreach (var child in Children)
        {
            if (ReferenceEquals(child, _chrome) || !child.IsVisible)
                continue;

            int id = GetIsland(child);
            if (!byId.TryGetValue(id, out var list))
            {
                list = [];
                byId[id] = list;
                islands.Add(list);
            }
            list.Add(child);
        }

        return islands;
    }
}

/// <summary>Back layer that paints the island bars computed by <see cref="IslandFlowPanel"/>.</summary>
internal sealed class IslandChrome : Control
{
    // fallback fill/stroke if the theme resources aren't found; the live look comes from the
    // theme-aware CtIslandBg/CtIslandBorder brushes so drawn bars match real Border.island surfaces
    private static readonly IBrush FallbackFill = new SolidColorBrush(Color.Parse("#0F808080"));
    private static readonly IBrush FallbackStroke = new SolidColorBrush(Color.Parse("#20808080"));

    public List<Rect>? Rects;
    public double CornerRadius;

    public IslandChrome()
    {
        IsHitTestVisible = false;
        // repaint when the user flips light/dark so the bars pick up the new theme brushes
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Rects is null)
            return;

        var fill = Resolve("CtIslandBg", FallbackFill);
        var stroke = new Pen(Resolve("CtIslandBorder", FallbackStroke), 1);

        foreach (var r in Rects)
        {
            // inset by half a pixel so the 1px stroke stays crisp and isn't clipped at the edge
            var rr = r.Deflate(0.5);
            context.DrawRectangle(fill, stroke, rr, CornerRadius, CornerRadius);
        }
    }

    private IBrush Resolve(string key, IBrush fallback) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : fallback;
}
