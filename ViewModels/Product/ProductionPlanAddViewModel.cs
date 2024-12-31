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
using MES.Solution.Models;
using MES.Solution.Services;
using System.Collections.Generic;


namespace MES.Solution.ViewModels
{
    public class ProductionPlanAddViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 상수
        private const int MAX_QUANTITY = 999999;
        private const int MAX_REMARKS_LENGTH = 200;

        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;
        private readonly bool _isEditMode;

        // 에러 처리 관련
        private HashSet<string> _errors = new HashSet<string>();
        private string _errormessage;
        private bool _haserror;

        // 데이터 필드
        private string _originalWorkOrderCode;
        private DateTime _productionDate = DateTime.Today;
        private string _selectedProductionLine;
        private ProductionPlanModel _selectedProduct;
        private int _orderQuantity;
        private string _selectedWorkShift;
        private string _remarks;
        private string _windowTitle;
        //private DateTime _minimumDate = DateTime.Today;
        private FormMode _mode;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RequestClose;
        #endregion


        #region Constructor
        public ProductionPlanAddViewModel(bool isEdit = false)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _logService = new LogService(); // LogService 초기화
            _isEditMode = isEdit;
            Mode = isEdit ? FormMode.Edit : FormMode.Add;

            // 컬렉션 초기화
            ProductionLines = new ObservableCollection<string> { "라인1", "라인2", "라인3" };
            WorkShifts = new ObservableCollection<string> { "주간1", "주간2", "야간1", "야간2" };
            Products = new ObservableCollection<ProductionPlanModel>();

