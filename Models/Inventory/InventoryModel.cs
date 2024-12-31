using LiveCharts;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MES.Solution.Models
{
    public class InventoryModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string ProductCode { get; set; }
        public string ProductGroup { get; set; }
        public string ProductName { get; set; }
        public int TotalQuantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime LastUpdateDate { get; set; }
        public DateTime Date {  get; set; }
        public string TransactionType { get; set; }
        public string ResponsiblePerson { get; set; }
        public string Remarks { get; set; }
        public int ProductCount { get; set; }



        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class InventoryChartData
    {
        public SeriesCollection ProductGroupChart { get; set; } = new SeriesCollection();
        public SeriesCollection InventoryTrendChart { get; set; } = new SeriesCollection();
        public SeriesCollection QuantityPieChart { get; set; } = new SeriesCollection(); // Fixed property name
        public string[] GroupChartLabels { get; set; }
        public string[] TrendChartLabels { get; set; }
    }
}
