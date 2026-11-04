using LightSQL.Controls;
using Xunit;

namespace LightSQL.Tests
{
    /// <summary>
    /// Tests how <see cref="ResultGridsPanel"/> sizes result grids so they stretch to
    /// fill the results pane (e.g. when the window is maximized).
    /// </summary>
    public class ResultGridsPanelTests
    {
        [Fact]
        public void SingleGrid_FillsTheWholeHeight()
        {
            double height = ResultGridsPanel.CalculateItemHeight(900d, 1, 150d);

            Assert.Equal(900d, height);
        }

        [Fact]
        public void SeveralGrids_ShareTheHeightEqually()
        {
            double height = ResultGridsPanel.CalculateItemHeight(900d, 3, 150d);

            Assert.Equal(300d, height);
        }

        [Fact]
        public void TooManyGrids_KeepTheMinimumHeight()
        {
            // 10 grids in 900px would be 90px each; the minimum (150px) wins and the
            // panel becomes taller than the viewport so it can scroll.
            double height = ResultGridsPanel.CalculateItemHeight(900d, 10, 150d);

            Assert.Equal(150d, height);
        }

        [Fact]
        public void NoGrids_HaveNoHeight()
        {
            Assert.Equal(0d, ResultGridsPanel.CalculateItemHeight(900d, 0, 150d));
        }

        [Theory]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NaN)]
        [InlineData(0d)]
        public void UnknownAvailableHeight_FallsBackToMinimum(double availableHeight)
        {
            double height = ResultGridsPanel.CalculateItemHeight(availableHeight, 2, 150d);

            Assert.Equal(150d, height);
        }

        [Fact]
        public void NegativeMinimum_IsTreatedAsZero()
        {
            Assert.Equal(200d, ResultGridsPanel.CalculateItemHeight(400d, 2, -50d));
            Assert.Equal(0d, ResultGridsPanel.CalculateItemHeight(double.PositiveInfinity, 2, -50d));
        }
    }
}
