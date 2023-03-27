using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OptionView
{
    public class GroupWindowHandler
    {
        public GroupDetailsWindow Window { get; set; } = null;
        public double Left { get; set; } = 0;
        public double Top { get; set; } = 0;
        private GroupGraph groupContents = null;


        public void Open(GroupGraph gg = null)
        {
            if (Window == null)
            {
                Window = new GroupDetailsWindow();
                Window.Left = Left;
                Window.Top = Top;
            }
            Window.Show();

            Window.Update((gg == null) ? groupContents : gg);
        }
        public void Update(GroupGraph gg)
        {
            groupContents = gg;
            if ((Window != null) && (Window.IsVisible)) Window.Update(gg);
        }
        public void Close()
        {
            if (Window != null) Window.Close();
            Window = null;
        }
        public void Clear()
        {
            groupContents = null;
            if (Window != null) Window.Clear();
        }
    }
}