            // 커맨드 초기화    
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);

            WindowTitle = isEdit ? "생산계획 수정":"생산계획 등록" ;

            // 데이터 초기화
            InitializeData();
            ValidateAll();
        }
        #endregion


        #region Properties
        // 기본 속성
        public FormMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    WindowTitle = value == FormMode.Add ? "생산계획 등록" : "생산계획 수정";
                    OnPropertyChanged();
                }
            }
        }
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                if (_windowTitle != value)
                {
                    _windowTitle = value;
                    OnPropertyChanged();
                }
            }
        }
        //public DateTime MinimumDate
        //{
        //    get => _minimumDate;
        //    set
        //    {
        //        if (_minimumDate != value)
        //        {
        //            _minimumDate = value;
        //            OnPropertyChanged();
        //            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        //        }
        //    }
        //}

        // 입력 데이터 속성
        public DateTime ProductionDate
        {
            get => _productionDate;
            set
            {
                if (_productionDate != value)
                {
                    _productionDate = value;
                    ValidateProductionDate();
                    OnPropertyChanged();
                }
            }
        }
        public string SelectedProductionLine
        {
            get => _selectedProductionLine;
            set
            {
                if (_selectedProductionLine != value)
                {
                    _selectedProductionLine = value;
                    ValidateProductionLine();
                    OnPropertyChanged();
                }
            }
        }
        public ProductionPlanModel SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (_selectedProduct != value)
                {
                    _selectedProduct = value;
                    ValidateProduct();
                    OnPropertyChanged();
                }
            }
        }
        public int OrderQuantity
        {
            get => _orderQuantity;
            set
            {
                if (value > MAX_QUANTITY)
                {
                    _orderQuantity = MAX_QUANTITY;
                    _errors.Add($"지시수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                else
                {
                    _orderQuantity = value;
                    _errors.Remove($"지시수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                ValidateOrderQuantity();
                OnPropertyChanged();
            }
        }
        public string SelectedWorkShift
        {
            get => _selectedWorkShift;
            set
            {
                if (_selectedWorkShift != value)
                {
                    _selectedWorkShift = value;
                    ValidateWorkShift();
                    OnPropertyChanged();
                }
            }
        }
        public string Remarks
        {
            get => _remarks;
            set
            {
                if (value?.Length > MAX_REMARKS_LENGTH)
                {
                    _remarks = value.Substring(0, MAX_REMARKS_LENGTH);
                    _errors.Add($"비고는 {MAX_REMARKS_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _remarks = value;
                    _errors.Remove($"비고는 {MAX_REMARKS_LENGTH}자를 초과할 수 없습니다.");
                }
                OnPropertyChanged();
                UpdateErrorMessage();
            }
        }

        // 에러 관련 속성
        public string ErrorMessage
        {
            get => _errormessage;
            set
            {
                _errormessage = value;
                OnPropertyChanged();
                HasError = !string.IsNullOrEmpty(value);
            }
        }
        public bool HasError
        {
            get => _haserror;
            set
            {
                _haserror = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Collections
        public ObservableCollection<string> ProductionLines { get; }
        public ObservableCollection<ProductionPlanModel> Products { get; }
        public ObservableCollection<string> WorkShifts { get; }
        #endregion


        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async void InitializeData()
        {
            try
            {
                // 생산라인 초기화
                ProductionLines.Clear();
                ProductionLines.Add("라인1");
                ProductionLines.Add("라인2");
                ProductionLines.Add("라인3");

                // 근무조 초기화
                WorkShifts.Clear();
                WorkShifts.Add("주간1");
                WorkShifts.Add("주간2");
                WorkShifts.Add("야간1");
                WorkShifts.Add("야간2");

                // 제품 목록 로드
                await LoadProducts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"초기 데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async Task LoadProducts()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT product_code as ProductCode, product_name as ProductName, unit as Unit FROM dt_product ORDER BY product_name";
                var products = await conn.QueryAsync<ProductionPlanModel>(sql);

                Products.Clear();
                foreach (var product in products)
                {
                    Products.Add(product);
                }
            }
        }
        public async void LoadData(ProductionPlanModel plan)
        {
            if (plan == null) return;

            try
            {
                _originalWorkOrderCode = plan.PlanNumber;  // 원본 작업지시 번호 저장
                ProductionDate = plan.PlanDate;
                SelectedProductionLine = plan.ProductionLine;

                // 제품 정보 로드
                await LoadProducts();  // 제품 목록을 먼저 로드

                // 제품 선택
                SelectedProduct = Products.FirstOrDefault(p => p.ProductCode == plan.ProductCode);

                OrderQuantity = plan.PlannedQuantity;
                SelectedWorkShift = plan.WorkShift;
                Remarks = plan.Remarks;

                // 유효성 검사 실행
                ValidateAll();

                // Mode 설정
                Mode = FormMode.Edit;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void LoadDataForEdit(string workOrderCode)
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
                        SELECT 
                            production_date as ProductionDate,
                            production_line as ProductionLine,
                            product_code as ProductCode,
                            order_quantity as OrderQuantity,
                            work_shift as WorkShift,
                            remarks as Remarks
                        FROM dt_production_plan 
                        WHERE work_order_code = @WorkOrderCode";

                    var plan = conn.QueryFirstOrDefault<dynamic>(sql, new { WorkOrderCode = workOrderCode });

                    if (plan != null)
                    {
                        _originalWorkOrderCode = workOrderCode;
                        ProductionDate = plan.ProductionDate;
                        SelectedProductionLine = plan.ProductionLine;
                        SelectedProduct = Products.FirstOrDefault(p => p.ProductCode == plan.ProductCode);
                        OrderQuantity = plan.OrderQuantity;
                        SelectedWorkShift = plan.WorkShift;
                        Remarks = plan.Remarks;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터 로드 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 유효성 검사 메서드
        private bool ValidateAll()
        {
            ValidateProductionDate();
            ValidateProduct();
            ValidateWorkShift();
            ValidateProductionLine();
            ValidateOrderQuantity();

            return !HasError;
        }
        private void ValidateProductionDate()
        {
            if (!_isEditMode)
            {
                if (ProductionDate < DateTime.Today)// 신규 등록의 경우에만 현재 날짜 이후로 제한
                {
                    _errors.Add("생산일자는 현재 날짜 이후여야 합니다.");
                }
                else
                {
                    _errors.Remove("생산일자는 현재 날짜 이후여야 합니다.");
                }
            }
            UpdateErrorMessage();
        }
        private void ValidateProductionLine()
        {
            if (SelectedProductionLine == null)
            {
                _errors.Add("생산라인을 선택해주세요.");
            }
            else
            {
                _errors.Remove("생산라인을 선택해주세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateProduct()
        {
            if (SelectedProduct == null)
            {
                _errors.Add("제품을 선택해주세요.");
            }
            else
            {
                _errors.Remove("제품을 선택해주세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateOrderQuantity()
        {
            if (OrderQuantity <= 0)
            {
                _errors.Add("지시수량은 0보다 커야 합니다.");
            }
            else
            {
                _errors.Remove("지시수량은 0보다 커야 합니다.");
            }
            UpdateErrorMessage();
        }
        private void ValidateWorkShift()
        {
            if (SelectedWorkShift == null)
            {
                _errors.Add("근무조를 선택해주세요.");
            }
            else
            {
                _errors.Remove("근무조를 선택해주세요.");
            }
            UpdateErrorMessage();
        }
        private void UpdateErrorMessage()
        {
            ErrorMessage = _errors.FirstOrDefault();
            HasError = _errors.Any();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // 실행 메서드
        private bool CanExecuteSave()
        {
            return !HasError;
        }
        private async void ExecuteSave()
        {
            try
            {
                if (!ValidateAll())
                {
                    return;
                }

                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    string sql;
                    var parameters = new DynamicParameters();
                    string workOrderCode = _originalWorkOrderCode; // 수정 모드일 때 사용할 기존 코드

                    if (Mode == FormMode.Edit)
                    {
                        sql = @"UPDATE dt_production_plan 
                    SET production_date = @ProductionDate,
                        production_line = @ProductionLine,
                        product_code = @ProductCode,
                        order_quantity = @OrderQuantity,
                        work_shift = @WorkShift,
                        remarks = @Remarks,
                        employee_name = @EmployeeName
                    WHERE work_order_code = @WorkOrderCode";

                        parameters.Add("@WorkOrderCode", _originalWorkOrderCode);
                    }
                    else
                    {
                        // 신규 등록 시 시퀀스 번호를 먼저 생성
                        var sequenceQuery = @"
                    SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(work_order_code, '-', -1) AS UNSIGNED)), 0) + 1
                    FROM dt_production_plan 
                    WHERE DATE(production_date) = @ProductionDate";

                        var nextSequence = await conn.QuerySingleAsync<int>(sequenceQuery, new { ProductionDate });
                        workOrderCode = $"PP-{ProductionDate:yyyyMMdd}-{nextSequence:D3}";

                        sql = @"INSERT INTO dt_production_plan (
                        work_order_code,
                        production_date,
                        production_line,
                        product_code,
                        order_quantity,
                        work_shift,
                        process_status,
                        work_order_sequence,
                        remarks,
                        employee_name
                    ) VALUES (
                        @WorkOrderCode,
                        @ProductionDate,
                        @ProductionLine,
                        @ProductCode,
                        @OrderQuantity,
                        @WorkShift,
                        @ProcessStatus,
                        @WorkOrderSequence,
                        @Remarks,
                        @EmployeeName
                    )";

                        parameters.Add("@WorkOrderCode", workOrderCode);
                        parameters.Add("@ProcessStatus", "대기");
                        parameters.Add("@WorkOrderSequence", nextSequence);
                    }

                    // 공통 파라미터 추가
                    parameters.Add("@ProductionDate", ProductionDate);
                    parameters.Add("@ProductionLine", SelectedProductionLine);
                    parameters.Add("@ProductCode", SelectedProduct.ProductCode);
                    parameters.Add("@OrderQuantity", OrderQuantity);
                    parameters.Add("@WorkShift", SelectedWorkShift);
                    parameters.Add("@Remarks", Remarks);
                    parameters.Add("@EmployeeName", App.CurrentUser.UserName);

                    // 로그에 필요한 정보 준비 (이제 workOrderCode 사용 가능)
                    string actionType = Mode == FormMode.Edit ? "생산계획 수정" : "생산계획 등록";
                    string actionDetail = $"작업지시번호: {workOrderCode}, " +
                                        $"생산일자: {ProductionDate:yyyy-MM-dd}, " +
                                        $"생산라인: {SelectedProductionLine}, " +
                                        $"제품: {SelectedProduct?.ProductName}, " +
                                        $"수량: {OrderQuantity}, " +
                                        $"근무조: {SelectedWorkShift}";

                    await conn.ExecuteAsync(sql, parameters);

                    // 로그 저장
                    await _logService.SaveLogAsync(App.CurrentUser.UserId, actionType, actionDetail);

                    MessageBox.Show(Mode == FormMode.Edit ? "수정되었습니다." : "등록되었습니다.",
                        "알림", MessageBoxButton.OK, MessageBoxImage.Information);

                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ExecuteCancel()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        // INotifyPropertyChanged 구현
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

    }
}