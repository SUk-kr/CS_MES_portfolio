using MES.Solution.Helpers;
using MES.Solution.ViewModels.Equipment;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MES.Solution.Views
{
    /// <summary>
    /// EquipmentMaintenanceScheduleAddWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class EquipmentMaintenanceScheduleAddWindow : Window
    {
        private readonly EquipmentMaintenanceScheduleAddViewModel _viewModel;

        public EquipmentMaintenanceScheduleAddWindow(bool isEdit = false)
        {
            InitializeComponent();
            _viewModel = new EquipmentMaintenanceScheduleAddViewModel(isEdit);
            _viewModel.RequestClose += (s, e) =>
            {
                this.DialogResult = true;
                this.Close();
            };
            DataContext = _viewModel;

        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            WindowHelper.RemoveMinimizeMaximizeButtons(this);
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

        private void ValidateTemperature(object sender, TextCompositionEventArgs e)
        {
            // 실수만 입력 받고 싶을때
            if (e.Text == "." && TemperatureTextbox.Text.Contains("."))
            {
                e.Handled = true;
                return;
            }
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void ValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            // 실수만 입력 받고 싶을때
            if (e.Text == "." && TemperatureTextbox.Text.Contains("."))
            {
                e.Handled = true;
                return;
            }
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }



        public void LoadData(Models.MaintenanceScheduleModel model)
        {
            _viewModel.LoadData(model);
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
