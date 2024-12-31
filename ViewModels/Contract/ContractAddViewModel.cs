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
using System.Collections.Generic;
using MES.Solution.Models;

namespace MES.Solution.ViewModels
{
    public class ContractAddViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 상수
        private const int MAX_COMPANY_NAME_LENGTH = 100;
        private const int MAX_QUANTITY = 999999;
        private const int MAX_REMARKS_LENGTH = 200;

        // 서비스 관련
        private readonly LogService _logService;
        private readonly string _connectionString;
        private readonly bool _isEditMode;
        private string _originalOrderNumber;

        // 에러 처리 관련
        private HashSet<string> _errors = new HashSet<string>();
        private string _errorMessage;
        private bool _hasError;

        // 데이터 관련
        private DateTime _orderDate = DateTime.Today;
        private string _selectedCompanyName;
        private ProductionPlanModel _selectedProduct;
        private DateTime _deliveryDate = DateTime.Today;
        private int _quantity;
        private string _remarks;
        private string _windowTitle;
        #endregion


        #region Events
        // INotifyPropertyChanged 구현
        public event PropertyChangedEventHandler PropertyChanged;
        // 창 닫기 이벤트
        public event EventHandler RequestClose;
        #endregion


        #region Constructor
        public ContractAddViewModel(bool isEdit = false)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _logService = new LogService();
            _isEditMode = isEdit;
            Mode = isEdit ? FormMode.Edit : FormMode.Add;

            // 콤보박스 데이터 초기화
            Products = new ObservableCollection<ProductionPlanModel>();

