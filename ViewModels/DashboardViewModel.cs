using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Windows;
using LiveCharts;
using LiveCharts.Wpf;
using System.Windows.Media;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;
using MES.Solution.Services;
using MES.Solution.Models;
using MES.Solution.Models.Equipment;

namespace MES.Solution.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly DispatcherTimer _refreshTimer;
        private readonly EquipmentChartService _equipmentChartService;
        private readonly InventoryChartService _inventoryChartService;

        // 생산 현황 관련
        private string _todayProduction;
        private double _todayProductionRate;
        private string _weeklyProduction;
        private double _weeklyProductionRate;

        // 설비 현황 관련
        private double _equipmentOperationRate;
        private double _achievementRate;
        private int _operatingEquipmentCount;
        private int _totalEquipmentCount;

        // 차트 관련
        private string[] _timeLabels;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public DashboardViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _equipmentChartService = new EquipmentChartService();
            _inventoryChartService = new InventoryChartService(_connectionString);
            NumberFormatter = value => value.ToString("N0");

            // Collection 초기화
            LineStatus = new ObservableCollection<ProductionPlanModel>();
            QuantityPieChartData = new SeriesCollection();
            EquipmentStatus = new ObservableCollection<EquipmentCardModel>();
            ProductionTrendSeries = new SeriesCollection();

            // 타이머 설정 (30초마다 새로고침)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += async (s, e) => await RefreshDataAsync();
            // _refreshTimer.Start();

            // 초기 데이터 로드
            _ = InitializeDataAsync();
        }
        #endregion


        #region Properties
        // 생산 현황 속성
        public string TodayProduction
        {
            get => _todayProduction;
            set
            {
                if (_todayProduction != value)
                {
                    _todayProduction = value;
                    OnPropertyChanged();
                }
            }
        }

        public double TodayProductionRate
        {
            get => _todayProductionRate;
            set
            {
                if (_todayProductionRate != value)
                {
                    _todayProductionRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string WeeklyProduction
        {
            get => _weeklyProduction;
            set
            {
                if (_weeklyProduction != value)
                {
                    _weeklyProduction = value;
                    OnPropertyChanged();
                }
            }
        }

        public double WeeklyProductionRate
        {
            get => _weeklyProductionRate;
            set
            {
                if (_weeklyProductionRate != value)
                {
                    _weeklyProductionRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public double AchievementRate
        {
            get => _achievementRate;
            set
            {
                if (_achievementRate != value)
                {
                    _achievementRate = value;
                    OnPropertyChanged();
                }
            }
        }

        // 설비 현황 속성
        public double EquipmentOperationRate
        {
            get => _equipmentOperationRate;
            set
            {
                if (_equipmentOperationRate != value)
                {
                    _equipmentOperationRate = value;
                    OnPropertyChanged();
                }
            }
        }

        public int OperatingEquipmentCount
        {
            get => _operatingEquipmentCount;
            set
            {
                if (_operatingEquipmentCount != value)
                {
                    _operatingEquipmentCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int TotalEquipmentCount
        {
            get => _totalEquipmentCount;
            set
            {
                _totalEquipmentCount = value;
                OnPropertyChanged(nameof(TotalEquipmentCount));
            }
        }

        // 차트 관련 속성
        public string[] TimeLabels
        {
            get => _timeLabels;
            set
            {
                if (_timeLabels != value)
                {
                    _timeLabels = value;
                    OnPropertyChanged();
                }
            }
        }
        public string[] ProductionTrendLabels { get; private set; }
        public Func<double, string> NumberFormatter { get; set; }
        #endregion


        #region Collections
        // UI 바인딩용 컬렉션
        public ObservableCollection<ProductionPlanModel> LineStatus { get; }
        public SeriesCollection QuantityPieChartData { get; }  
        public ObservableCollection<EquipmentCardModel> EquipmentStatus { get; }
        public SeriesCollection ProductionTrendSeries { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async Task InitializeDataAsync()
        {
            try
            {
                await Task.WhenAll(
                    LoadProductionStatusAsync(),
                    LoadPieChartData(),
                    LoadEquipmentStatusAsync(),
                    LoadProductionTrendAsync()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 초기화 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            if(Application.Current.Windows.Count>1)
            {
                
            }

        }

        private async Task LoadProductionStatusAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                        SELECT 
                            SUM(order_quantity) as TotalPlanned,
                            SUM(production_quantity) as TotalProduced
                        FROM dt_production_plan 
                        WHERE DATE(production_date) = CURDATE();

                        SELECT 
                            SUM(order_quantity) as WeeklyPlanned,
                            SUM(production_quantity) as WeeklyProduced
                        FROM dt_production_plan 
                        WHERE production_date >= DATE_SUB(CURDATE(), INTERVAL 7 DAY);

                        SELECT 
                            production_line as ProductionLine,
                            CASE 
                                WHEN COUNT(DISTINCT process_status) > 1 THEN '혼합'
                                ELSE MAX(process_status)
                            END as Status,
                            SUM(production_quantity) as ProductionQuantity
                        FROM dt_production_plan
                        WHERE DATE(production_date) = CURDATE()
                        GROUP BY production_line;";

                using (var multi = await conn.QueryMultipleAsync(sql))
                {
                    var dailyStats = await multi.ReadFirstAsync();
                    var weeklyStats = await multi.ReadFirstAsync();
                    var lineStats = await multi.ReadAsync<ProductionPlanModel>();

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        TodayProduction = $"{dailyStats.TotalProduced:N0}";
                        // decimal을 double로 변환
                        TodayProductionRate = dailyStats.TotalPlanned > 0
                            ? Convert.ToDouble(dailyStats.TotalProduced) * 100.0 / Convert.ToDouble(dailyStats.TotalPlanned)
                            : 0;

                        WeeklyProduction = $"{weeklyStats.WeeklyProduced:N0}";
                        // decimal을 double로 변환
                        WeeklyProductionRate = weeklyStats.WeeklyPlanned > 0
                            ? Convert.ToDouble(weeklyStats.WeeklyProduced) * 100.0 / Convert.ToDouble(weeklyStats.WeeklyPlanned)
                            : 0;

                        AchievementRate = TodayProductionRate;

                        LineStatus.Clear();
                        foreach (var line in lineStats)
                        {
                            LineStatus.Add(line);
                        }
                    });
                }
            }
        }
        private async Task LoadPieChartData()
        {
            try
            {
                var chartData = await _inventoryChartService.LoadAllChartDataAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    QuantityPieChartData.Clear();
                    foreach (var series in chartData.QuantityPieChart)
                    {
                        QuantityPieChartData.Add(series);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파이 차트 데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadEquipmentStatusAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                SELECT 
                    equipment_code as EquipmentCode,
                    production_line as ProductionLine,
                    CAST(temperature as DECIMAL(5,2)) as Temperature,
                    CAST(humidity as DECIMAL(5,2)) as Humidity,
                    inspection_date as InspectionDate,
                    CASE 
                        WHEN temperature > 28 THEN '경고'
                        WHEN temperature < 18 THEN '경고'
                        ELSE '정상'
                    END as Status
                FROM dt_facility_management
                ORDER BY production_line, inspection_date";

                var equipments = await conn.QueryAsync<EquipmentCardModel>(sql);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    EquipmentStatus.Clear();
                    var groupedEquipments = equipments.GroupBy(e => e.ProductionLine);

                    foreach (var lineGroup in groupedEquipments)
                    {
                        var lineData = lineGroup.OrderBy(e => e.InspectionDate).ToList();

                        var equipmentCard = new EquipmentCardModel
                        {
                            ProductionLine = lineGroup.Key,
                            Temperature = (decimal)(double)lineData.Last().Temperature,
                            Status = lineData.Last().Status,
                        };

                        EquipmentStatus.Add(equipmentCard);
                    }
                    // 고유한 장비코드 중에서 상태가 정상인 것들의 수
                    var distinctEquipments = equipments
                        .GroupBy(e => e.EquipmentCode)
                        .Select(g => g.OrderByDescending(e => e.InspectionDate).First()) // 각 장비의 최신 상태
                        .ToList();

                    // 정상 상태인 장비 수
                    OperatingEquipmentCount = distinctEquipments.Count(e => e.Status == "정상");
                    TotalEquipmentCount = distinctEquipments.Count;  // 전체 고유 장비 수 설정

                    // 전체 고유 장비 수
                    var totalDistinctEquipmentCount = distinctEquipments.Count;

                    // 가동률 계산 (정상 장비 수 / 전체 고유 장비 수 * 100)
                    EquipmentOperationRate = totalDistinctEquipmentCount > 0
                        ? (double)OperatingEquipmentCount * 100 / totalDistinctEquipmentCount
                        : 0;
                });
            }
        }

        private async Task LoadProductionTrendAsync()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT 
                        production_date as PlanDate,
                        SUM(production_quantity) as ProductionQuantity
                    FROM dt_production_plan
                    WHERE production_date >= DATE_SUB(CURDATE(), INTERVAL 7 DAY)
                    GROUP BY production_date
                    ORDER BY production_date";

                var result = await conn.QueryAsync<ProductionPlanModel>(sql);
                var data = result.ToList();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductionTrendSeries.Clear();
                    ProductionTrendSeries.Add(new LineSeries
                    {
                        Title = "생산량",
                        Values = new ChartValues<double>(data.Select(x => (double)x.ProductionQuantity)),
                        PointGeometry = DefaultGeometries.Circle,
                        Stroke = new SolidColorBrush(Color.FromRgb(24, 90, 189))
                    });
                    TimeLabels = data.Select(x => x.PlanDate.ToString("MM/dd")).ToArray();
                });
            }
        }

        // 데이터 갱신 메서드
        private async Task RefreshDataAsync()
        {
            await InitializeDataAsync();
        }

        // 속성 변경 알림
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 리소스 정리
        public void Cleanup()
        {
            _refreshTimer?.Stop();
        }
        #endregion
    }
}