using System.Windows;
using System.Windows.Input;

namespace TempTrayWidget
{
    public partial class WidgetWindow : Window
    {
        public WidgetWindow()
        {
            InitializeComponent();
            // allow dragging
            MouseDown += (s, e) => {
                if (e.ChangedButton == MouseButton.Left)
                    DragMove();
            };
        }
    }
}
