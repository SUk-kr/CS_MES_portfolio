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
using MES.Solution.Models;
using System.Collections.Generic;

namespace MES.Solution.ViewModels
{
    public class ShipmentAddViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 상수
        private const int MAX_COMPANY_NAME_LENGTH = 100;
        private const int MAX_VEHICLE_NUMBER_LENGTH = 20;
        private const int MAX_QUANTITY = 999999;

        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;

        // 상태 관련
        private readonly bool _isEditMode;
        private string _originalShipmentNumber;
        //private FormMode _mode;
        private string _windowTitle;

        // 에러 처리 관련
        private HashSet<string> _errors = new HashSet<string>();
        private string _errormessage;
        private bool _haserror;

        // 데이터 필드
        private DateTime _shipmentDate = DateTime.Today;
        private string _selectedCompanyCode;
        private string _selectedCompanyName;
        private ProductionPlanModel _selectedProduct;
        private DateTime _productionDate = DateTime.Today;
        private int _shipmentQuantity;
        private string _vehicleNumber;

        // 유효성 검증 필드
        //private bool _shipmentDateValid = true;
        //private bool _companyValid = true;
        //private bool _productValid = true;
        //private bool _productionDateValid = true;
        //private bool _quantityValid = true;
        //private bool _vehicleNumberValid = true;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RequestClose;
        #endregion


        #region Constructor
        public ShipmentAddViewModel(bool isEdit = false)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _logService = new LogService(); // LogService 초기화
            _isEditMode = isEdit;
            Mode = isEdit ? FormMode.Edit : FormMode.Add;

            // 컬렉션 초기화
            Products = new ObservableCollection<ProductionPlanModel>();
            Companies = new ObservableCollection<ShipmentModel>();

