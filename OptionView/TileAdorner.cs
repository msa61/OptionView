using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OptionView
{
    class TileAdorner : Adorner
    { 
        public TileAdorner(UIElement elem) : base (elem)
        {

        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // todo
            this.IsHitTestVisible = false;

            SolidColorBrush brush = new SolidColorBrush(Colors.Transparent);
            Pen pen = new Pen(new SolidColorBrush(Colors.White), 1);
            Rect rect = new Rect(0, 0, this.AdornedElement.RenderSize.Width, this.AdornedElement.RenderSize.Height);

            drawingContext.DrawRoundedRectangle(brush, pen, rect, 8, 8);
        }
    }
}
