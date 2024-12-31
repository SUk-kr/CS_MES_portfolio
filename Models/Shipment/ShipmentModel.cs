using MES.Solution.Helpers;
using MES.Solution.ViewModels;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace MES.Solution.Models
{
    public class ShipmentModel : INotifyPropertyChanged
    {
        public ShipmentModel()
        {
            Status = "대기";
        }
        private bool _isSelected;

        public string OrderNumber { get; set; }  // 수주번호 추가
        public string ShipmentNumber { get; set; }
        public string CompanyCode { get; set; }
        public string CompanyName { get; set; }
        public string ProductCode { get; set; }
        public string ProductName { get; set; }
        public DateTime ProductionDate { get; set; }
        public DateTime ShipmentDate { get; set; }
        public int ShipmentQuantity { get; set; }
        public string VehicleNumber { get; set; }
        public string EmployeeName { get; set; }
        public string Status { get; set; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    // 체크박스 상태가 변경될 때 메인 뷰모델의 명령 상태를 갱신
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (Application.Current.MainWindow.DataContext is ShipmentViewModel viewModel)
                        {
                            (viewModel.ConfirmShipmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                            (viewModel.CancelShipmentCommand as RelayCommand)?.RaiseCanExecuteChanged();
                        }
                    }));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