            // 커맨드 초기화    
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);

            WindowTitle = isEdit ? "출하 수정" : "출하 등록";

            // 데이터 초기화
            InitializeData();
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

        // 데이터 속성
        public DateTime ShipmentDate
        {
            get => _shipmentDate;
            set
            {
                if (_shipmentDate != value)
                {
                    _shipmentDate = value;
                    ValidateShipmentDate();
                    OnPropertyChanged();
                }
            }
        }
        public string SelectedCompanyCode
        {
            get => _selectedCompanyCode;
            set
            {
                if (_selectedCompanyCode != value)
                {
                    _selectedCompanyCode = value;
                    ValidateCompany();
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
        public int ShipmentQuantity
        {
            get => _shipmentQuantity;
            set
            {
                if (value > MAX_QUANTITY)
                {
                    _shipmentQuantity = MAX_QUANTITY;
                    _errors.Add($"출하수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                else
                {
                    _shipmentQuantity = value;
                    _errors.Remove($"출하수량은 {MAX_QUANTITY:N0}을 초과할 수 없습니다.");
                }
                ValidateQuantity();
                OnPropertyChanged();
            }
        }
        public string VehicleNumber
        {
            get => _vehicleNumber;
            set
            {
                if (value?.Length > MAX_VEHICLE_NUMBER_LENGTH)
                {
                    _vehicleNumber = value.Substring(0, MAX_VEHICLE_NUMBER_LENGTH);
                    _errors.Add($"차량번호는 {MAX_VEHICLE_NUMBER_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _vehicleNumber = value;
                    _errors.Remove($"차량번호는 {MAX_VEHICLE_NUMBER_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateVehicleNumber();
                OnPropertyChanged();
            }
        }
        #endregion


        #region Collections
        public ObservableCollection<ProductionPlanModel> Products { get; }
        public ObservableCollection<ShipmentModel> Companies { get; }
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
                await LoadProducts();
                await LoadCompanies();
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
        private async Task LoadCompanies()
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                var sql = "SELECT DISTINCT company_code, company_name FROM dt_shipment ORDER BY company_name";
                var companies = await conn.QueryAsync<ShipmentModel>(sql);

                Companies.Clear();
                foreach (var company in companies)
                {
                    Companies.Add(company);
                }
            }
        }
        public void LoadData(ShipmentModel shipment)
        {
            if (shipment == null) return;

            try
            {
                _originalShipmentNumber = shipment.ShipmentNumber;
                ShipmentDate = shipment.ShipmentDate;
                SelectedCompanyName = shipment.CompanyName;  // 거래처명 설정
                SelectedProduct = Products.FirstOrDefault(p => p.ProductCode == shipment.ProductCode);
                ProductionDate = shipment.ProductionDate;
                ShipmentQuantity = shipment.ShipmentQuantity;
                VehicleNumber = shipment.VehicleNumber;

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
            ValidateProduct();
            ValidateCompany();
            ValidateShipmentDate();
            ValidateQuantity();
            ValidateVehicleNumber();
            ValidateProductionDate();
            _ = ValidateInventory();

            return !HasError;
        }
        private void ValidateShipmentDate()
        {
            if (!_isEditMode && ShipmentDate < DateTime.Today)
            {
                _errors.Add("출하일자는 현재 날짜 이후여야 합니다.");
            }
            else
            {
                _errors.Remove("출하일자는 현재 날짜 이후여야 합니다.");
            }
            UpdateErrorMessage();
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
        private void ValidateProductionDate()
        {
            if (ProductionDate > DateTime.Today)
            {
                _errors.Add("생산일자는 현재 날짜 이전이어야 합니다.");
            }
            else
            {
                _errors.Remove("생산일자는 현재 날짜 이전이어야 합니다.");
            }
            UpdateErrorMessage();
        }
        private void ValidateQuantity()
        {
            if (ShipmentQuantity<=0)
            {
                _errors.Add("출하수량은 0보다 커야 합니다.");
            }
            else
            {
                _errors.Remove("출하수량은 0보다 커야 합니다.");
                _ = ValidateInventory();
            }
            UpdateErrorMessage();
        }
        private void ValidateVehicleNumber()
        {
            if (string.IsNullOrEmpty(VehicleNumber))
            {
                _errors.Add("차량번호를 입력해주세요.");
            }
            else
            {
                _errors.Remove("차량번호를 입력해주세요.");
            }
            UpdateErrorMessage();
        }
        private async Task ValidateInventory()
        {
            if (SelectedProduct == null || ShipmentQuantity <= 0)
                return;

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    var sql = @"
                    SELECT COALESCE(SUM(CASE 
                        WHEN transaction_type = '입고' THEN inventory_quantity
                        WHEN transaction_type = '출고' THEN -inventory_quantity
                        ELSE 0
                    END), 0) as current_stock
                    FROM dt_inventory_management
                    WHERE product_code = @ProductCode";

                    var currentStock = await conn.QuerySingleAsync<int>(sql, new { ProductCode = SelectedProduct.ProductCode });

                    if (currentStock < ShipmentQuantity)
                    {
                        _errors.Add($"재고가 부족합니다. 현재고: {currentStock}, 요청수량: {ShipmentQuantity}");
                    }
                    else
                    {
                        _errors.RemoveWhere(e=>e.Contains("재고가 부족합니다. 현재고:"));
                    }
                }
                _errors.RemoveWhere(e => e.Contains("재고 확인 중 오류가 발생했습니다:"));
            }
            catch (Exception ex)
            {
                _errors.Add($"재고 확인 중 오류가 발생했습니다: {ex.Message}");
            }
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
            return ValidateAll();
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
                    string shipmentNumber = _originalShipmentNumber;

                    if (Mode == FormMode.Edit)
                    {
                        sql = @"UPDATE dt_shipment 
                           SET shipment_date = @ShipmentDate,
                               company_code = @CompanyCode,
                               company_name = @CompanyName,
                               product_code = @ProductCode,
                               production_date = @ProductionDate,
                               shipment_quantity = @ShipmentQuantity,
                               vehicle_number = @VehicleNumber,
                               employee_name = @EmployeeName
                           WHERE shipment_number = @ShipmentNumber";

                        parameters.Add("@ShipmentNumber", _originalShipmentNumber);
                    }
                    else
                    {
                        var sequenceQuery = @"
                        SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(shipment_number, '-', -1) AS UNSIGNED)), 0) + 1
                        FROM dt_shipment 
                        WHERE DATE(shipment_date) = @ShipmentDate";

                        var nextSequence = await conn.QuerySingleAsync<int>(sequenceQuery, new { ShipmentDate });
                        shipmentNumber = $"SH-{ShipmentDate:yyyyMMdd}-{nextSequence:D3}";

                        sql = @"INSERT INTO dt_shipment (
                        shipment_number, company_code, company_name, product_code,
                        production_date, shipment_date, shipment_quantity,
                        vehicle_number, employee_name
                    ) VALUES (
                        @ShipmentNumber, @CompanyCode, @CompanyName, @ProductCode,
                        @ProductionDate, @ShipmentDate, @ShipmentQuantity,
                        @VehicleNumber, @EmployeeName
                    )";

                        parameters.Add("@ShipmentNumber", shipmentNumber);
                    }

                    // 회사 코드는 회사명에서 자동 생성 (예: 첫 2글자 + 마지막 2글자)
                    string companyCode = string.IsNullOrEmpty(SelectedCompanyName) ? "" :
                        SelectedCompanyName.Length >= 4
                            ? SelectedCompanyName.Substring(0, 2) + SelectedCompanyName.Substring(SelectedCompanyName.Length - 2, 2)
                            : SelectedCompanyName.Substring(0, SelectedCompanyName.Length);  // 길이가 짧을 경우 전체 사용

                    parameters.Add("@ShipmentDate", ShipmentDate);
                    parameters.Add("@CompanyCode", companyCode);
                    parameters.Add("@CompanyName", SelectedCompanyName);
                    parameters.Add("@ProductCode", SelectedProduct.ProductCode);
                    parameters.Add("@ProductionDate", ProductionDate);
                    parameters.Add("@ShipmentQuantity", ShipmentQuantity);
                    parameters.Add("@VehicleNumber", VehicleNumber);
                    parameters.Add("@EmployeeName", App.CurrentUser.UserName);

                    await conn.ExecuteAsync(sql, parameters);

                    // 로그 저장
                    string actionType = Mode == FormMode.Edit ? "출하내역 수정" : "출하내역 등록";
                    string actionDetail = $"출하번호: {shipmentNumber}, " +
                                        $"출하일자: {ShipmentDate:yyyy-MM-dd}, " +
                                        $"거래처: {SelectedCompanyName}, " +
                                        $"제품: {SelectedProduct?.ProductName}, " +
                                        $"수량: {ShipmentQuantity}, " +
                                        $"차량번호: {VehicleNumber}";

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

        // INotifyPropertyChanged 구현
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}