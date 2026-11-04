using System.Windows;
using System.Windows.Controls;

namespace LightSQL.Controls
{
    /// <summary>
    /// Stacks result grids vertically and stretches them to fill the visible area,
    /// like SSMS: a single result set takes the whole results pane, several share it
    /// equally. Each grid is never shorter than <see cref="MinItemHeight"/>; when
    /// they no longer fit, the panel grows taller than the viewport so the hosting
    /// <see cref="ScrollViewer"/> can scroll.
    /// </summary>
    /// <remarks>
    /// Inside a vertically scrolling <see cref="ScrollViewer"/> a panel is measured
    /// with an infinite height, so it cannot "fill" anything by itself. The viewport
    /// height is therefore supplied through <see cref="ViewportHeight"/> (bound to the
    /// ScrollViewer's own <see cref="ScrollViewer.ViewportHeight"/>). Measuring each
    /// grid with a finite height also keeps the DataGrid's row virtualization working.
    /// </remarks>
    public sealed class ResultGridsPanel : Panel
    {
        /// <summary>
        /// Backing property for the minimum height of each stacked item.
        /// </summary>
        public static readonly DependencyProperty MinItemHeightProperty = DependencyProperty.Register(
            nameof(MinItemHeight),
            typeof(double),
            typeof(ResultGridsPanel),
            new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// Backing property for the height of the visible area the items should fill.
        /// </summary>
        public static readonly DependencyProperty ViewportHeightProperty = DependencyProperty.Register(
            nameof(ViewportHeight),
            typeof(double),
            typeof(ResultGridsPanel),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure));

        /// <summary>
        /// The minimum height of each stacked item, in device-independent pixels.
        /// </summary>
        public double MinItemHeight
        {
            get
            {
                return (double)GetValue(MinItemHeightProperty);
            }

            set
            {
                SetValue(MinItemHeightProperty, value);
            }
        }

        /// <summary>
        /// The height of the visible area the items should fill. Used when the panel
        /// is measured with an unbounded height (inside a ScrollViewer).
        /// </summary>
        public double ViewportHeight
        {
            get
            {
                return (double)GetValue(ViewportHeightProperty);
            }

            set
            {
                SetValue(ViewportHeightProperty, value);
            }
        }

        /// <summary>
        /// Calculates the height given to each item: the available height split
        /// equally, but never less than the minimum item height.
        /// </summary>
        /// <param name="availableHeight">The height to fill.</param>
        /// <param name="itemCount">The number of items to stack.</param>
        /// <param name="minItemHeight">The minimum height of one item.</param>
        /// <returns>The height of each item, or 0 when there are no items.</returns>
        public static double CalculateItemHeight(double availableHeight, int itemCount, double minItemHeight)
        {
            if (itemCount <= 0)
            {
                return 0d;
            }

            double safeMinimum = double.IsNaN(minItemHeight) == true || minItemHeight < 0d ? 0d : minItemHeight;

            if (double.IsNaN(availableHeight) == true || double.IsInfinity(availableHeight) == true || availableHeight <= 0d)
            {
                return safeMinimum;
            }

            return Math.Max(safeMinimum, availableHeight / itemCount);
        }

        /// <summary>
        /// Measures every child with the same finite height.
        /// </summary>
        /// <param name="availableSize">The size offered by the parent.</param>
        /// <returns>The total size needed to stack all children.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            int count = InternalChildren.Count;
            double itemHeight = CalculateItemHeight(ResolveFillHeight(availableSize.Height), count, MinItemHeight);
            double width = double.IsInfinity(availableSize.Width) == true ? double.PositiveInfinity : availableSize.Width;
            double desiredWidth = 0d;

            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(width, itemHeight));
                desiredWidth = Math.Max(desiredWidth, child.DesiredSize.Width);
            }

            double desiredHeight = itemHeight * count;

            // Report the offered width (when known) so the grids stretch horizontally.
            double reportedWidth = double.IsInfinity(availableSize.Width) == true ? desiredWidth : availableSize.Width;

            return new Size(reportedWidth, desiredHeight);
        }

        /// <summary>
        /// Arranges the children top to bottom, each with the same height.
        /// </summary>
        /// <param name="finalSize">The size given by the parent.</param>
        /// <returns>The size actually used.</returns>
        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = InternalChildren.Count;
            double itemHeight = CalculateItemHeight(ResolveFillHeight(finalSize.Height), count, MinItemHeight);
            double top = 0d;

            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(0d, top, finalSize.Width, itemHeight));
                top += itemHeight;
            }

            return finalSize;
        }

        /// <summary>
        /// Picks the height to fill: the viewport height when one is supplied,
        /// otherwise the height offered by the parent.
        /// </summary>
        /// <param name="offeredHeight">The height offered by the parent.</param>
        /// <returns>The height the items should fill.</returns>
        private double ResolveFillHeight(double offeredHeight)
        {
            if (ViewportHeight > 0d)
            {
                return ViewportHeight;
            }

            return offeredHeight;
        }
    }
}
