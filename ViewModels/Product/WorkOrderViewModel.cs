using System;
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
using System.Linq;
using MES.Solution.Models;
using MES.Solution.Services;


namespace MES.Solution.ViewModels
{
    public class WorkOrderViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;
        private readonly ProductionSimulationService _simulationService;
        private readonly InventoryChartService _inventoryService;

        // 상태 관련
        private DateTime _workDate = DateTime.Today;
        private string _selectedShift;
        private string _selectedLine;
        private WorkOrderModel _selectedWorkOrder;
        private ObservableCollection<WorkOrderModel> _workOrders;
        private bool _isallWorkOrdersChecked;
        private bool _isCompletedOnlyChecked;

        // 시뮬레이션 관련
        private bool _isAutoMode;
        private string _simulationStatus;

        // 버튼 상태
        private bool _canStartWork;
        private bool _canCompleteWork;
        private bool _canCancelWork;

        // 명령 관련
        private ICommand _startWorkCommand;
        private ICommand _completeWorkCommand;
        private ICommand _cancelWorkCommand;
        private ICommand _restartWorkCommand;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public WorkOrderViewModel()
        {
            // 서비스 초기화
            _logService = new LogService();
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _simulationService = new ProductionSimulationService(_connectionString);
            _simulationService.ProductionUpdated += OnProductionUpdated;
            _simulationService.SimulationError += OnSimulationError;
            _inventoryService = new InventoryChartService(_connectionString);

            // 초기값 설정
            IsAllWorkOrdersChecked = false;
            IsCompletedOnlyChecked = false;
            WorkDate = DateTime.Now.Date;

            // 컬렉션 초기화
            WorkOrders = new ObservableCollection<WorkOrderModel>();
            Shifts = new ObservableCollection<string> { "전체", "주간1", "주간2", "야간1", "야간2" };
            ProductionLines = new ObservableCollection<string> { "전체", "라인1", "라인2", "라인3" };

            // 상태 명령 초기화
            StartWorkCommand = new RelayCommand(ExecuteStartWork, CanExecuteStartWork);
            CompleteWorkCommand = new RelayCommand(ExecuteCompleteWork, CanExecuteCompleteWork);
            CancelWorkCommand = new RelayCommand(ExecuteCancel, CanExecuteCancel);
            RestartWorkCommand = new RelayCommand(ExecuteRestartWork, CanExecuteRestartWork);

            // 명령 초기화
            SearchCommand = new RelayCommand(async () => await ExecuteSearch());
            AddCommand = new RelayCommand(ExecuteAdd);
            MoveUpCommand = new RelayCommand(ExecuteMoveUp, CanExecuteMove);
            MoveDownCommand = new RelayCommand(ExecuteMoveDown, CanExecuteMove);
            SaveSequenceCommand = new RelayCommand(ExecuteSaveSequence, CanExecuteSaveSequence);

            // 시뮬레이션 명령 초기화
            StartSimulationCommand = new RelayCommand(ExecuteStartSimulation, CanExecuteStartSimulation);
            PauseSimulationCommand = new RelayCommand(ExecutePauseSimulation, CanExecutePauseSimulation);
            ResumeSimulationCommand = new RelayCommand(ExecuteResumeSimulation, CanExecuteResumeSimulation);
            StopSimulationCommand = new RelayCommand(ExecuteStopSimulation, CanExecuteStopSimulation);

            // 초기 데이터 로드
            LoadInitialData();
            SelectedShift = "전체";
            SelectedLine = "전체";
        }
        #endregion


