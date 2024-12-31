using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MES.Solution.Helpers;
using MES.Solution.Services;
using System.Windows;
using MES.Solution.Models;
using System.Collections.Generic;
using System.Linq;

namespace MES.Solution.ViewModels.Equipment
{
    public class EquipmentMaintenanceScheduleAddViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 상수
        private const int MAX_EQUIPMENT_CODE_LENGTH = 20;
        private const int MAX_COMPANY_NAME_LENGTH = 100;
        private const int MAX_CONTACT_NUMBER_LENGTH = 20;
        private const int MAX_CONTACT_PERSON_LENGTH = 50;
        private const decimal MIN_TEMP = -50;
        private const decimal MAX_TEMP = 100;
        private const decimal MIN_HUMIDITY = 0;
        private const decimal MAX_HUMIDITY = 100;

        // 서비스 관련
        private readonly EquipmentMaintenanceScheduleService _service;
        private readonly LogService _logService;

        // 상태 관련
        private readonly bool _isEditMode;
        private string _originalEquipmentCode;
        private FormMode _mode;
        private string _windowTitle;
        private string _errorMessage;
        private bool _hasError;
        private HashSet<string> _errors = new HashSet<string>();

        // 데이터 관련
        private string _equipmentCode;
        private string _selectedProductionLine;
        private string _equipmentCompanyName;
        private string _equipmentContactNumber;
        private string _equipmentContactPerson;
        private DateTime _inspectionDate = DateTime.Today;
        private string _selectedInspectionFrequency;
        private decimal _temperature;
        private decimal _humidity;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RequestClose;
        #endregion

        
        #region Constructor
        public EquipmentMaintenanceScheduleAddViewModel(bool isEdit = false)
        {
            // 서비스 초기화
            _service = new EquipmentMaintenanceScheduleService();
            _logService = new LogService();
            _isEditMode = isEdit;

            // 기본값 설정
            Mode = isEdit ? FormMode.Edit : FormMode.Add;
            WindowTitle = isEdit ? "장비점검 일정 수정" : "장비점검 일정 등록";

            // 컬렉션 초기화
            ProductionLines = new ObservableCollection<string> { "라인1", "라인2", "라인3" };
            InspectionFrequencies = new ObservableCollection<string> { "월간", "분기" };

            // 명령 초기화
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);

