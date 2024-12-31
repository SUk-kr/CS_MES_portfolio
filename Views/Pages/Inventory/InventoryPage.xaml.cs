using System.Windows.Controls;
using MES.Solution.ViewModels;
using MES.Solution.Models;
using System.Windows.Input;

namespace MES.Solution.Views.Pages
{
    public partial class InventoryPage : Page
    {
        private readonly InventoryViewModel _viewModel;

        public InventoryPage()
        {
            InitializeComponent();
            _viewModel = new InventoryViewModel();
            DataContext = _viewModel;
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid?.SelectedItem is InventoryModel selectedInventory)
            {
                _viewModel.LoadDataForEdit(selectedInventory);
            }
        }

        private void TextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if(e.Key == System.Windows.Input.Key.Enter)
            {
                if (_viewModel.SearchCommand.CanExecute(null))
                {
                    _viewModel.SearchCommand.Execute(null);
                }
            }
        }
    }
}