            // 커맨드 초기화    
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);

            WindowTitle = isEdit ? "수주 수정" : "수주 등록";

            // 데이터 초기화
            InitializeData();

            // 초기 유효성 검사 실행
            ValidateAll();
        }
        #endregion


        #region Properties
        // 상태 관련 속성
        public FormMode Mode { get; set; }

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

        // 계약 데이터 속성
        public DateTime OrderDate
        {
            get => _orderDate;
            set
            {
                if (_orderDate != value)
                {
                    _orderDate = value;
                    if (_orderDate > DeliveryDate)
                    {
                        DeliveryDate = _orderDate;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedCompanyName
        {
            get => _selectedCompanyName;
            set
            {
                if (value?.Length > MAX_COMPANY_NAME_LENGTH)
                {
                    _selectedCompanyName = value.Substring(0, MAX_COMPANY_NAME_LENGTH);
                    _errors.Add($"거래처명은 {MAX_COMPANY_NAME_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _selectedCompanyName = value;
                    _errors.Remove($"거래처명은 {MAX_COMPANY_NAME_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateCompany();
                OnPropertyChanged();
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

        public DateTime DeliveryDate
        {
            get => _deliveryDate;
            set
            {
                if (_deliveryDate != value)
                {
                    _deliveryDate = value;
                    if (_deliveryDate < OrderDate)
                    {
                        OrderDate = _deliveryDate;
                    }
                    OnPropertyChanged();
                }
            }
        }

        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value > MAX_QUANTITY)
                {
                    _quantity = MAX_QUANTITY;
                    _errors.Add($"수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                else
                {
                    _quantity = value;
                    _errors.Remove($"수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                ValidateQuantity();
                OnPropertyChanged();
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
                UpdateErrorMessage();
                OnPropertyChanged();
            }
        }

        // 에러 관련 속성
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
                HasError = !string.IsNullOrEmpty(value);
            }
        }

        public bool HasError
        {
            get => _hasError;
            set
            {
                _hasError = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Collections
        // UI 바인딩용 컬렉션
        public ObservableCollection<ProductionPlanModel> Products { get; }
        #endregion


        #region Commands
        // UI 액션 커맨드
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        private async void InitializeData()
        {
            try
            {
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

        public void LoadData(ContractModel contract)
        {
            if (contract == null) return;

            try
            {
                _originalOrderNumber = contract.OrderNumber;
                OrderDate = contract.OrderDate;
                SelectedCompanyName = contract.CompanyName;
                SelectedProduct = Products.FirstOrDefault(p => p.ProductCode == contract.ProductCode);
                DeliveryDate = contract.DeliveryDate;
                Quantity = contract.Quantity;
                Remarks = contract.Remarks;

                ValidateAll();
                Mode = FormMode.Edit;
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
            ValidateCompany();
            ValidateProduct();
            ValidateQuantity();

            return !HasError ;
        }

        private void ValidateCompany()
        {
            if (string.IsNullOrEmpty(SelectedCompanyName))
            {
                _errors.Add("거래처명을 입력해주세요.");
            }
            else
            {
                _errors.Remove("거래처명을 입력해주세요.");
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

        private void ValidateQuantity()
        {
            if (Quantity <= 0)
            {
                _errors.Add("수량은 0보다 커야 합니다.");
            }
            else
            {
                _errors.Remove("수량은 0보다 커야 합니다.");
            }
            UpdateErrorMessage();
        }

        private void UpdateErrorMessage()
        {
            ErrorMessage = _errors.FirstOrDefault();
            HasError = _errors.Any();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanExecuteSave()
        {
            return ValidateAll();
        }

        // 실행 메서드
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
                    string orderNumber = _originalOrderNumber;

                    if (Mode == FormMode.Edit)
                    {
                        sql = @"UPDATE dt_contract 
                           SET order_date = @OrderDate,
                               company_code = @CompanyCode,
                               company_name = @CompanyName,
                               product_code = @ProductCode,
                               quantity = @Quantity,
                               delivery_date = @DeliveryDate,
                               remarks = @Remarks,
                               employee_name = @EmployeeName,
                               status = @Status
                           WHERE order_number = @OrderNumber";

                        parameters.Add("@OrderNumber", _originalOrderNumber);
                    }
                    else
                    {
                        var sequenceQuery = @"
                        SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(order_number, '-', -1) AS UNSIGNED)), 0) + 1
                        FROM dt_contract 
                        WHERE DATE(order_date) = @OrderDate";

                        var nextSequence = await conn.QuerySingleAsync<int>(sequenceQuery, new { OrderDate });
                        orderNumber = $"CT-{OrderDate:yyyyMMdd}-{nextSequence:D3}";

                        sql = @"INSERT INTO dt_contract (
                            order_number, order_date, company_code, company_name,
                            product_code, quantity, delivery_date,
                            remarks, employee_name, status
                        ) VALUES (
                            @OrderNumber, @OrderDate, @CompanyCode, @CompanyName,
                            @ProductCode, @Quantity, @DeliveryDate,
                            @Remarks, @EmployeeName, @Status
                        )";

                        parameters.Add("@OrderNumber", orderNumber);
                    }

                    string companyCode = string.IsNullOrEmpty(SelectedCompanyName) ? "" :
       (SelectedCompanyName.Length >= 4
           ? SelectedCompanyName.Substring(0, 2) + SelectedCompanyName.Substring(SelectedCompanyName.Length - 2, 2)
           : SelectedCompanyName.Substring(0, SelectedCompanyName.Length));  // 길이가 짧을 경우 첫 2글자만 사용



                    parameters.Add("@OrderDate", OrderDate);
                    parameters.Add("@CompanyCode", companyCode);
                    parameters.Add("@CompanyName", SelectedCompanyName);
                    parameters.Add("@ProductCode", SelectedProduct.ProductCode);
                    parameters.Add("@Quantity", Quantity);
                    parameters.Add("@DeliveryDate", DeliveryDate);
                    parameters.Add("@Remarks", Remarks);
                    parameters.Add("@EmployeeName", App.CurrentUser.UserName);
                    parameters.Add("@Status", "대기");

                    await conn.ExecuteAsync(sql, parameters);

                    // 로그 저장
                    string actionType = Mode == FormMode.Edit ? "수주내역 수정" : "수주내역 등록";
                    string actionDetail = $"수주번호: {orderNumber}, " +
                                       $"주문일자: {OrderDate:yyyy-MM-dd}, " +
                                       $"거래처: {SelectedCompanyName}, " +
                                       $"제품: {SelectedProduct?.ProductName}, " +
                                       $"수량: {Quantity}, " +
                                       $"납품일자: {DeliveryDate:yyyy-MM-dd}";

                    await _logService.SaveLogAsync(App.CurrentUser.UserId, actionType, actionDetail);

                    MessageBox.Show(Mode == FormMode.Edit ? "수정되었습니다." : "등록되었습니다.",
                        "알림", MessageBoxButton.OK, MessageBoxImage.Information);

                    RequestClose?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"저장 중 오류가 발생했습니다: {ex.Message}",
                    "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        // 속성 변경 알림
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}