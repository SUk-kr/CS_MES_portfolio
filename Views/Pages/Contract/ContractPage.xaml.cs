using MES.Solution.Models;
using MES.Solution.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace MES.Solution.Views.Pages.Contract
{
    /// <summary>
    /// ContractPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ContractPage : Page
    {
        private readonly ContractViewModel _viewModel;
        public ContractPage()
        {
            InitializeComponent();
            _viewModel = new ContractViewModel();
            DataContext = _viewModel;
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            if (dataGrid != null && dataGrid.SelectedItem != null)
            {
                var selectedContract = dataGrid.SelectedItem as ContractModel;
                if (selectedContract != null)
                {
                    var viewModel = DataContext as ContractViewModel;
                    if (viewModel != null)
                    {
                        viewModel.LoadDataForEdit(selectedContract);
                    }
                }
            }
        }

        public void OnNavigatedFrom()
        {
            _viewModel?.Cleanup();
        }
    }
}
