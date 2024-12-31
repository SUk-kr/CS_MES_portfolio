﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MES.Solution.Helpers;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using LiveCharts;
using MES.Solution.Services;
using MES.Solution.Views;
using System.Linq;
using MES.Solution.Models;

namespace MES.Solution.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly InventoryChartService _chartService;
        private readonly LogService _logService;

        // 필터 관련
        private string _productNameFilter;
        private string _selectedProductGroup;
        private bool _isshowCurrentStockOnly;

        // 상태 관련
        private InventoryManagementWindow _currentWindow;
        private InventoryModel _selectedInventory;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public InventoryViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _chartService = new InventoryChartService(_connectionString);
            _logService = new LogService();

            // 필터 초기화
            SelectedProductGroup = "전체";
            
            // 컬렉션 초기화
            Inventories = new ObservableCollection<InventoryModel>();
            ProductGroups = new ObservableCollection<string>();
            QuantityFilters = new ObservableCollection<string>
            {
                "전체",
                "재고 없음"
            };

            // 차트 컬렉션 초기화 추가
            ProductGroupChartData = new SeriesCollection();
            InventoryTrendChartData = new SeriesCollection();
            QuantityPieChartData = new SeriesCollection();

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            DeleteCommand = new AsyncRelayCommand(ExecuteDelete, CanExecuteDelete);

            // 초기 데이터 로드
            _ = LoadInitialData();
        }
        #endregion


        #region Properties
        // 필터 속성
        public string ProductNameFilter
        {
            get => _productNameFilter;
            set
            {
                if (_productNameFilter != value)
                {
                    _productNameFilter = value;
                    OnPropertyChanged();
                }
            }
        }
        public string SelectedProductGroup
        {
            get => _selectedProductGroup;
            set
            {
                if (_selectedProductGroup != value)
                {
                    _selectedProductGroup = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool IsShowCurrentStockOnly
        {
            get => _isshowCurrentStockOnly;
            set
            {
                if (_isshowCurrentStockOnly != value)
                {
                    _isshowCurrentStockOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        // 선택 항목 속성
        public InventoryModel SelectedInventory
        {
            get => _selectedInventory;
            set
            {
                if (_selectedInventory != value)
                {
                    _selectedInventory = value;
                    OnPropertyChanged();
                }
            }
        }

        // 차트 관련 속성
        public string[] TrendChartLabels { get; private set; }
        public string[] GroupChartLabels { get; private set; }
        public Func<double, string> QuantityFormatter => value => value.ToString("N0");
        #endregion


        #region Collections
        // 데이터 컬렉션
        public ObservableCollection<InventoryModel> Inventories { get; }
        public ObservableCollection<string> ProductGroups { get; }
        public ObservableCollection<string> QuantityFilters { get; }

        // 차트 컬렉션
        public SeriesCollection ProductGroupChartData { get; }
        public SeriesCollection InventoryTrendChartData { get; }
        public SeriesCollection QuantityPieChartData { get; }
        #endregion


        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RegisterReceiptCommand { get; }
        public ICommand RegisterShipmentCommand { get; }
        public ICommand AdjustInventoryCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async Task LoadInitialData()
        {
            try
            {
                await Task.WhenAll(
                    LoadProductGroups(),
                    ExecuteSearch()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadProductGroups()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT DISTINCT product_group FROM dt_product ORDER BY product_group";
                var groups = await conn.QueryAsync<string>(sql);



                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductGroups.Clear();
                    ProductGroups.Add("전체");
                    foreach (var group in groups)
                    {
                        ProductGroups.Add(group);
                    }
                });
            }
        }
        private async Task LoadChartData()
        {
            try
            {
                var chartData = await _chartService.LoadAllChartDataAsync();

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ProductGroupChartData.Clear();
                    foreach (var series in chartData.ProductGroupChart)
                    {
                        ProductGroupChartData.Add(series);
                    }

                    InventoryTrendChartData.Clear();
                    foreach (var series in chartData.InventoryTrendChart)
                    {
                        InventoryTrendChartData.Add(series);
                    }

                    QuantityPieChartData.Clear();
                    foreach (var series in chartData.QuantityPieChart)
                    {
                        QuantityPieChartData.Add(series);
                    }

                    TrendChartLabels = chartData.TrendChartLabels;
                    GroupChartLabels = chartData.GroupChartLabels; 
                    OnPropertyChanged();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"차트 데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 실행 메서드
        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    string sql;
                    if (IsShowCurrentStockOnly)
                    {
                        sql = @"
    WITH LatestTransactions AS (
        SELECT 
            i.product_code,
            i.transaction_type,
            i.responsible_person,
            i.remarks,
            i.transaction_date,
            ROW_NUMBER() OVER (PARTITION BY i.product_code ORDER BY i.transaction_date DESC) as rn
        FROM dt_inventory_management i
        JOIN dt_product p ON i.product_code = p.product_code
        WHERE 1=1";

                        if (!string.IsNullOrEmpty(SelectedProductGroup) && SelectedProductGroup != "전체")
                        {
                            sql += " AND p.product_group = @ProductGroup";
                        }

                        if (!string.IsNullOrEmpty(ProductNameFilter))
                        {
                            sql += " AND p.product_name LIKE @ProductName";
                        }

                        sql += @"
    )
    SELECT 
        p.product_code AS ProductCode,
        p.product_group AS ProductGroup,
        p.product_name AS ProductName,
        COALESCE(SUM(CASE 
            WHEN i.transaction_type = '입고' THEN i.inventory_quantity
            WHEN i.transaction_type = '출고' THEN -i.inventory_quantity
            ELSE 0
        END), 0) AS TotalQuantity,
        p.unit AS Unit,
        p.price AS UnitPrice,
        COALESCE(SUM(CASE 
            WHEN i.transaction_type = '입고' THEN i.inventory_quantity
            WHEN i.transaction_type = '출고' THEN -i.inventory_quantity
            ELSE 0
        END), 0) * p.price AS TotalValue,
        MAX(lt.transaction_date) AS LastUpdateDate,
        MAX(lt.transaction_type) AS TransactionType,
        MAX(lt.responsible_person) AS ResponsiblePerson,
        MAX(lt.remarks) AS Remarks
    FROM dt_product p
    LEFT JOIN dt_inventory_management i ON p.product_code = i.product_code
    LEFT JOIN LatestTransactions lt ON p.product_code = lt.product_code AND lt.rn = 1
    WHERE 1=1";

                        if (!string.IsNullOrEmpty(SelectedProductGroup) && SelectedProductGroup != "전체")
                        {
                            sql += " AND p.product_group = @ProductGroup";
                        }

                        if (!string.IsNullOrEmpty(ProductNameFilter))
                        {
                            sql += " AND p.product_name LIKE @ProductName";
                        }

                        sql += @" 
    GROUP BY 
        p.product_code, 
        p.product_group, 
        p.product_name, 
        p.unit, 
        p.price
    HAVING TotalQuantity > 0";  // 현재고가 0보다 큰 것만 표시
                    }
                    else
                    {
                        // 기존 쿼리 사용
                        sql = @"
SELECT 
    i.product_code as ProductCode,
    p.product_group as ProductGroup,
    p.product_name as ProductName,
    i.inventory_quantity as TotalQuantity,
    i.unit as Unit,
    p.price as UnitPrice,
    i.inventory_quantity * p.price as TotalValue,
    i.transaction_date as LastUpdateDate,
    i.transaction_type as TransactionType,
    i.responsible_person as ResponsiblePerson,
    i.remarks as Remarks
FROM dt_inventory_management i
JOIN dt_product p ON i.product_code = p.product_code
WHERE 1=1";
                    }

                    var parameters = new DynamicParameters();

                    if (!string.IsNullOrEmpty(SelectedProductGroup) && SelectedProductGroup != "전체")
                    {
                        sql += " AND p.product_group = @ProductGroup";
                        parameters.Add("@ProductGroup", SelectedProductGroup);
                    }

                    if (!string.IsNullOrEmpty(ProductNameFilter))
                    {
                        sql += " AND p.product_name LIKE @ProductName";
                        parameters.Add("@ProductName", $"%{ProductNameFilter}%");
                    }


                    sql += " ORDER BY p.product_group, p.product_name";

                    var result = await conn.QueryAsync<InventoryModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Inventories.Clear();
                        foreach (var item in result)
                        {
                            Inventories.Add(item);
                        }
                    });

                    await LoadChartData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task ExecuteDelete()//삭제용
        {
            if (IsShowCurrentStockOnly)
            {
                MessageBox.Show("통합제거에선 삭제가 불가능합니다", "경고");
                return;
            }

            if (MessageBox.Show("선택한 항목을 삭제하시겠습니까?", "삭제 확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                var selectedItems = Inventories.Where(x => x.IsSelected).ToList();
                if (!selectedItems.Any())
                {
                    MessageBox.Show("삭제할 항목을 선택해주세요.", "알림");
                    return;
                }

                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var item in selectedItems)
                    {
                        var sql = "DELETE FROM dt_inventory_management " +
                            "WHERE product_code = @ProductCode AND inventory_quantity = @InventoryQuantity;";
                        await conn.ExecuteAsync(sql, new { ProductCode = item.ProductCode, InventoryQuantity = item.TotalQuantity });


                        // 로그 기록
                        string actionDetail = $"제품: {item.ProductName}, 수량: {item.TotalQuantity}, 단위: {item.Unit}";
                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "재고 삭제", actionDetail);
                    }
                }

                MessageBox.Show("선택한 항목이 삭제되었습니다.", "알림");
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}", "오류",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private bool CanExecuteDelete()
        {
            return Inventories.Any(x => x.IsSelected);
        }
        public async void LoadDataForEdit(InventoryModel selectedInventory)
        {
            if (selectedInventory == null) return;

            try
            {
                // 이미 창이 열려 있는 경우 활성화
                if (_currentWindow != null && _currentWindow.IsLoaded)
                {
                    _currentWindow.Activate();
                    return;
                }

                // ViewModel 설정
                var viewModel = new InventoryManagementViewModel();
                await viewModel.LoadInventoryForEdit(selectedInventory);
                //_currentWindow.DataContext = viewModel;

                viewModel.RequestClose += (s, e) =>
                {
                    if (_currentWindow != null)
                    {
                        _currentWindow.DialogResult = true;
                        _currentWindow.Close();
                    }
                };

                // 새로운 창 생성
                _currentWindow = new InventoryManagementWindow(selectedInventory)
                {
                    //Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = "재고수정",
                    DataContext = viewModel
                };


                // 창 닫힘 이벤트 처리
                _currentWindow.Closed += async (s, e) =>
                {
                    if (_currentWindow.DialogResult == true)
                    {
                        // 창이 성공적으로 닫힌 경우 데이터 갱신
                        await ExecuteSearch();
                        await LoadChartData();
                    }
                    _currentWindow = null; // 창 인스턴스 해제
                };

                // 창을 모달로 열기
                _currentWindow.ShowDialog();
                if (_currentWindow != null)
                {
                    _currentWindow.Owner = Application.Current.MainWindow;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"재고 조정 창을 여는 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 속성 변경 알림
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}