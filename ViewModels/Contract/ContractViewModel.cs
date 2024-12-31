using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using MES.Solution.Helpers;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Threading.Tasks;
using System.Linq;
using MES.Solution.Services;
using MES.Solution.Views;
using MES.Solution.Models;

namespace MES.Solution.ViewModels
{
    public class ContractViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;

        // 검색 조건 필드
        private DateTime _startDate = DateTime.Now.AddDays(1 - DateTime.Now.Day); //이번달 1일 
        private DateTime _endDate = DateTime.Now.AddDays(1 - DateTime.Now.Day).AddMonths(1).AddDays(-1);//이번달 말일
        private string _selectedCompany = "전체";
        private string _selectedProduct = "전체";
        private string _selectedStatus = "전체";

        // 선택 항목 관련
        private ContractModel _selectedContract;
        private ContractAddWindow _currentAddWindow;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public ContractViewModel()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _logService = new LogService();

            // 컬렉션 초기화
            Contracts = new ObservableCollection<ContractModel>();
            Companies = new ObservableCollection<string>();
            Products = new ObservableCollection<string>();
            StatusOptions = new ObservableCollection<string> { "전체", "대기", "확정", "취소" };

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            AddCommand = new RelayCommand(ExecuteAdd);
            DeleteCommand = new RelayCommand(async () => await ExecuteDelete(), CanExecuteDelete);
            ConfirmContractCommand = new RelayCommand(async () => await ExecuteConfirmContract(), CanExecuteConfirmContract);
            CancelContractCommand = new RelayCommand(async () => await ExecuteCancelContract(), CanExecuteCancelContract);

