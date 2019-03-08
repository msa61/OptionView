using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace OptionView
{

    public class MoveTile : Thumb
    {
        public MoveTile()
        {
            DragDelta += new DragDeltaEventHandler(this.MoveThumb_DragDelta);
        }

        private void MoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            Control tile = this.DataContext as Control;

            if (tile != null)
            {
                double left = Canvas.GetLeft(tile);
                double top = Canvas.GetTop(tile);
                double tileWidth = tile.ActualWidth;
                double tileHeight = tile.ActualHeight;
                double canvasWidth = ((Canvas)tile.Parent).ActualWidth;
                double canvasHeight = ((Canvas)tile.Parent).ActualHeight;

                double newLeft = left + e.HorizontalChange;
                newLeft = newLeft < 0 ? 0 : newLeft;
                newLeft = (newLeft + tileWidth) > (canvasWidth) ? canvasWidth - tileWidth : newLeft;
                double newTop = top + e.VerticalChange;
                newTop = newTop < 0 ? 0 : newTop;
                newTop = (newTop + tileHeight) > (canvasHeight) ? canvasHeight - tileHeight : newTop;
                Canvas.SetLeft(tile, newLeft);
                Canvas.SetTop(tile, newTop < 0 ? 0 : newTop);
            }
        }
    }
}
