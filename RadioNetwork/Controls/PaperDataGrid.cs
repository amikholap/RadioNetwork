using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RadioNetwork.Controls
{
    public class PaperDataGrid : DataGrid
    {
        static PaperDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PaperDataGrid), new FrameworkPropertyMetadata(typeof(PaperDataGrid)));
        }

        /// <summary>
        /// Tell whether the grid contains currently focused element.
        /// </summary>
        /// <returns></returns>
        public bool HasFocus()
        {
            DependencyObject o = (DependencyObject)FocusManager.GetFocusedElement(Application.Current.MainWindow);
            while (o != null)
            {
                if (o == this)
                {
                    return true;
                }
                o = VisualTreeHelper.GetParent(o);
            }
            return false;
        }
    }
}