            // 초기 데이터 로드
            LoadInitialData();
        }
        #endregion


        #region Properties
        // 검색 조건 속성
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (_startDate != value)
                {
                    _startDate = value;
                    OnPropertyChanged();
                    if (_startDate > EndDate)
                    {
                        EndDate = _startDate;
                    }
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (_endDate != value)
                {
                    _endDate = value;
                    OnPropertyChanged();
                    if (_endDate < StartDate)
                    {
                        StartDate = _endDate;
                    }
                }
            }
        }

        public string SelectedCompany
        {
            get => _selectedCompany;
            set
            {
                if (_selectedCompany != value)
                {
                    _selectedCompany = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct != value)
                {
                    _selectedProduct = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus != value)
                {
                    _selectedStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public ContractModel SelectedContract
        {
            get => _selectedContract;
            set
            {
                if (_selectedContract != value)
                {
                    _selectedContract = value;
                    OnPropertyChanged();
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ConfirmContractCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelContractCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        #endregion


        #region Collections
        // UI 바인딩용 컬렉션
        public ObservableCollection<ContractModel> Contracts { get; }
        public ObservableCollection<string> Companies { get; }
        public ObservableCollection<string> Products { get; }
        public ObservableCollection<string> StatusOptions { get; }
        #endregion


        #region Commands
        // UI 액션 커맨드
        public ICommand SearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ConfirmContractCommand { get; }
        public ICommand CancelContractCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async void LoadInitialData()
        {
            try
            {
                await LoadCompanies();
                await LoadProducts();
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCompanies()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT DISTINCT company_name FROM dt_contract ORDER BY company_name";
                var companies = await conn.QueryAsync<string>(sql);

                Companies.Clear();
                Companies.Add("전체");
                foreach (var company in companies)
                {
                    Companies.Add(company);
                }
            }
        }

        private async Task LoadProducts()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = @"
                    SELECT DISTINCT p.product_name 
                    FROM dt_contract c
                    JOIN dt_product p ON c.product_code = p.product_code 
                    ORDER BY p.product_name";
                var products = await conn.QueryAsync<string>(sql);

                Products.Clear();
                Products.Add("전체");
                foreach (var product in products)
                {
                    Products.Add(product);
                }
            }
        }

        // 실행 메서드
        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
                    SELECT 
                        c.order_number as OrderNumber,
                        c.order_date as OrderDate,
                        c.company_code as CompanyCode,
                        c.company_name as CompanyName,
                        c.product_code as ProductCode,
                        p.product_name as ProductName,
                        c.quantity as Quantity,
                        c.delivery_date as DeliveryDate,
                        c.remarks as Remarks,
                        c.employee_name as EmployeeName,
                        c.status as Status,
                        p.unit as Unit
                    FROM dt_contract c
                    JOIN dt_product p ON c.product_code = p.product_code
                    WHERE c.order_date BETWEEN @StartDate AND @EndDate";

                    var parameters = new DynamicParameters();
                    parameters.Add("@StartDate", StartDate.Date);
                    parameters.Add("@EndDate", EndDate.Date);

                    if (SelectedCompany != "전체")
                    {
                        sql += " AND c.company_name = @CompanyName";
                        parameters.Add("@CompanyName", SelectedCompany);
                    }

                    if (SelectedProduct != "전체")
                    {
                        sql += " AND p.product_name = @ProductName";
                        parameters.Add("@ProductName", SelectedProduct);
                    }

                    if (SelectedStatus != "전체")
                    {
                        sql += " AND c.status = @Status";
                        parameters.Add("@Status", SelectedStatus);
                    }

                    sql += " ORDER BY c.order_date DESC, c.order_number";

                    var result = await conn.QueryAsync<ContractModel>(sql, parameters);

                    Contracts.Clear();
                    foreach (var contract in result)
                    {
                        Contracts.Add(contract);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteAdd()
        {
            ShowAddWindow(false);
        }

        private async Task ExecuteDelete()
        {
            var selectedItems = Contracts.Where(x => x.IsSelected && x.Status == "대기").ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("삭제할 수 있는 항목이 없습니다.\n확정 또는 취소된 수주는 삭제할 수 없습니다.", "알림");
                return;
            }

            if (MessageBox.Show("선택한 항목을 삭제하시겠습니까?", "삭제 확인",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var item in selectedItems)
                    {
                        var sql = "DELETE FROM dt_contract WHERE order_number = @OrderNumber";
                        await conn.ExecuteAsync(sql, new { OrderNumber = item.OrderNumber });

                        // 로그 저장
                        string actionDetail = $"수주번호: {item.OrderNumber}, " +
                                           $"주문일자: {item.OrderDate:yyyy-MM-dd}, " +
                                           $"거래처: {item.CompanyName}, " +
                                           $"제품: {item.ProductName}, " +
                                           $"수량: {item.Quantity}";

                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "수주내역 삭제", actionDetail);
                    }
                }

                MessageBox.Show("선택한 항목이 삭제되었습니다.", "알림");
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteConfirmContract()//
        {
            // 체크된 항목들과 선택된 항목 모두 가져오기
            var selectedItems = Contracts.Where(x =>
                (x.IsSelected || x == SelectedContract) && x.Status == "대기"
            ).Distinct().ToList();

            if (!selectedItems.Any())
            {
                MessageBox.Show("확정할 수 있는 항목이 없습니다.\n대기 상태의 수주만 확정할 수 있습니다.", "알림");
                return;
            }

            // 재고 서비스 인스턴스 생성
            var inventoryService = new InventoryChartService(_connectionString);

            // 각 선택된 수주에 대해 처리
            foreach (var contract in selectedItems)
            {
                try
                {
                    // 1. 현재 재고 확인
                    int currentStock = await inventoryService.GetCurrentStock(contract.ProductCode);
                    int requiredQuantity = contract.Quantity;

                    using (var conn = new MySqlConnection(_connectionString))
                    {
                        await conn.OpenAsync();
                        using (var transaction = conn.BeginTransaction())
                        {
                            try
                            {
                                if (currentStock >= requiredQuantity)
                                {
                                    // 수주 추가 확인
                                    if (MessageBox.Show(
                                        $"제품 '{contract.ProductName}'의 수주를 확정하시겠습니까?\n" +
                                        $"현재고: {currentStock}\n" +
                                        $"필요수량: {requiredQuantity}\n", "수주확정", MessageBoxButton.OKCancel)
                                        == MessageBoxResult.Cancel)
                                    {
                                        return;
                                    }

                                    // 재고가 충분한 경우
                                    // 1. 수주 상태 업데이트
                                    var updateSql = @"
        UPDATE dt_contract 
        SET status = '확정' 
        WHERE order_number = @OrderNumber";

                                    await conn.ExecuteAsync(updateSql,
                                        new { OrderNumber = contract.OrderNumber },
                                        transaction);

                                    // 2. 출하 등록 (재고 차감 없이 대기 상태로만 등록)
                                    var sequenceQuery = @"
        SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(shipment_number, '-', -1) AS UNSIGNED)), 0) + 1
        FROM dt_shipment 
        WHERE DATE(shipment_date) = @ShipmentDate";

                                    var nextSequence = await conn.QuerySingleAsync<int>(sequenceQuery,
                                        new { ShipmentDate = contract.DeliveryDate });
                                    var shipmentNumber = $"SH-{contract.DeliveryDate:yyyyMMdd}-{nextSequence:D3}";

                                    var insertShipmentSql = @"
        INSERT INTO dt_shipment (
            shipment_number, company_code, company_name, 
            product_code, production_date, shipment_date, 
            shipment_quantity, vehicle_number, employee_name, status
        ) VALUES (
            @ShipmentNumber, @CompanyCode, @CompanyName,
            @ProductCode, @ProductionDate, @ShipmentDate,
            @Quantity, @VehicleNumber, @EmployeeName, '대기'
        )";

                                    await conn.ExecuteAsync(insertShipmentSql, new
                                    {
                                        ShipmentNumber = shipmentNumber,
                                        CompanyCode = contract.CompanyCode,
                                        CompanyName = contract.CompanyName,
                                        ProductCode = contract.ProductCode,
                                        ProductionDate = DateTime.Now,
                                        ShipmentDate = contract.DeliveryDate,
                                        Quantity = requiredQuantity,
                                        VehicleNumber = "미정",  // 기본값
                                        EmployeeName = App.CurrentUser.UserName
                                    }, transaction);

                                    // 로그 저장
                                    await _logService.SaveLogAsync(
                                        App.CurrentUser.UserId,
                                        "수주확정_출하등록",
                                        $"수주번호: {contract.OrderNumber}, " +
                                        $"출하번호: {shipmentNumber}, " +
                                        $"현재고: {currentStock}, " +
                                        $"예정수량: {requiredQuantity}"
                                    );
                                }
                                else
                                {
                                    // 재고가 부족한 경우 - 생산계획 생성
                                    int productionQuantity = requiredQuantity - currentStock;
                                    MessageBox.Show(
                                        $"제품 '{contract.ProductName}'의 재고가 부족합니다.\n" +
                                        $"현재고: {currentStock}\n" +
                                        $"필요수량: {requiredQuantity}\n" +
                                        $"생산필요: {productionQuantity}",
                                        "재고 부족 알림"
                                    );

                                    // 생산계획 입력 창 표시
                                    var planInputWindow = new ContractProductionPlanInputWindow(
                                        new ContractModel
                                        {
                                            OrderNumber = contract.OrderNumber,
                                            ProductCode = contract.ProductCode,
                                            ProductName = contract.ProductName,
                                            Quantity = productionQuantity,
                                            DeliveryDate = contract.DeliveryDate
                                        })
                                    {
                                        //Owner = Application.Current.MainWindow,
                                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                                    };
                                    

                                    if (planInputWindow.ShowDialog() == true)
                                    {
                                        // 수주 상태 업데이트
                                        var updateSql = @"
                                    UPDATE dt_contract 
                                    SET status = '확정' 
                                    WHERE order_number = @OrderNumber";

                                        await conn.ExecuteAsync(updateSql,
                                            new { OrderNumber = contract.OrderNumber },
                                            transaction);

                                        // 로그 저장
                                        await _logService.SaveLogAsync(
                                            App.CurrentUser.UserId,
                                            "수주확정_생산계획등록",
                                            $"수주번호: {contract.OrderNumber}, " +
                                            $"현재고: {currentStock}, " +
                                            $"생산계획수량: {productionQuantity}, " +
                                            $"총 필요수량: {requiredQuantity}"
                                        );

                                        if (_currentAddWindow != null)
                                        {
                                            _currentAddWindow.Owner = Application.Current.MainWindow;
                                        }
                                    }
                                    else
                                    {
                                        // 사용자가 취소한 경우
                                        transaction.Rollback();
                                        continue;
                                    }
                                }

                                transaction.Commit();
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"수주번호 {contract.OrderNumber}의 확정 처리 중 오류가 발생했습니다: {ex.Message}",
                                  "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            //MessageBox.Show("선택한 항목의 처리가 완료되었습니다.", "알림");
            await ExecuteSearch(); // 목록 새로고침
        }

        private async Task ExecuteCancelContract()
        {
            // 체크된 항목들과 선택된 항목 모두 가져오기
            var selectedItems = Contracts.Where(x =>
                (x.IsSelected || x == SelectedContract) && x.Status == "대기"
            ).Distinct().ToList();

            if (!selectedItems.Any())
            {
                MessageBox.Show("취소할 수 있는 항목이 없습니다.\n대기 상태의 수주만 취소할 수 있습니다.", "알림");
                return;
            }

            if (MessageBox.Show("선택한 항목을 취소하시겠습니까?", "수주 취소",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var item in selectedItems)
                    {
                        var sql = "UPDATE dt_contract SET status = '취소' WHERE order_number = @OrderNumber";
                        await conn.ExecuteAsync(sql, new { OrderNumber = item.OrderNumber });

                        // 로그 저장
                        string actionDetail = $"수주번호: {item.OrderNumber}, " +
                                           $"주문일자: {item.OrderDate:yyyy-MM-dd}, " +
                                           $"거래처: {item.CompanyName}, " +
                                           $"제품: {item.ProductName}, " +
                                           $"수량: {item.Quantity}";

                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "수주내역 취소", actionDetail);
                    }
                }

                MessageBox.Show("선택한 항목이 취소되었습니다.", "알림");
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"취소 처리 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // 상태 확인 메서드
        private bool CanExecuteDelete()
        {
            return Contracts.Any(x => x.IsSelected);
        }

        private bool CanExecuteConfirmContract()
        {
            // 선택된 항목이나 체크된 항목 중 "대기" 상태인 것이 있는지 확인
            return (SelectedContract != null && SelectedContract.Status == "대기") ||
                   Contracts.Any(x => x.IsSelected && x.Status == "대기");
        }

        private bool CanExecuteCancelContract()
        {
            // 선택된 항목이나 체크된 항목 중 "대기" 상태인 것이 있는지 확인
            return (SelectedContract != null && SelectedContract.Status == "대기") ||
                   Contracts.Any(x => x.IsSelected && x.Status == "대기");
        }

        // UI 관련 메서드 
        private void ShowAddWindow(bool isEdit = false)
        {
            try
            {
                if (_currentAddWindow != null && _currentAddWindow.IsLoaded)
                {
                    _currentAddWindow.Activate();
                    return;
                }

                _currentAddWindow = new ContractAddWindow(isEdit)
                {
                    //Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                _currentAddWindow.Closed += async (s, e) =>
                {
                    if (_currentAddWindow.DialogResult == true)
                    {
                        await ExecuteSearch();
                    }
                    _currentAddWindow = null;
                };

                _currentAddWindow.ShowDialog();
                if (_currentAddWindow != null)
                {
                    _currentAddWindow.Owner = Application.Current.MainWindow;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(isEdit ? "수정" : "등록")} 창을 여는 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadDataForEdit(ContractModel selectedContract)
        {
            if (selectedContract == null || selectedContract.Status != "대기")
            {
                MessageBox.Show("대기 상태의 수주만 수정할 수 있습니다.", "알림");
                return;
            }

            try
            {
                if (_currentAddWindow != null && _currentAddWindow.IsLoaded)
                {
                    _currentAddWindow.Activate();
                    return;
                }

                _currentAddWindow = new ContractAddWindow(true)
                {
                    //Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = "수주 수정"
                };

                var viewModel = _currentAddWindow.DataContext as ContractAddViewModel;
                if (viewModel != null)
                {
                    viewModel.LoadData(selectedContract);
                }

                _currentAddWindow.Closed += async (s, e) =>
                {
                    if (_currentAddWindow.DialogResult == true)
                    {
                        await ExecuteSearch();
                    }
                    _currentAddWindow = null;
                };

                _currentAddWindow.ShowDialog();
                if (_currentAddWindow != null)
                {
                    _currentAddWindow.Owner = Application.Current.MainWindow;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"수정 창을 여는 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // INotifyPropertyChanged 구현
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 리소스 정리
        public void Cleanup()
        {
            // 리소스 정리 필요시 여기에 구현
            _currentAddWindow?.Close();
        }
        #endregion
    }
}