        #region Properties
        // 필터 관련 속성
        public DateTime WorkDate
        {
            get => _workDate;
            set
            {
                if (_workDate != value)
                {
                    _workDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedShift
        {
            get => _selectedShift;
            set
            {
                if (_selectedShift != value)
                {
                    _selectedShift = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedLine
        {
            get => _selectedLine;
            set
            {
                if (_selectedLine != value)
                {
                    _selectedLine = value;
                    OnPropertyChanged();
                }
            }
        }

        public WorkOrderModel SelectedWorkOrder
        {
            get => _selectedWorkOrder;
            set
            {
                if (_selectedWorkOrder != value)
                {
                    _selectedWorkOrder = value;
                    OnPropertyChanged();
                    // 선택된 작업지시 변경 시 이동 버튼 상태 갱신
                    (MoveUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (MoveDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveSequenceCommand as RelayCommand)?.RaiseCanExecuteChanged();

                    // 버튼 상태 갱신
                    (StartWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CompleteWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CancelWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RestartWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();

                    // 시뮬레이션 버튼 상태도 갱신
                    (StartSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PauseSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ResumeSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StopSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<WorkOrderModel> WorkOrders
        {
            get => _workOrders;
            set
            {
                _workOrders = value;
                OnPropertyChanged();
            }
        }

        public bool IsAllWorkOrdersChecked
        {
            get => _isallWorkOrdersChecked;
            set
            {
                if (_isallWorkOrdersChecked != value)
                {
                    _isallWorkOrdersChecked = value;
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
        public bool CanStartWork
        {
            get => _canStartWork;
            set
            {
                if (_canStartWork != value)
                {
                    _canStartWork = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanCompleteWork
        {
            get => _canCompleteWork;
            set
            {
                if (_canCompleteWork != value)
                {
                    _canCompleteWork = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool CanCancelWork
        {
            get => _canCancelWork;
            set
            {
                if (_canCancelWork != value)
                {
                    _canCancelWork = value;
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
                    UpdateCommandStates();
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

        #endregion


        #region Collections
        public ObservableCollection<string> Shifts { get; }
        public ObservableCollection<string> ProductionLines { get; }
        #endregion


        #region Commands
        public ICommand StartWorkCommand
        {
            get { return _startWorkCommand; }
            set
            {
                if (_startWorkCommand != value)
                {
                    _startWorkCommand = value;
                    OnPropertyChanged();
                }
            }
        }
        public ICommand CompleteWorkCommand
        {
            get { return _completeWorkCommand; }
            set
            {
                if (_completeWorkCommand != value)
                {
                    _completeWorkCommand = value;
                    OnPropertyChanged();
                }
            }
        }
        public ICommand CancelWorkCommand
        {
            get { return _cancelWorkCommand; }
            set
            {
                if (_cancelWorkCommand != value)
                {
                    _cancelWorkCommand = value;
                    OnPropertyChanged();
                }
            }
        }
        public ICommand RestartWorkCommand
        {
            get => _restartWorkCommand;
            set
            {
                if (_restartWorkCommand != value)
                {
                    _restartWorkCommand = value;
                    OnPropertyChanged();
                }
            }
        }
        
        // 순서관련
        public ICommand SearchCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveSequenceCommand { get; }

        // 시뮬레이션 관련
        public ICommand StartSimulationCommand { get; }
        public ICommand PauseSimulationCommand { get; }
        public ICommand ResumeSimulationCommand { get; }
        public ICommand StopSimulationCommand { get; }
        #endregion


        #region Methods
        // 데이터 로드
        private async void LoadInitialData()
        {
            try
            {
                await ExecuteSearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshWorkOrders()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"SELECT 
                pp.work_order_code as WorkOrderNumber,
                pp.production_date as ProductionDate,
                p.product_code as ProductCode,
                p.product_name as ProductName,
                pp.order_quantity as OrderQuantity,
                pp.production_quantity as ProductionQuantity,
                pp.work_order_sequence as Sequence,
                pp.work_shift as Shift,
                pp.process_status as Status,
                pp.production_line as ProductionLine
            FROM dt_production_plan pp
            JOIN dt_product p ON pp.product_code = p.product_code
            WHERE 1=1";

                    var parameters = new DynamicParameters();

                    // 전체 기간 보기가 체크되지 않은 경우, 오늘 날짜만 표시
                    if (!IsAllWorkOrdersChecked)
                    {
                        sql += " AND DATE(pp.production_date) = CURRENT_DATE()";
                    }

                    // 생산라인 필터
                    if (!string.IsNullOrEmpty(SelectedLine) && SelectedLine != "전체")
                    {
                        sql += " AND pp.production_line = @ProductionLine";
                        parameters.Add("@ProductionLine", SelectedLine);
                    }

                    // 근무조 필터
                    if (!string.IsNullOrEmpty(SelectedShift) && SelectedShift != "전체")
                    {
                        sql += " AND pp.work_shift = @Shift";
                        parameters.Add("@Shift", SelectedShift);
                    }

                    // 완료 항목 필터
                    if (IsCompletedOnlyChecked)
                    {
                        sql += " AND pp.process_status = '완료'";
                    }

                    // 정렬 조건
                    sql += " ORDER BY pp.production_date DESC, pp.work_order_sequence ASC";

                    var result = await conn.QueryAsync<WorkOrderModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        WorkOrders.Clear();
                        foreach (var order in result)
                        {
                            WorkOrders.Add(order);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSearch()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
                            SELECT 
                                pp.work_order_code AS WorkOrderNumber,
                                pp.production_date AS ProductionDate,
                                p.product_code AS ProductCode,
                                p.product_name AS ProductName,
                                pp.order_quantity AS OrderQuantity,
                                pp.production_quantity AS ProductionQuantity,
                                pp.work_shift AS Shift,
                                pp.process_status AS Status,
                                pp.production_line AS ProductionLine,
                                pp.simulation_mode AS SimulationMode,
                                pp.start_time AS StartTime,
                                pp.completion_time AS CompletionTime,
                                pp.work_order_sequence AS Sequence,
                                pp.remarks AS Remarks
                            FROM dt_production_plan pp
                            JOIN dt_product p ON pp.product_code = p.product_code
                            WHERE 1=1";

                    var parameters = new DynamicParameters();

                    // 전체 기간 보기가 체크되지 않은 경우, 선택한 날짜만 표시
                    if (!IsAllWorkOrdersChecked)
                    {
                        sql += " AND DATE(pp.production_date) = @WorkDate";
                        parameters.Add("@WorkDate", WorkDate.Date);
                    }

                    // 생산라인 필터
                    if (!string.IsNullOrEmpty(SelectedLine) && SelectedLine != "전체")
                    {
                        sql += " AND pp.production_line = @ProductionLine";
                        parameters.Add("@ProductionLine", SelectedLine);
                    }

                    // 근무조 필터
                    if (!string.IsNullOrEmpty(SelectedShift) && SelectedShift != "전체")
                    {
                        sql += " AND pp.work_shift = @Shift";
                        parameters.Add("@Shift", SelectedShift);
                    }

                    // 완료 항목 필터
                    if (IsCompletedOnlyChecked)
                    {
                        sql += " AND pp.process_status = '완료'";
                    }

                    // 정렬 조건
                    sql += " ORDER BY pp.production_date DESC, pp.work_order_sequence ASC";

                    var result = await conn.QueryAsync<WorkOrderModel>(sql, parameters);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        WorkOrders.Clear();
                        foreach (var order in result)
                        {
                            WorkOrders.Add(order);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 작업 상태 관리
        private async void ExecuteStartWork()
        {
            try
            {
                // 체크된 항목과 선택된 항목 모두 처리
                var itemsToProcess = WorkOrders.Where(x =>
                    (x.IsSelected || x == SelectedWorkOrder) && x.Status == "대기"
                ).Distinct().ToList();

                if (!itemsToProcess.Any())
                {
                    MessageBox.Show("시작할 수 있는 작업이 없습니다.", "알림");
                    return;
                }
                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var workOrder in itemsToProcess)
                    {
                        var sql = @"
                UPDATE dt_production_plan 
                SET process_status = '작업중',
                    start_time = @StartTime
                WHERE work_order_code = @WorkOrderCode";

                        await conn.ExecuteAsync(sql, new
                        {
                            StartTime = DateTime.Now,

                            WorkOrderCode = workOrder.WorkOrderNumber
                        });

                        // 로그 기록
                        string actionDetail = $"작업번호: {workOrder.WorkOrderNumber}, " +
                                           $"제품: {workOrder.ProductName}, " +
                                           $"수량: {workOrder.OrderQuantity}";

                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "작업시작", actionDetail);

                        workOrder.StartTime = DateTime.Now;
                        workOrder.Status = "작업중";
                    }
                }
                await ExecuteSearch(); // 목록 새로고침
                UpdateButtonStates();
                MessageBox.Show("작업이 시작되었습니다.", "알림");

            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업 시작 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private async void ExecuteCompleteWork()
        {
            try
            {
                // 체크된 항목과 선택된 항목 모두 처리
                var itemsToProcess = WorkOrders.Where(x =>
                    (x.IsSelected || x == SelectedWorkOrder) && x.Status == "작업중"
                ).Distinct().ToList();

                if (!itemsToProcess.Any())
                {
                    MessageBox.Show("완료할 수 있는 작업이 없습니다.", "알림");
                    return;
                }

                var result = MessageBox.Show(
                    $"선택한 작업을 완료하시겠습니까?\n대상 작업 수: {itemsToProcess.Count}",
                    "작업 완료 확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var workOrder in itemsToProcess)
                            {
                                // 1. 작업지시 상태 업데이트
                                var sql = @"
                            UPDATE dt_production_plan 
                            SET process_status = '완료',
                                completion_time = @CompletionTime,
                                production_quantity = @ProductionQuantity
                            WHERE work_order_code = @WorkOrderCode";

                                await conn.ExecuteAsync(sql, new
                                {
                                    CompletionTime = DateTime.Now,
                                    ProductionQuantity = workOrder.OrderQuantity,
                                    WorkOrderCode = workOrder.WorkOrderNumber
                                }, transaction);

                                // 2. 자동모드가 아닐 때만 재고 증가 처리
                                if (workOrder.SimulationMode != "자동")
                                {
                                    // 재고 기록 확인
                                    sql = @"
                                SELECT COUNT(*)
                                FROM dt_inventory_management 
                                WHERE remarks LIKE @Remarks";

                                    var exists = await conn.ExecuteScalarAsync<int>(sql, new
                                    {
                                        Remarks = $"%작업지시: {workOrder.WorkOrderNumber}%"
                                    }, transaction);

                                    if (exists == 0)
                                    {
                                        await _inventoryService.UpdateStock(
                                            workOrder.ProductCode,
                                            workOrder.OrderQuantity,
                                            "입고",
                                            $"생산완료 입고 (작업지시: {workOrder.WorkOrderNumber}) - 수동"
                                        );
                                    }
                                }

                                // 3. 수주 정보 확인
                                sql = @"
                            SELECT 
                                pp.order_number,
                                pp.product_code,
                                c.company_code,
                                c.company_name,
                                c.delivery_date,
                                c.status as contract_status
                            FROM dt_production_plan pp
                            LEFT JOIN dt_contract c ON pp.order_number = c.order_number
                            WHERE pp.work_order_code = @WorkOrderCode";

                                var orderInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(sql,
                                    new { WorkOrderCode = workOrder.WorkOrderNumber }, transaction);

                                // 4. 출하 등록 (수주가 있고 확정 상태인 경우)
                                if (orderInfo != null && orderInfo.order_number != null &&
                                    orderInfo.contract_status == "확정")
                                {
                                    sql = @"
                                SELECT COUNT(*) 
                                FROM dt_shipment 
                                WHERE order_number = @OrderNumber";

                                    var exists = await conn.ExecuteScalarAsync<int>(sql,
                                        new { OrderNumber = orderInfo.order_number }, transaction);

                                    if (exists == 0)
                                    {
                                        var nextSeq = await conn.QuerySingleAsync<int>(
                                            @"SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(shipment_number, '-', -1) AS UNSIGNED)), 0) + 1
                                    FROM dt_shipment 
                                    WHERE DATE(shipment_date) = @Today",
                                            new { Today = DateTime.Today }, transaction);

                                        var shipmentNumber = $"SH-{DateTime.Today:yyyyMMdd}-{nextSeq:D3}";

                                        sql = @"
                                    INSERT INTO dt_shipment (
                                        shipment_number, order_number, company_code, company_name,
                                        product_code, production_date, shipment_date,
                                        shipment_quantity, vehicle_number, employee_name, status
                                    ) VALUES (
                                        @ShipmentNumber, @OrderNumber, @CompanyCode, @CompanyName,
                                        @ProductCode, @ProductionDate, @ShipmentDate,
                                        @Quantity, @VehicleNumber, @EmployeeName, '대기'
                                    )";

                                        await conn.ExecuteAsync(sql, new
                                        {
                                            ShipmentNumber = shipmentNumber,
                                            OrderNumber = orderInfo.order_number,
                                            CompanyCode = orderInfo.company_code,
                                            CompanyName = orderInfo.company_name,
                                            ProductCode = orderInfo.product_code,
                                            ProductionDate = DateTime.Now,
                                            ShipmentDate = orderInfo.delivery_date,
                                            Quantity = workOrder.OrderQuantity,
                                            VehicleNumber = "미정",
                                            EmployeeName = App.CurrentUser.UserName
                                        }, transaction);

                                        await _logService.SaveLogAsync(
                                            App.CurrentUser.UserId,
                                            "생산완료_출하등록",
                                            $"작업지시번호: {workOrder.WorkOrderNumber}, " +
                                            $"수주번호: {orderInfo.order_number}, " +
                                            $"출하번호: {shipmentNumber}");
                                    }
                                }

                                await _logService.SaveLogAsync(
                                    App.CurrentUser.UserId,
                                    "작업완료",
                                    $"작업번호: {workOrder.WorkOrderNumber}, " +
                                    $"제품: {workOrder.ProductName}, " +
                                    $"수량: {workOrder.OrderQuantity}");
                            }

                            transaction.Commit();
                            await ExecuteSearch();
                            MessageBox.Show("작업이 완료되었습니다.", "알림");
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception($"작업 완료 처리 중 오류 발생: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업 완료 처리 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async void ExecuteCancel()
        {
            try
            {
                // 체크된 항목과 선택된 항목 모두 처리
                var itemsToProcess = WorkOrders.Where(x =>
                    (x.IsSelected || x == SelectedWorkOrder) &&
                    (x.Status == "작업중" || x.Status == "대기")
                ).Distinct().ToList();

                if (!itemsToProcess.Any())
                {
                    MessageBox.Show("취소할 수 있는 작업이 없습니다.", "알림");
                    return;
                }

                if (MessageBox.Show(
                    $"선택한 작업을 취소하시겠습니까?\n대상 작업 수: {itemsToProcess.Count}",
                    "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;

                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var workOrder in itemsToProcess)
                    {
                        var sql = @"
                UPDATE dt_production_plan 
                SET process_status = '지연',
                    employee_name = @EmployeeName
                WHERE work_order_code = @WorkOrderCode";

                        await conn.ExecuteAsync(sql, new
                        {
                            EmployeeName = App.CurrentUser.UserName,
                            WorkOrderCode = workOrder.WorkOrderNumber
                        });
                        // 로그 기록
                        string actionDetail = $"작업번호: {workOrder.WorkOrderNumber}, " +
                                           $"제품: {workOrder.ProductName}, " +
                                           $"수량: {workOrder.OrderQuantity}";

                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "작업지연", actionDetail);

                    }
                    await ExecuteSearch();
                    UpdateButtonStates();
                    MessageBox.Show("작업이 취소되었습니다.", "알림");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업 취소 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private async void ExecuteRestartWork()
        {
            try
            {
                // 체크된 항목과 선택된 항목 중 '지연' 상태인 것들 처리
                var itemsToProcess = WorkOrders.Where(x =>
                    (x.IsSelected || x == SelectedWorkOrder) && x.Status == "지연"
                ).Distinct().ToList();

                if (!itemsToProcess.Any())
                {
                    MessageBox.Show("재시작할 수 있는 작업이 없습니다.", "알림");
                    return;
                }

                using (var conn = new MySqlConnection(_connectionString))
                {
                    foreach (var workOrder in itemsToProcess)
                    {
                        var sql = @"
                   UPDATE dt_production_plan 
                   SET process_status = '작업중',
                       start_time = @StartTime
                   WHERE work_order_code = @WorkOrderCode";

                        await conn.ExecuteAsync(sql, new
                        {
                            StartTime = DateTime.Now,
                            WorkOrderCode = workOrder.WorkOrderNumber
                        });

                        // 로그 기록
                        string actionDetail = $"작업번호: {workOrder.WorkOrderNumber}, " +
                                           $"제품: {workOrder.ProductName}, " +
                                           $"수량: {workOrder.OrderQuantity}";

                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "작업재시작", actionDetail);


                        workOrder.StartTime = DateTime.Now;
                    }
                }

                await ExecuteSearch(); // 목록 새로고침
                UpdateButtonStates();
                MessageBox.Show("선택한 작업이 재시작되었습니다.", "알림");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"작업 재시작 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        // 순서 관리
        private void ExecuteMoveUp()
        {
            if (SelectedWorkOrder == null) return;

            var index = WorkOrders.IndexOf(SelectedWorkOrder);
            if (index > 0)
            {
                WorkOrders.Move(index, index - 1);
                UpdateSequenceNumbers();
            }
        }

        private void ExecuteMoveDown()
        {
            if (SelectedWorkOrder == null) return;

            var index = WorkOrders.IndexOf(SelectedWorkOrder);
            if (index < WorkOrders.Count - 1)
            {
                WorkOrders.Move(index, index + 1);
                UpdateSequenceNumbers();
            }
        }

        private async void ExecuteSaveSequence()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 기존 순서와 비교하여 변경된 것만 찾기
                            var sql = @"
                       SELECT work_order_code, work_order_sequence 
                       FROM dt_production_plan 
                       WHERE work_order_code IN @WorkOrderCodes";

                            var workOrderCodes = WorkOrders.Select(w => w.WorkOrderNumber).ToList();
                            var currentSequences = await conn.QueryAsync<(string WorkOrderCode, int Sequence)>(
                                sql,
                                new { WorkOrderCodes = workOrderCodes },
                                transaction
                            );

                            // 변경된 항목만 필터링하고 이전 순서 정보도 저장
                            var changedOrders = WorkOrders
                                .Select(order =>
                                {
                                    var currentSequence = currentSequences
                                        .FirstOrDefault(cs => cs.WorkOrderCode == order.WorkOrderNumber).Sequence;
                                    return new
                                    {
                                        Order = order,
                                        OldSequence = currentSequence,
                                        HasChanged = currentSequence != order.Sequence
                                    };
                                })
                                .Where(x => x.HasChanged)
                                .ToList();

                            // 변경된 항목만 업데이트 및 로그 기록
                            foreach (var change in changedOrders)
                            {
                                sql = @"
                           UPDATE dt_production_plan 
                           SET work_order_sequence = @Sequence 
                           WHERE work_order_code = @WorkOrderNumber";

                                await conn.ExecuteAsync(sql, new
                                {
                                    Sequence = change.Order.Sequence,
                                    WorkOrderNumber = change.Order.WorkOrderNumber
                                }, transaction);

                                // 로그 기록
                                string actionDetail = $"작업번호: {change.Order.WorkOrderNumber}, " +
                                                   $"제품: {change.Order.ProductName}, " +
                                                   $"순서변경: {change.OldSequence} -> {change.Order.Sequence}";

                                await _logService.SaveLogAsync(App.CurrentUser.UserId, "작업순서변경", actionDetail);
                            }

                            transaction.Commit();

                            if (changedOrders.Any())
                            {
                                var message = "작업 순서가 저장되었습니다.\n\n변경된 항목:\n";
                                message += string.Join("\n", changedOrders.Select(change =>
                                    $"• {change.Order.ProductName}: {change.OldSequence} -> {change.Order.Sequence}"));

                                MessageBox.Show(message, "알림",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
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
                MessageBox.Show($"작업 순서 저장 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSequenceNumbers()
        {
            for (int i = 0; i < WorkOrders.Count; i++)
            {
                WorkOrders[i].Sequence = i + 1;
            }
        }

        // 상태 확인
        private bool CanExecuteStartWork()
        {
            // 자동 모드이거나 선택된 항목이 없으면 비활성화
            if (SelectedWorkOrder?.SimulationMode == "자동")
                return false;
            // 선택된 항목이나 체크된 항목 중 '대기' 상태인 것이 있는지 확인
            return (SelectedWorkOrder?.Status == "대기") ||
                   WorkOrders.Any(x => x.IsSelected && x.Status == "대기");
        }

        private bool CanExecuteCompleteWork()
        {
            // 자동 모드이거나 선택된 항목이 없으면 비활성화
            if (SelectedWorkOrder?.SimulationMode == "자동")
                return false;
            // 선택된 항목이나 체크된 항목 중 '작업중' 상태인 것이 있는지 확인
            return (SelectedWorkOrder?.Status == "작업중") ||
                   WorkOrders.Any(x => x.IsSelected && x.Status == "작업중");
        }

        private bool CanExecuteCancel()
        {
            // 자동 모드이거나 선택된 항목이 없으면 비활성화
            if (SelectedWorkOrder?.SimulationMode == "자동")
                return false;
            // 선택된 항목이나 체크된 항목 중 '대기' 또는 '작업중' 상태인 것이 있는지 확인
            return (SelectedWorkOrder != null && (SelectedWorkOrder.Status == "작업중" || SelectedWorkOrder.Status == "대기")) ||
                   WorkOrders.Any(x => x.IsSelected && (x.Status == "작업중" || x.Status == "대기"));
        }

        private bool CanExecuteRestartWork()
        {
            // 자동 모드이거나 선택된 항목이 없으면 비활성화
            if (SelectedWorkOrder?.SimulationMode == "자동")
                return false;
            // 선택된 항목이나 체크된 항목 중 '지연' 상태인 것이 있는지 확인
            return (SelectedWorkOrder?.Status == "지연") ||
                   WorkOrders.Any(x => x.IsSelected && x.Status == "지연");

        }

        private bool CanExecuteMove()
        {
            return SelectedWorkOrder != null;
        }

        private bool CanExecuteSaveSequence()
        {
            return WorkOrders.Count > 0;
        }

        // 시뮬레이션
        private async void ExecuteStartSimulation()
        {
            try
            {
                if (SelectedWorkOrder == null) return;

                var result = MessageBox.Show(
                    "시뮬레이션을 시작하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                await _simulationService.StartSimulation(SelectedWorkOrder.WorkOrderNumber);
                SimulationStatus = "시뮬레이션 실행 중...";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 시작",
                    $"작업지시번호: {SelectedWorkOrder.WorkOrderNumber}");
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
                if (SelectedWorkOrder == null) return;
                await _simulationService.PauseSimulation(SelectedWorkOrder.WorkOrderNumber);
                SimulationStatus = "시뮬레이션 일시정지";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 일시정지",
                    $"작업지시번호: {SelectedWorkOrder.WorkOrderNumber}");
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
                if (SelectedWorkOrder == null) return;
                await _simulationService.ResumeSimulation(SelectedWorkOrder.WorkOrderNumber);
                SimulationStatus = "시뮬레이션 재시작...";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 재시작",
                    $"작업지시번호: {SelectedWorkOrder.WorkOrderNumber}");
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
                if (SelectedWorkOrder == null) return;

                var result = MessageBox.Show(
                    "시뮬레이션을 중지하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                await _simulationService.StopSimulation(SelectedWorkOrder.WorkOrderNumber);
                SimulationStatus = "시뮬레이션 중지됨";

                await _logService.SaveLogAsync(
                    App.CurrentUser.UserId,
                    "시뮬레이션 중지",
                    $"작업지시번호: {SelectedWorkOrder.WorkOrderNumber}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"시뮬레이션 중지 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        private void OnProductionUpdated(object sender, ProductionEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    // 현재 선택된 작업지시 번호 저장
                    string selectedWorkOrderNumber = SelectedWorkOrder?.WorkOrderNumber;
                    var workOrder = WorkOrders.FirstOrDefault(w => w.WorkOrderNumber == e.WorkOrderCode);
                    if (workOrder != null)
                    {
                        workOrder.ProductionQuantity = e.CurrentQuantity;
                        // 생산량이 목표량에 도달했는지 확인
                        if (workOrder.ProductionQuantity >= workOrder.OrderQuantity)
                        {
                            workOrder.Status = "완료";
                            SimulationStatus = "생산 완료";
                            // 시뮬레이션 중지
                            await _simulationService.StopSimulation(e.WorkOrderCode);

                            using (var conn = new MySqlConnection(_connectionString))
                            {
                                await conn.OpenAsync();
                                using (var transaction = conn.BeginTransaction())
                                {
                                    try
                                    {
                                        // 1. 재고 증가를 위한 제품 코드 조회
                                        string sql = @"
                                    SELECT product_code 
                                    FROM dt_production_plan 
                                    WHERE work_order_code = @WorkOrderCode";
                                        var productCode = await conn.QuerySingleAsync<string>(sql,
                                            new { WorkOrderCode = e.WorkOrderCode }, transaction);

                                        // 재고 기록 확인
                                        sql = @"
                                    SELECT COUNT(*)
                                    FROM dt_inventory_management 
                                    WHERE remarks LIKE @Remarks";

                                        var inventoryExists = await conn.ExecuteScalarAsync<int>(sql, new
                                        {
                                            Remarks = $"%작업지시: {e.WorkOrderCode}%"
                                        }, transaction);

                                        if (inventoryExists == 0)
                                        {
                                            await _inventoryService.UpdateStock(
                                                productCode,
                                                e.CurrentQuantity,
                                                "입고",
                                                $"생산완료 입고 (작업지시: {e.WorkOrderCode}) - 자동"
                                            );
                                        }

                                        // 2. 작업지시 완료 처리
                                        sql = @"
                                    UPDATE dt_production_plan 
                                    SET process_status = '완료',
                                        production_quantity = @Quantity,
                                        completion_time = @CompletionTime
                                    WHERE work_order_code = @WorkOrderCode";

                                        await conn.ExecuteAsync(sql, new
                                        {
                                            WorkOrderCode = e.WorkOrderCode,
                                            Quantity = e.CurrentQuantity,
                                            CompletionTime = DateTime.Now
                                        }, transaction);

                                        // 3. 수주 정보 확인
                                        sql = @"
                                    SELECT 
                                        pp.order_number,
                                        pp.product_code,
                                        c.company_code,
                                        c.company_name,
                                        c.delivery_date,
                                        c.status as contract_status
                                    FROM dt_production_plan pp
                                    LEFT JOIN dt_contract c ON pp.order_number = c.order_number
                                    WHERE pp.work_order_code = @WorkOrderCode";

                                        var orderInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(sql,
                                            new { WorkOrderCode = e.WorkOrderCode }, transaction);

                                        // 4. 출하 등록 (수주가 있고 확정 상태인 경우)
                                        if (orderInfo != null && orderInfo.order_number != null &&
                                            orderInfo.contract_status == "확정")
                                        {
                                            // 기존 출하 여부 확인
                                            sql = @"SELECT COUNT(*) FROM dt_shipment WHERE order_number = @OrderNumber";
                                            var shipmentExists = await conn.ExecuteScalarAsync<int>(sql,
                                                new { OrderNumber = orderInfo.order_number }, transaction);

                                            if (shipmentExists == 0)
                                            {
                                                // 출하번호 생성
                                                sql = @"SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(shipment_number, '-', -1) AS UNSIGNED)), 0) + 1
                                            FROM dt_shipment WHERE DATE(shipment_date) = @Today";
                                                var nextSeq = await conn.QuerySingleAsync<int>(sql,
                                                    new { Today = DateTime.Today }, transaction);

                                                var shipmentNumber = $"SH-{DateTime.Today:yyyyMMdd}-{nextSeq:D3}";

                                                // 출하 등록
                                                sql = @"
                                            INSERT INTO dt_shipment (
                                                shipment_number, order_number, company_code, company_name,
                                                product_code, production_date, shipment_date,
                                                shipment_quantity, vehicle_number, employee_name, status
                                            ) VALUES (
                                                @ShipmentNumber, @OrderNumber, @CompanyCode, @CompanyName,
                                                @ProductCode, @ProductionDate, @ShipmentDate,
                                                @Quantity, @VehicleNumber, @EmployeeName, '대기'
                                            )";

                                                await conn.ExecuteAsync(sql, new
                                                {
                                                    ShipmentNumber = shipmentNumber,
                                                    OrderNumber = orderInfo.order_number,
                                                    CompanyCode = orderInfo.company_code,
                                                    CompanyName = orderInfo.company_name,
                                                    ProductCode = orderInfo.product_code,
                                                    ProductionDate = DateTime.Now,
                                                    ShipmentDate = orderInfo.delivery_date,
                                                    Quantity = e.CurrentQuantity,
                                                    VehicleNumber = "미정",
                                                    EmployeeName = App.CurrentUser.UserName
                                                }, transaction);

                                                await _logService.SaveLogAsync(
                                                    App.CurrentUser.UserId,
                                                    "생산완료_출하등록",
                                                    $"작업지시번호: {e.WorkOrderCode}, " +
                                                    $"수주번호: {orderInfo.order_number}, " +
                                                    $"출하번호: {shipmentNumber}");
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

                            await _logService.SaveLogAsync(
                                App.CurrentUser.UserId,
                                "생산 완료",
                                $"작업지시번호: {e.WorkOrderCode}, 최종생산량: {e.CurrentQuantity}");
                        }
                        else
                        {
                            OnPropertyChanged(nameof(WorkOrders));
                        }
                    }

                    await Task.Delay(100);
                    await ExecuteSearch();

                    if (selectedWorkOrderNumber != null)
                    {
                        SelectedWorkOrder = WorkOrders.FirstOrDefault(w => w.WorkOrderNumber == selectedWorkOrderNumber);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"생산 데이터 업데이트 중 오류가 발생했습니다: {ex.Message}", "오류");
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

        private void UpdateCommandStates()
        {
            (StartSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PauseSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResumeSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StopSimulationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // 시뮬레이션 상태
        private bool CanExecuteStartSimulation() =>
    SelectedWorkOrder?.Status == "대기"  && SelectedWorkOrder.IsAutoMode;

        private bool CanExecutePauseSimulation() =>
            SelectedWorkOrder?.Status == "작업중" && SelectedWorkOrder.IsAutoMode;

        private bool CanExecuteResumeSimulation() =>
             (SelectedWorkOrder?.Status == "일시정지"|| SelectedWorkOrder?.Status =="지연") && SelectedWorkOrder.IsAutoMode;

        private bool CanExecuteStopSimulation() =>
               (SelectedWorkOrder?.Status == "작업중" || SelectedWorkOrder?.Status == "일시정지")
               && SelectedWorkOrder.IsAutoMode;

        // 기타
        private void ExecuteAdd()
        {
            // TODO: 작업지시 등록 창 구현
            MessageBox.Show("작업지시 등록 기능은 추후 구현 예정입니다.");
        }

        private void UpdateButtonStates()
        {
            (StartWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CompleteWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RestartWorkCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Cleanup()
        {
            // 리소스 정리 필요시 여기에 구현
        }
        #endregion

    }
}