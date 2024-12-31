using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Dapper;
using MES.Solution.Helpers;
using MES.Solution.Models;
using MES.Solution.Services;
using MES.Solution.Views;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;


namespace MES.Solution.ViewModels
{
    public class ProductionPlanViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;
        private readonly ProductionPlanService _service;

        // 상태 관련
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today;
        private string _selectedLine = "전체";
        private string _selectedProduct = "전체";
        private string _selectedStatus = "전체";
        private ProductionPlanModel _selectedPlan;
        private ObservableCollection<ProductionPlanModel> _productionPlans;
        private bool _isAllWorkOrdersChecked;
        private bool _isCompletedOnlyChecked;

        // 시뮬레이션 관련
        private readonly ProductionSimulationService _simulationService;
        private string _selectedMode = "수동";
        private bool _isAutoMode;
        private string _simulationStatus;

        // 창 관련
        private ProductionPlanAddWindow _currentAddWindow;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public ProductionPlanViewModel()
        {
            // 서비스 초기화
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _service = new ProductionPlanService();
            _logService = new LogService();
            _simulationService = new ProductionSimulationService(_connectionString);

            // 컬렉션 초기화
            ProductionPlans = new ObservableCollection<ProductionPlanModel>();
            ProductionLines = new ObservableCollection<string>();
            Products = new ObservableCollection<string>();
            Statuses = new ObservableCollection<string>();

            // 기본값 설정
            IsAllWorkOrdersChecked = false;
            IsCompletedOnlyChecked = false;
            StartDate = DateTime.Today;
            EndDate = DateTime.Now.AddDays(1 - DateTime.Now.Day).AddMonths(1).AddDays(-1);//이번달 말일

            // 명령 초기화
            SearchCommand = new AsyncRelayCommand(async () => await ExecuteSearch());
            DeleteCommand = new AsyncRelayCommand(async () => await ExecuteDelete(), CanExecuteDelete);
            AddCommand = new RelayCommand(ExecuteAdd);
            ViewDetailCommand = new RelayCommand<ProductionPlanModel>(ExecuteViewDetail);
            StartSimulationCommand = new RelayCommand(ExecuteStartSimulation, CanExecuteSimulationCommand);
            PauseSimulationCommand = new RelayCommand(ExecutePauseSimulation, CanExecutePauseCommand);
            ResumeSimulationCommand = new RelayCommand(ExecuteResumeSimulation, CanExecuteResumeCommand);
            StopSimulationCommand = new RelayCommand(ExecuteStopSimulation, CanExecuteStopCommand);
            ConfirmAutoModeCommand = new RelayCommand(ExecuteConfirmAutoMode, CanExecuteConfirmMode);
            ConfirmManualModeCommand = new RelayCommand(ExecuteConfirmManualMode, CanExecuteConfirmMode);

            // 이벤트 핸들러 등록
            _simulationService.ProductionUpdated += OnProductionUpdated;
            _simulationService.SimulationError += OnSimulationError;

            // 초기 데이터 로드
            InitializeAsync();


        }
        #endregion


