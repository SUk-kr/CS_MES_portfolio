using MES.Solution.Models;
using MES.Solution.ViewModels;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MES.Solution.Views
{
    /// <summary>
    /// InventoryManagementWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InventoryManagementWindow : Window
    {
        private readonly InventoryManagementViewModel _viewModel;
        public InventoryManagementWindow(InventoryModel inventory = null)
        {
            InitializeComponent();
            _viewModel = new InventoryManagementViewModel();
            _viewModel.RequestClose += (s, e) =>
            {
                this.DialogResult = true;
                this.Close();
            };
            DataContext = _viewModel;

        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(@"^-?\d*\.?\d*$");
            e.Handled = !regex.IsMatch(e.Text);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
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
    }
}
