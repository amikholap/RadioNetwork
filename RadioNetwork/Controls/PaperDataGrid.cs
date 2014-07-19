using System.Windows;
using System.Windows.Controls;

namespace RadioNetwork.Controls
{
    public class PaperDataGrid : DataGrid
    {
        static PaperDataGrid()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PaperDataGrid), new FrameworkPropertyMetadata(typeof(PaperDataGrid)));
        }
    }
}
