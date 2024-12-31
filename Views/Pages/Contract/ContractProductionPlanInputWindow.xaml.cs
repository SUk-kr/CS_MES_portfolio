using MES.Solution.Models;
using MES.Solution.ViewModels;
using System.Windows;

namespace MES.Solution.Views
{
    /// <summary>
    /// ContractProductionPlanInputWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ContractProductionPlanInputWindow : Window
    {
        private readonly ContractProductionPlanInputViewModel _viewModel;

        public ContractProductionPlanInputWindow(ContractModel contract)
        {
            InitializeComponent();
            _viewModel = new ContractProductionPlanInputViewModel(contract);
            _viewModel.RequestClose += (s, e) =>
            {
                this.DialogResult = true;
                this.Close();
            };
            DataContext = _viewModel;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
