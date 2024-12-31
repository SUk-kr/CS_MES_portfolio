using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MES.Solution.Helpers;
using System.Configuration;
using MySql.Data.MySqlClient;
using Dapper;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using MES.Solution.Models;
using MES.Solution.Services;

namespace MES.Solution.ViewModels
{
    public class ContractProductionPlanInputViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 상수
        private const int MAX_REMARKS_LENGTH = 200;

        // 서비스 관련
        private readonly string _connectionString;
        private readonly ContractModel _contract;

        // 에러 처리 관련
        private HashSet<string> _errors = new HashSet<string>();
        private string _errorMessage;
        private bool _hasError;

        // 데이터 관련
        private DateTime _productionDate;
        private string _selectedProductionLine;
        private string _selectedWorkShift;
        private string _remarks;

        // 계산 관련
        private int _currentStock;

        //로그 관련
        private readonly LogService _logService;
        #endregion


        #region Events
        // INotifyPropertyChanged 구현
        public event PropertyChangedEventHandler PropertyChanged;
        // 창 닫기 이벤트
        public event EventHandler RequestClose;
        #endregion


        #region Constructor
        // 뷰모델 생성자 - 초기화 로직 포함
        public ContractProductionPlanInputViewModel(ContractModel contract)
        {
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;
            _contract = contract;
            _productionDate = DateTime.Today;

            _logService = new LogService();  // LogService 초기화

            // 콤보박스 데이터 초기화
            ProductionLines = new ObservableCollection<string> { "라인1", "라인2", "라인3" };
            WorkShifts = new ObservableCollection<string> { "주간1", "주간2", "야간1", "야간2" };

            // 기본값 설정
            SelectedProductionLine = ProductionLines[0];
            SelectedWorkShift = WorkShifts[0];

            // 커맨드 초기화
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
        }
        #endregion


        #region Properties
        // 계약 정보
        public ContractModel Contract => _contract;

        // 생산 계획 데이터 속성
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
                    OnPropertyChanged();
                }
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

        // 계산 관련
        public int CurrentStock
        {
            get => _currentStock;
            set
            {
                _currentStock = value;
                OnPropertyChanged();
            }
        }
        #endregion


        #region Collections
        // UI 바인딩용 컬렉션
        public ObservableCollection<string> ProductionLines { get; }
        public ObservableCollection<string> WorkShifts { get; }
        #endregion


        #region Commands
        // UI 액션 커맨드
        public ICommand SaveCommand { get; }
        #endregion


        #region Methods
        // 유효성 검사 메서드
        private bool ValidateAll()
        {
            ValidateProductionDate();
            return !HasError;
        }

        private void ValidateProductionDate()
        {
            if (ProductionDate > Contract.DeliveryDate)
            {
                _errors.Add("생산일자는 납기일을 초과할 수 없습니다.");
            }
            else
            {
                _errors.Remove("생산일자는 납기일을 초과할 수 없습니다.");
            }
            UpdateErrorMessage();
        }

        private bool CanExecuteSave()
        {
            return !HasError;
        }

        private void UpdateErrorMessage()
        {
            ErrorMessage = _errors.FirstOrDefault();
            HasError = _errors.Any();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // 실행 메서드
        private async void ExecuteSave()
        {
            try
            {
                if (!ValidateAll())
                    return;

                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 작업지시 번호 생성
                            var sequenceQuery = @"
                        SELECT IFNULL(MAX(CAST(SUBSTRING_INDEX(work_order_code, '-', -1) AS UNSIGNED)), 0) + 1
                        FROM dt_production_plan 
                        WHERE DATE(production_date) = @ProductionDate";

                            var nextSequence = await conn.QuerySingleAsync<int>(sequenceQuery, new { ProductionDate });
                            var workOrderCode = $"PP-{ProductionDate:yyyyMMdd}-{nextSequence:D3}";

                            // work_order_sequence 계산
                            var sequenceCountQuery = @"
                        SELECT COUNT(*) + 1
                        FROM dt_production_plan 
                        WHERE DATE(production_date) = @ProductionDate";

                            var workOrderSequence = await conn.QuerySingleAsync<int>(sequenceCountQuery, new { ProductionDate });

                            // 생산계획 등록
                            var sql = @"
                        INSERT INTO dt_production_plan (
                            work_order_code, order_number, production_date, product_code,
                            order_quantity, work_order_sequence, production_line,
                            work_shift, process_status, remarks, employee_name
                        ) VALUES (
                            @WorkOrderCode, @OrderNumber, @ProductionDate, @ProductCode,
                            @OrderQuantity, @WorkOrderSequence, @ProductionLine,
                            @WorkShift, @ProcessStatus, @Remarks, @EmployeeName
                        )";

                            var parameters = new
                            {
                                WorkOrderCode = workOrderCode,
                                OrderNumber = Contract.OrderNumber,    // 수주번호 연계
                                ProductionDate = ProductionDate,
                                ProductCode = Contract.ProductCode,
                                OrderQuantity = Contract.Quantity,
                                WorkOrderSequence = workOrderSequence,
                                ProductionLine = SelectedProductionLine,
                                WorkShift = SelectedWorkShift,
                                ProcessStatus = "대기",
                                Remarks = $"수주번호: {Contract.OrderNumber} - {Remarks}",  // 비고에 수주번호 추가
                                EmployeeName = App.CurrentUser.UserName
                            };

                            await conn.ExecuteAsync(sql, parameters, transaction);

                            // 로그 저장
                            await _logService.SaveLogAsync(
                                App.CurrentUser.UserId,
                                "생산계획등록",
                                $"작업지시번호: {workOrderCode}, " +
                                $"수주번호: {Contract.OrderNumber}, " +
                                $"제품: {Contract.ProductName}, " +
                                $"수량: {Contract.Quantity}"
                            );

                            transaction.Commit();
                            MessageBox.Show("생산계획이 등록되었습니다.", "알림");
                            RequestClose?.Invoke(this, EventArgs.Empty);
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
                MessageBox.Show($"생산계획 등록 중 오류가 발생했습니다: {ex.Message}", "오류");
            }
        }

        // 계산 메서드
        private async void LoadCurrentStock()
        {
            try
            {
                var inventoryService = new InventoryChartService(_connectionString);
                CurrentStock = await inventoryService.GetCurrentStock(_contract.ProductCode);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"현재고 조회 중 오류가 발생했습니다: {ex.Message}", "오류");
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