            ValidateAll();
        }
        #endregion


        #region Properties
        // 상태 관련 속성
        public FormMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    WindowTitle = value == FormMode.Add ? "장비점검 일정 등록" : "장비점검 일정 수정";
                    OnPropertyChanged();
                }
            }
        }
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                OnPropertyChanged();
            }
        }
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

        // 데이터 속성
        public string EquipmentCode
        {
            get => _equipmentCode;
            set
            {
                if (value?.Length > MAX_EQUIPMENT_CODE_LENGTH)
                {
                    _equipmentCode = value.Substring(0, MAX_EQUIPMENT_CODE_LENGTH);
                    _errors.Add($"장비코드는 {MAX_EQUIPMENT_CODE_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _equipmentCode = value;
                    _errors.Remove($"장비코드는 {MAX_EQUIPMENT_CODE_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateEquipmentCode();
                OnPropertyChanged();
            }
        }
        public string SelectedProductionLine
        {
            get => _selectedProductionLine;
            set
            {
                _selectedProductionLine = value;
                ValidateProductionLine();
                OnPropertyChanged();
            }
        }
        public string EquipmentCompanyName
        {
            get => _equipmentCompanyName;
            set
            {
                if (value?.Length > MAX_COMPANY_NAME_LENGTH)
                {
                    _equipmentCompanyName = value.Substring(0, MAX_COMPANY_NAME_LENGTH);
                    _errors.Add($"장비업체명은 {MAX_COMPANY_NAME_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _equipmentCompanyName = value;
                    _errors.Remove($"장비업체명은 {MAX_COMPANY_NAME_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateEquipmentCompanyName();
                OnPropertyChanged();
            }
        }
        public string EquipmentContactNumber
        {
            get => _equipmentContactNumber;
            set
            {
                if (value?.Length > MAX_CONTACT_NUMBER_LENGTH)
                {
                    _equipmentContactNumber = value.Substring(0, MAX_CONTACT_NUMBER_LENGTH);
                    _errors.Add($"업체연락처는 {MAX_CONTACT_NUMBER_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _equipmentContactNumber = value;
                    _errors.Remove($"업체연락처는 {MAX_CONTACT_NUMBER_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateEquipmentContactNumber();
                OnPropertyChanged();
            }
        }
        public string EquipmentContactPerson
        {
            get => _equipmentContactPerson;
            set
            {
                if (value?.Length > MAX_CONTACT_PERSON_LENGTH)
                {
                    _equipmentContactPerson = value.Substring(0, MAX_CONTACT_PERSON_LENGTH);
                    _errors.Add($"담당자는 {MAX_CONTACT_PERSON_LENGTH}자를 초과할 수 없습니다.");
                }
                else
                {
                    _equipmentContactPerson = value;
                    _errors.Remove($"담당자는 {MAX_CONTACT_PERSON_LENGTH}자를 초과할 수 없습니다.");
                }
                ValidateEquipmentContactPerson();
                OnPropertyChanged();
            }
        }
        public DateTime InspectionDate
        {
            get => _inspectionDate;
            set
            {
                _inspectionDate = value;
                ValidateInspectionDate();
                OnPropertyChanged();
            }
        }
        public string SelectedInspectionFrequency
        {
            get => _selectedInspectionFrequency;
            set
            {
                _selectedInspectionFrequency = value;
                ValidateInspectionFrequency();
                OnPropertyChanged();
            }
        }
        public decimal Temperature
        {
            get => _temperature;
            set
            {
                if (value < MIN_TEMP)
                    _temperature = MIN_TEMP;
                else if (value > MAX_TEMP)
                    _temperature = MAX_TEMP;
                else
                    _temperature = value;

                ValidateTemperature();
                OnPropertyChanged();
            }
        }
        public decimal Humidity
        {
            get => _humidity;
            set
            {
                if (value < MIN_HUMIDITY)
                    _humidity = MIN_HUMIDITY;
                else if (value > MAX_HUMIDITY)
                    _humidity = MAX_HUMIDITY;
                else
                    _humidity = value;

                ValidateHumidity();
                OnPropertyChanged();
            }
        }
        #endregion


        #region Collections
        public ObservableCollection<string> ProductionLines { get; }
        public ObservableCollection<string> InspectionFrequencies { get; }
        #endregion


        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        #endregion


        #region Methods
        // 데이터 로드
        public void LoadData(MaintenanceScheduleModel model)
        {
            if (model == null) return;

            try
            {
                _originalEquipmentCode = model.EquipmentCode;
                EquipmentCode = model.EquipmentCode;
                SelectedProductionLine = model.ProductionLine;
                EquipmentCompanyName = model.EquipmentCompanyName;
                EquipmentContactNumber = model.EquipmentContactNumber;
                EquipmentContactPerson = model.EquipmentContactPerson;
                InspectionDate = model.InspectionDate;
                SelectedInspectionFrequency = model.InspectionFrequency;
                Temperature = model.Temperature;
                Humidity = model.Humidity;

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
            ValidateEquipmentCode();
            ValidateProductionLine();
            ValidateEquipmentCompanyName();
            ValidateEquipmentContactNumber();
            ValidateEquipmentContactPerson();
            ValidateInspectionDate();
            ValidateInspectionFrequency();
            ValidateTemperature();
            ValidateHumidity();

            return !HasError;
        }
        private void ValidateEquipmentCode()
        {
            if (string.IsNullOrEmpty(EquipmentCode))
            {
                _errors.Add("장비코드를 입력하세요.");
            }
            else
            {
                _errors.Remove("장비코드를 입력하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateProductionLine()
        {
            if (string.IsNullOrEmpty(SelectedProductionLine))
            {
                _errors.Add("생산라인을 선택하세요.");
            }
            else
            {
                _errors.Remove("생산라인을 선택하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateEquipmentCompanyName()
        {
            if (string.IsNullOrEmpty(EquipmentCompanyName))
            {
                _errors.Add("장비업체명을 입력하세요.");
            }
            else
            {
                _errors.Remove("장비업체명을 입력하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateEquipmentContactNumber()
        {
            if (string.IsNullOrEmpty(EquipmentContactNumber))
            {
                _errors.Add("업체연락처를 입력하세요.");
            }
            else
            {
                _errors.Remove("업체연락처를 입력하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateEquipmentContactPerson()
        {
            if (string.IsNullOrEmpty(EquipmentContactPerson))
            {
                _errors.Add("담당자를 입력하세요.");
            }
            else
            {
                _errors.Remove("담당자를 입력하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateInspectionDate()
        {
            if (InspectionDate == DateTime.MinValue)
            {
                _errors.Add("점검일자를 선택하세요.");
            }
            else
            {
                _errors.Remove("점검일자를 선택하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateInspectionFrequency()
        {
            if (string.IsNullOrEmpty(SelectedInspectionFrequency))
            {
                _errors.Add("점검주기를 선택하세요.");
            }
            else
            {
                _errors.Remove("점검주기를 선택하세요.");
            }
            UpdateErrorMessage();
        }
        private void ValidateTemperature()
        {
            if (Temperature < -50 || Temperature > 100)
            {
                _errors.Add("온도는 -50°C에서 100°C 사이여야 합니다.");
            }
            else
            {
                _errors.Remove("온도는 -50°C에서 100°C 사이여야 합니다.");
            }
            UpdateErrorMessage();
        }
        private void ValidateHumidity()
        {
            if (Humidity < 0 || Humidity > 100)
            {
                _errors.Add("습도는 0%에서 100% 사이여야 합니다.");
            }
            else
            {
                _errors.Remove("습도는 0%에서 100% 사이여야 합니다.");
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
            return !HasError &&
                   !string.IsNullOrEmpty(EquipmentCode) &&
                   !string.IsNullOrEmpty(SelectedProductionLine) &&
                   !string.IsNullOrEmpty(EquipmentCompanyName) &&
                   !string.IsNullOrEmpty(EquipmentContactNumber) &&
                   !string.IsNullOrEmpty(EquipmentContactPerson) &&
                   !string.IsNullOrEmpty(SelectedInspectionFrequency) &&
                   Temperature >= -50 && Temperature <= 100 &&
                   Humidity >= 0 && Humidity <= 100;
        }
        private async void ExecuteSave()
        {
            try
            {
                if (!ValidateAll())
                {
                    return;
                }

                var schedule = new MaintenanceScheduleModel
                {
                    EquipmentCode = EquipmentCode,
                    ProductionLine = SelectedProductionLine,
                    EquipmentCompanyName = EquipmentCompanyName,
                    EquipmentContactNumber = EquipmentContactNumber,
                    EquipmentContactPerson = EquipmentContactPerson,
                    InspectionDate = InspectionDate,
                    InspectionFrequency = SelectedInspectionFrequency,
                    Temperature = Temperature,
                    Humidity = Humidity,
                    EmployeeName = App.CurrentUser.UserName
                };

                if (Mode == FormMode.Edit)
                {
                    await _service.UpdateSchedule(schedule, _originalEquipmentCode);
                    await _logService.SaveLogAsync(App.CurrentUser.UserId, "장비점검 일정 수정",
                        $"장비코드: {schedule.EquipmentCode}, 생산라인: {schedule.ProductionLine}");
                }
                else
                {
                    await _service.AddSchedule(schedule);
                    await _logService.SaveLogAsync(App.CurrentUser.UserId, "장비점검 일정 등록",
                        $"장비코드: {schedule.EquipmentCode}, 생산라인: {schedule.ProductionLine}");
                }

                MessageBox.Show(Mode == FormMode.Edit ? "수정되었습니다." : "등록되었습니다.",
                    "알림", MessageBoxButton.OK, MessageBoxImage.Information);

                RequestClose?.Invoke(this, EventArgs.Empty);
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

        // PropertyChanged 알림
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        #endregion
    }
}