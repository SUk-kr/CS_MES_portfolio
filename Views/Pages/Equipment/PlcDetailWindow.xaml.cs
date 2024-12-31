using MES.Solution.ViewModels;
using MES.Solution.ViewModels.Equipment;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MES.Solution.Views
{
    /// <summary>
    /// PlcDetailWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PlcDetailWindow : Window
    {
        private readonly PlcViewModel _plcViewModel;
        public PlcDetailWindow(PlcStatusModel plc, PlcViewModel plcViewModel)
        {
            InitializeComponent();
            DataContext = plc;
            _plcViewModel = plcViewModel;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is EquipmentViewModel viewModel)
            {
                _plcViewModel?.PLCCleanup();
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                var result = MessageBox.Show("창을 닫으시겠습니까?", "확인",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    this.Close();
                }
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