        #region Properties
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
                if (_endDate < value)
                {
                    EndDate = value;
                }
            }
        }
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
                if (_startDate > value)
                {
                    StartDate = value;
                }
            }
        }
        public string SelectedLine
        {
            get => _selectedLine;
            set
            {
                _selectedLine = value;
                OnPropertyChanged();
            }
        }
        public string SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
            }
        }
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
            }
        }
        public ProductionPlanModel SelectedPlan
        {
            get => _selectedPlan;
            set
            {
                if (_selectedPlan != value)
                {
                    _selectedPlan = value;
                    OnPropertyChanged();
                    // 선택된 계획이 변경될 때 IsAutoMode 업데이트
                    IsAutoMode = _selectedPlan?.SimulationMode == "자동";
                    // Command 상태 갱신
                    (ConfirmAutoModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ConfirmManualModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        public ObservableCollection<ProductionPlanModel> ProductionPlans
        {
            get => _productionPlans;
            set
            {
                _productionPlans = value;
                OnPropertyChanged();
            }
        }
        public bool IsAllWorkOrdersChecked
        {
            get => _isAllWorkOrdersChecked;
            set
            {
                if (_isAllWorkOrdersChecked != value)
                {
                    _isAllWorkOrdersChecked = value;
                    OnPropertyChanged();
                    _ = ExecuteSearch();
                }
            }
        }
        public bool IsCompletedOnlyChecked
        {
            get => _isCompletedOnlyChecked;
            set
            {
                if (_isCompletedOnlyChecked != value)
                {
                    _isCompletedOnlyChecked = value;
                    OnPropertyChanged();
                    _ = ExecuteSearch();
                }
            }
        }

        //시뮬레이션 관련
        public string ProductionMode { get; set; } // "자동" 또는 "수동"
        public string SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value)
                {
                    _selectedMode = value;
                    OnPropertyChanged();
                }
            }
        }
        public string SimulationStatus
        {
            get => _simulationStatus;
            set
            {
                if (_simulationStatus != value)
                {
                    _simulationStatus = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool IsAutoMode
        {
            get => _isAutoMode;
            set
            {
                if (_isAutoMode != value)
                {
                    _isAutoMode = value;
                    OnPropertyChanged();
                }
            }
        }
        #endregion


        #region Collections
        public ObservableCollection<string> ProductionLines { get; private set; }
        public ObservableCollection<string> Products { get; private set; }
        public ObservableCollection<string> Statuses { get; private set; }
        public ObservableCollection<string> ProductionModes { get; } = new ObservableCollection<string>
{ "자동", "수동" };
        #endregion


        #region Commands
        public ICommand SearchCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand ViewDetailCommand { get; private set; }
        // 시뮬레이션 관련
        public ICommand StartSimulationCommand { get; }
        public ICommand PauseSimulationCommand { get; }
        public ICommand ResumeSimulationCommand { get; }
        public ICommand StopSimulationCommand { get; }
        public ICommand ConfirmAutoModeCommand { get; }
        public ICommand ConfirmManualModeCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async void InitializeAsync()
        {
            await LoadComboBoxData();  // 콤보박스 데이터 로드
            await ExecuteSearch();     // 초기 검색 수행
        }

        private async Task LoadComboBoxData()
        {
            try
            {
                var lines = await _service.GetProductionLines();
                ProductionLines.Clear();
                ProductionLines.Add("전체");
                foreach (var line in lines) ProductionLines.Add(line);

                var products = await _service.GetProducts();
                Products.Clear();
                Products.Add("전체");
                foreach (var product in products) Products.Add(product);

                var statuses = await _service.GetStatuses();
                Statuses.Clear();
                foreach (var status in statuses) Statuses.Add(status);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 데이터 조작 메서드
        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
                            SELECT 
                                pp.work_order_code AS PlanNumber,
                                pp.production_date AS PlanDate,
                                pp.production_line AS ProductionLine,
                                pp.product_code AS ProductCode,
                                p.product_name AS ProductName,
                                pp.order_quantity AS PlannedQuantity,
                                pp.production_quantity AS ProductionQuantity,
                                pp.work_shift AS WorkShift,
                                IF(pp.order_quantity > 0, 
                                   (pp.production_quantity * 100 / pp.order_quantity), 
                                   0) AS AchievementRate,
                                pp.process_status AS Status,
                                pp.remarks AS Remarks,
                                pp.simulation_mode AS SimulationMode 
                            FROM dt_production_plan pp
                            JOIN dt_product p ON pp.product_code = p.product_code
                            WHERE 1=1";

                    var parameters = new DynamicParameters();

                    // 전체 기간 보기가 체크되지 않은 경우, 오늘 날짜만 표시
                    if (!IsAllWorkOrdersChecked)
                    {
                        sql += " AND pp.production_date BETWEEN @StartDate AND @EndDate";
                        parameters.Add("@StartDate", StartDate.Date);
                        parameters.Add("@EndDate", EndDate.Date);
                    }

                    // 생산라인 필터
                    if (!string.IsNullOrEmpty(SelectedLine) && SelectedLine != "전체")
                    {
                        sql += " AND pp.production_line = @ProductionLine";
                        parameters.Add("@ProductionLine", SelectedLine);
                    }

                    // 제품 필터
                    if (!string.IsNullOrEmpty(SelectedProduct) && SelectedProduct != "전체")
                    {
                        sql += " AND p.product_name = @ProductName";
                        parameters.Add("@ProductName", SelectedProduct);
                    }

                    // 완료 항목 필터
                    if (IsCompletedOnlyChecked)
                    {
                        sql += " AND pp.process_status = '완료'";
                    }
                    else if (!string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "전체")
                    {
                        sql += " AND pp.process_status = @Status";
                        parameters.Add("@Status", SelectedStatus);
                    }

                    sql += " ORDER BY pp.production_date DESC, pp.work_order_sequence ASC";

                    var result = await conn.QueryAsync<ProductionPlanModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProductionPlans.Clear();
                        foreach (var plan in result)
                        {
                            ProductionPlans.Add(plan);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"검색 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteDelete()
        {
            if (MessageBox.Show("선택한 항목들을 삭제하시겠습니까?", "삭제", MessageBoxButton.YesNo) == MessageBoxResult.No)
            {
                MessageBox.Show("취소되었습니다", "취소");
                return;
            }
            try
            {
                var selectedItems = ProductionPlans.Where(x => x.IsSelected).ToList();
                if (!selectedItems.Any())
                {
                    MessageBox.Show("삭제할 항목이 선택되지 않았습니다.", "알림");
                    return;
                }

                foreach (var item in selectedItems)
                {
                    await _service.DeleteProductionPlan(item.PlanNumber);

                    // 로그 저장
                    string actionDetail = $"작업지시번호: {item.PlanNumber}, " +
                                        $"생산일자: {item.PlanDate:yyyy-MM-dd}, " +
                                        $"생산라인: {item.ProductionLine}, " +
                                        $"수량: {item.ProductionQuantity}, " +
                                        $"제품: {item.ProductName}";

                    await _logService.SaveLogAsync(App.CurrentUser.UserId, "생산계획 삭제", actionDetail);
                }

                MessageBox.Show("선택한 항목이 삭제되었습니다.", "알림");
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"삭제 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        public void ExecuteAdd()
        {
            ShowAddWindow(false);
        }

        private void ExecuteViewDetail(ProductionPlanModel plan)
        {
            if (plan != null)
            {
                // 비고 내용을 보여주는 메시지 박스 표시
                MessageBox.Show($"비고 내용:\n{plan.Remarks ?? "비고 없음"}",
                              "비고 보기",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        // 상태 확인 메서드
        private bool CanExecuteDelete()
        {
            var selectedItemsWithInProgress = ProductionPlans.Where(x => x.IsSelected && x.Status == "작업중").ToList();
            if (selectedItemsWithInProgress.Any())
            {
                foreach (var item in selectedItemsWithInProgress)
                {
                    item.IsSelected = false;
                }
                MessageBox.Show("작업중인 작업은 삭제할 수 없습니다.", "경고");
                OnPropertyChanged();
                return false;
            }

            return ProductionPlans.Any(x => x.IsSelected);
        }
        private bool CanExecuteSimulationCommand()
        {
            return SelectedPlan != null &&
                   SelectedPlan.Status == "대기" &&
                   IsAutoMode;
        }
        private bool CanExecutePauseCommand()
        {
            return SelectedPlan != null &&
                   SelectedPlan.Status == "작업중" &&
                   IsAutoMode;
        }
        private bool CanExecuteResumeCommand()
        {
            return SelectedPlan != null &&
                   SelectedPlan.Status == "일시정지" &&
                   IsAutoMode;
        }
        private bool CanExecuteStopCommand()
        {
            return SelectedPlan != null &&
                   (SelectedPlan.Status == "작업중" ||
                    SelectedPlan.Status == "일시정지") &&
                   IsAutoMode;
        }
        private bool CanExecuteConfirmMode()
        {
            return SelectedPlan != null &&
                   SelectedPlan.Status == "대기";
        }

        // 시뮬레이션 관련 메서드
        private async void ExecuteStartSimulation()
        {
            try
            {
                if (SelectedPlan == null) return;

                var result = MessageBox.Show(
                    "시뮬레이션을 시작하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                await _simulationService.StartSimulation(SelectedPlan.PlanNumber);
                SimulationStatus = "시뮬레이션 실행 중...";

                // 로그 기록
                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 시작",
                    $"작업지시번호: {SelectedPlan.PlanNumber}, 제품: {SelectedPlan.ProductName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시뮬레이션 시작 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }
        private async void ExecutePauseSimulation()
        {
            try
            {
                if (SelectedPlan == null) return;
                await _simulationService.PauseSimulation(SelectedPlan.PlanNumber);
                SimulationStatus = "시뮬레이션 일시정지";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 일시정지",
                    $"작업지시번호: {SelectedPlan.PlanNumber}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시뮬레이션 일시정지 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }
        private async void ExecuteResumeSimulation()
        {
            try
            {
                if (SelectedPlan == null) return;
                await _simulationService.ResumeSimulation(SelectedPlan.PlanNumber);
                SimulationStatus = "시뮬레이션 재시작 중...";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 재시작",
                    $"작업지시번호: {SelectedPlan.PlanNumber}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시뮬레이션 재시작 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }
        private async void ExecuteStopSimulation()
        {
            try
            {
                if (SelectedPlan == null) return;

                var result = MessageBox.Show(
                    "시뮬레이션을 중지하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                await _simulationService.StopSimulation(SelectedPlan.PlanNumber);
                SimulationStatus = "시뮬레이션 중지됨";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 중지",
                    $"작업지시번호: {SelectedPlan.PlanNumber}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시뮬레이션 중지 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }
        private async void ExecuteConfirmAutoMode()
        {
            try
            {
                if (SelectedPlan == null)
                {
                    MessageBox.Show("생산계획을 선택해주세요.", "알림");
                    return;
                }

                if (SelectedPlan.Status != "대기")
                {
                    MessageBox.Show("대기 상태의 계획만 모드를 변경할 수 있습니다.", "알림");
                    return;
                }

                await _service.SetSimulationMode(SelectedPlan.PlanNumber, "자동");

                // UI 업데이트
                SelectedPlan.SimulationMode = "자동";
                IsAutoMode = true;
                OnPropertyChanged(nameof(SelectedPlan));

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "자동모드 설정",
                    $"작업지시번호: {SelectedPlan.PlanNumber}");

                // 목록 갱신 및 선택 유지
                string currentPlanNumber = SelectedPlan.PlanNumber;
                await ExecuteSearch();
                SelectedPlan = ProductionPlans.FirstOrDefault(p => p.PlanNumber == currentPlanNumber);

                MessageBox.Show("자동 모드로 설정되었습니다.", "알림");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"자동모드 설정 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }
        private async void ExecuteConfirmManualMode()
        {
            try
            {
                if (SelectedPlan == null)
                {
                    MessageBox.Show("생산계획을 선택해주세요.", "알림");
                    return;
                }

                if (SelectedPlan.Status != "대기")
                {
                    MessageBox.Show("대기 상태의 계획만 모드를 변경할 수 있습니다.", "알림");
                    return;
                }

                await _service.SetSimulationMode(SelectedPlan.PlanNumber, "수동");

                // UI 업데이트
                SelectedPlan.SimulationMode = "수동";
                IsAutoMode = false;
                OnPropertyChanged(nameof(SelectedPlan));

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "수동모드 설정",
                    $"작업지시번호: {SelectedPlan.PlanNumber}");

                // 목록 갱신 및 선택 유지
                string currentPlanNumber = SelectedPlan.PlanNumber;
                await ExecuteSearch();
                SelectedPlan = ProductionPlans.FirstOrDefault(p => p.PlanNumber == currentPlanNumber);

                MessageBox.Show("수동 모드로 설정되었습니다.", "알림");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"수동모드 설정 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        // 시뮬레이션 상태관련
        private void OnProductionUpdated(object sender, ProductionEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var plan = ProductionPlans.FirstOrDefault(p => p.PlanNumber == e.WorkOrderCode);
                if (plan != null)
                {
                    plan.ProductionQuantity = e.CurrentQuantity;
                    plan.AchievementRate = (decimal)e.CurrentQuantity / e.TargetQuantity * 100;
                    OnPropertyChanged(nameof(ProductionPlans));
                }

                // 생산완료 시 처리
                if (e.CurrentQuantity >= e.TargetQuantity)
                {
                    SimulationStatus = "생산 완료";
                    await _logService.SaveLogAsync(
                        App.CurrentUser.UserId,
                        "생산 완료",
                        $"작업지시번호: {e.WorkOrderCode}, 최종생산량: {e.CurrentQuantity}");
                }
            });
        }
        private void OnSimulationError(object sender, string errorMessage)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(errorMessage, "시뮬레이션 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        // 창 관련 메서드
        private void ShowAddWindow(bool isEdit = false)
        {
            try
            {
                if (_currentAddWindow != null && _currentAddWindow.IsLoaded)
                {
                    _currentAddWindow.Activate();
                    return;
                }
                
                _currentAddWindow = new ProductionPlanAddWindow(isEdit)
                {
                    //Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = isEdit ? "생산계획 수정" : "생산계획 등록"  // 창 제목 설정
                };

                _currentAddWindow.Closed += async (s, e) =>
                {
                    if (_currentAddWindow.DialogResult == true)  // DialogResult 확인
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

        public void LoadDataForEdit(ProductionPlanModel selectedPlan)
        {
            if (selectedPlan == null)
                return;

            try
            {
                if (_currentAddWindow != null && _currentAddWindow.IsLoaded)
                {
                    _currentAddWindow.Activate();
                    return;
                }

                _currentAddWindow = new ProductionPlanAddWindow(true) // isEdit = true로 설정
                {
                    //Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Title = "생산계획 수정"
                };

                var viewModel = _currentAddWindow.DataContext as ProductionPlanAddViewModel;
                if (viewModel != null)
                {
                    viewModel.Mode = FormMode.Edit;  // 수정 모드로 설정
                    viewModel.LoadData(new ProductionPlanModel
                    {
                        PlanNumber = selectedPlan.PlanNumber,
                        PlanDate = selectedPlan.PlanDate,
                        ProductionLine = selectedPlan.ProductionLine,
                        ProductCode = selectedPlan.ProductCode,
                        ProductName = selectedPlan.ProductName,
                        PlannedQuantity = selectedPlan.PlannedQuantity,
                        WorkShift = selectedPlan.WorkShift,
                        Status = selectedPlan.Status,
                        Remarks = selectedPlan.Remarks
                    });
                }

                _currentAddWindow.Closed += async (s, e) =>
                {
                    if (_currentAddWindow.DialogResult == true)
                    {
                        await ExecuteSearch();  // 목록 새로고침
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

        // 유틸리티 메서드
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            // 여기에서 필요에 따라 정리 작업을 수행합니다.
            // 현재 상황에서는 아무 작업도 수행하지 않아도 무방합니다.
        }
        #endregion

    }
}
