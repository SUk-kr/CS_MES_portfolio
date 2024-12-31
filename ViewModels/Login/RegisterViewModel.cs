using System;
using System.Windows;
using System.Windows.Input;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Diagnostics;
using MES.Solution.Helpers;
using System.Windows.Controls;
using MES.Solution.Views;
using System.Linq;

namespace MES.Solution.ViewModels
{
    public class RegisterViewModel : ViewModelBase
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;

        // 정규식 패턴
        private static readonly Regex UsernamePattern = new Regex(@"^[a-zA-Z0-9_-]{2,10}$");//간단한패턴 (영문두글자이상)
        private static readonly Regex EmailPattern = new Regex(@"^[^@]+@[^@]+\.[^@]+$");//간단한패턴 (1글자 + @ + 1글자 + . + 1글자)
        private static readonly Regex PasswordPattern = new Regex(@"^(?=.*[a-zA-Z])(?=.*\d)[a-zA-Z0-9]{6,12}$");//간단한패턴 (영문+숫자)
        //private static readonly Regex PasswordPattern = new Regex(@"^[a-zA-Z]+[0-9]{6,12}$");//간단한패턴 (영문+숫자)
        //private static readonly Regex UsernamePattern = new Regex(@"^[a-zA-Z0-9_-]{4,20}$");//영문4글자이상
        //private static readonly Regex EmailPattern = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");//
        //private static readonly Regex PasswordPattern = new Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,20}$");

        // UI 컨트롤 
        private PasswordBox _passwordBox;
        private PasswordBox _confirmPasswordBox;

        // 사용자 입력 데이터
        private string _username = string.Empty;
        private string _email = string.Empty;
        private string _selectedRole;
        private string _password;
        private string _confirmPassword;

        // 검증 상태
        private bool _isUsernameChecked;
        private bool _isEmailChecked;
        private bool _canRegister;

        // 오류 메시지
        private string _usernameError = string.Empty;
        private string _emailError = string.Empty;
        private string _passwordError = string.Empty;
        private string _confirmPasswordError = string.Empty;
        private string _permissionError = string.Empty;
        private string _generalError = string.Empty;
        private string _passwordTotalError = string.Empty;

        // 명령
        private RelayCommand _registerCommand;
        #endregion


        #region Events
        // ViewModelBase에서 PropertyChanged 상속
        #endregion


        #region Constructor
        public RegisterViewModel()
        {
            // 서비스 초기화
            _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

            // 명령 초기화
            _registerCommand = new RelayCommand(ExecuteRegister, CanExecuteRegister);  // RelayCommand 필드에 할당
            CheckUsernameCommand = new RelayCommand(ExecuteCheckUsername);
            CheckEmailCommand = new RelayCommand(ExecuteCheckEmail);

            // 기본값 설정
            SelectedRole = "USER";
        }
        #endregion


        #region Properties
        // 입력 데이터 속성
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    _isUsernameChecked = false;
                    ValidateUsername();
                    UpdateCanRegister();
                }
            }
        }
        public string Email
        {
            get => _email;
            set
            {
                if (SetProperty(ref _email, value))
                {
                    _isEmailChecked = false;
                    ValidateEmail();
                    UpdateCanRegister();
                }
            }
        }
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                {
                    ValidatePassword(value);
                    ValidateConfirmPassword(value, ConfirmPassword);  // 비밀번호가 변경되면 확인도 다시 검증
                    UpdateCanRegister();
                    if (PasswordError == "")
                    {
                        PasswordTotalError = ConfirmPasswordError;
                    }
                    else
                    {
                        PasswordTotalError = PasswordError;
                    }

                }
            }
        }
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                {
                    ValidateConfirmPassword(Password, value);
                    UpdateCanRegister();
                    if (PasswordError == "")
                    {
                        PasswordTotalError = ConfirmPasswordError;
                    }
                    else
                    {
                        PasswordTotalError = PasswordError;
                    }
                }
            }
        }
        public string SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (SetProperty(ref _selectedRole, value))
                {
                    OnPropertyChanged();
                    UpdateCanRegister();
                }
            }
        }
        public bool CanRegister
        {
            get => _canRegister;
            private set
            {
                if (_canRegister != value)
                {
                    _canRegister = value;
                    OnPropertyChanged(nameof(CanRegister));
                    CommandManager.InvalidateRequerySuggested(); // 모든 Command의 CanExecute를 갱신
                }
            }
        }

        // 오류 메시지 속성
        public string UsernameError
        {
            get => _usernameError;
            set => SetProperty(ref _usernameError, value);
        }
        public string EmailError
        {
            get => _emailError;
            set => SetProperty(ref _emailError, value);
        }
        public string PasswordTotalError
        {
            get => _passwordTotalError;
            set => SetProperty(ref _passwordTotalError, value);
        }
        public string ConfirmPasswordError
        {
            get => _confirmPasswordError;
            set => SetProperty(ref _confirmPasswordError, value);
        }
        public string PermissionError
        {
            get => _permissionError;
            set => SetProperty(ref _permissionError, value);
        }
        public string GeneralError
        {
            get => _generalError;
            set => SetProperty(ref _generalError, value);
        }
        public string PasswordError
        {
            get => _passwordError;
            set => SetProperty(ref _passwordError, value);
        }
        #endregion


        #region Commands
        public RelayCommand RegisterCommand
        {
            get => _registerCommand;
            private set => _registerCommand = value;
        }
        public ICommand CheckUsernameCommand { get; }
        public ICommand CheckEmailCommand { get; }
        #endregion


        #region Methods
        // 유효성 검사 메서드
        private bool ValidateUsername()
        {
            if (string.IsNullOrEmpty(Username))
            {
                UsernameError = "아이디를 입력하세요.";
                return false;
            }

            if (!UsernamePattern.IsMatch(Username))
            {
                //UsernameError = "아이디는 4~20자의 영문, 숫자, 특수문자(-_)만 사용 가능합니다.";
                UsernameError = "아이디는 2~10자의 영문, 숫자만 사용 가능합니다.";
                return false;
            }

            if (!_isUsernameChecked)
            {
                UsernameError = "중복 확인이 필요합니다.";
                return false;
            }

            UsernameError = string.Empty;
            return true;
        }
        private bool ValidateEmail()
        {
            if (string.IsNullOrEmpty(Email))
            {
                EmailError = "이메일을 입력하세요.";
                return false;
            }

            if (!EmailPattern.IsMatch(Email))
            {
                EmailError = "올바른 이메일 형식이 아닙니다.";
                return false;
            }

            if (!_isEmailChecked)
            {
                EmailError = "중복 확인이 필요합니다.";
                return false;
            }

            EmailError = string.Empty;
            return true;
        }
        private bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                PasswordError = "비밀번호를 입력하세요.";
                return false;
            }

            if (!PasswordPattern.IsMatch(password))
            {
                //PasswordError = "비밀번호는 8~20자의 영문 대/소문자, 숫자, 특수문자를 포함해야 합니다.";
                PasswordError = "비밀번호는 6~12자의 영문 ,숫자를 포함해야 합니다.";
                return false;
            }

            PasswordError = string.Empty;
            return true;
        }
        private bool ValidateConfirmPassword(string password, string confirmPassword)
        {
            if (string.IsNullOrEmpty(confirmPassword))
            {
                ConfirmPasswordError = "비밀번호 확인을 입력하세요.";
                return false;
            }

            // 비밀번호가 패턴을 만족하지 않으면 확인 검증을 하지 않음
            if (!PasswordPattern.IsMatch(password))
            {
                ConfirmPasswordError = "올바른 비밀번호를 먼저 입력하세요.";
                return false;
            }

            if (password != confirmPassword)
            {
                ConfirmPasswordError = "비밀번호가 일치하지 않습니다.";
                return false;
            }

            ConfirmPasswordError = string.Empty;
            return true;
        }
        private bool ValidatePermission()
        {
            if (string.IsNullOrEmpty(SelectedRole))
            {
                PermissionError = "권한을 선택하세요.";
                return false;
            }

            PermissionError = string.Empty;
            return true;
        }
        private void UpdateCanRegister()
        {
            bool isPasswordValid = !string.IsNullOrEmpty(Password) && PasswordPattern.IsMatch(Password);
            bool isConfirmPasswordValid = !string.IsNullOrEmpty(ConfirmPassword) && Password == ConfirmPassword && isPasswordValid;

            bool isValid = !string.IsNullOrEmpty(Username)
                          && !string.IsNullOrEmpty(Email)
                          && isPasswordValid
                          && isConfirmPasswordValid
                          && !string.IsNullOrEmpty(UsernameError)
                          && !string.IsNullOrEmpty(EmailError)
                          && string.IsNullOrEmpty(PasswordError)
                          && string.IsNullOrEmpty(ConfirmPasswordError)
                          && _isUsernameChecked
                          && _isEmailChecked;

            CanRegister = isValid;
            RegisterCommand.RaiseCanExecuteChanged();
        }
        private bool CanExecuteRegister()
        {
            return CanRegister;
        }

        // 실행 메서드
        private void ExecuteCheckUsername()
        {
            try
            {
                if (!UsernamePattern.IsMatch(Username))
                {
                    //UsernameError = "아이디는 4~20자의 영문, 숫자, 특수문자(-_)만 사용 가능합니다.";
                    UsernameError = "아이디는 2~10자의 영문, 숫자만 사용 가능합니다.";
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT COUNT(1) FROM db_user WHERE username = @Username";
                    int count = connection.ExecuteScalar<int>(sql, new { Username });

                    _isUsernameChecked = count == 0;
                    UsernameError = _isUsernameChecked ? "사용 가능한 아이디입니다." : "이미 사용중인 아이디입니다.";
                    UpdateCanRegister();
                }
            }
            catch (Exception ex)
            {
                UsernameError = "중복 확인 중 오류가 발생했습니다.";
                Debug.WriteLine($"Username check error: {ex.Message}");
                UpdateCanRegister();
            }
        }
        private void ExecuteCheckEmail()
        {
            try
            {
                if (!EmailPattern.IsMatch(Email))
                {
                    EmailError = "올바른 이메일 형식이 아닙니다.";
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(_connectionString))
                {
                    connection.Open();
                    string sql = "SELECT COUNT(1) FROM db_user WHERE email = @Email";
                    int count = connection.ExecuteScalar<int>(sql, new { Email });

                    _isEmailChecked = count == 0;
                    EmailError = _isEmailChecked ? "사용 가능한 이메일입니다." : "이미 사용중인 이메일입니다.";
                    UpdateCanRegister();
                }
            }
            catch (Exception ex)
            {
                EmailError = "중복 확인 중 오류가 발생했습니다.";
                Debug.WriteLine($"Email check error: {ex.Message}");
                UpdateCanRegister();
            }
        }
        private void ExecuteRegister()
        {
            try
            {
                if (_passwordBox == null || _confirmPasswordBox == null)
                {
                    GeneralError = "시스템 오류가 발생했습니다.";
                    return;
                }

                if (!ValidateUsername() ||
                    !ValidateEmail() ||
                    !ValidatePassword(_passwordBox.Password) ||
                    !ValidateConfirmPassword(_passwordBox.Password, _confirmPasswordBox.Password) ||
                    !ValidatePermission())
                {
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(_connectionString))
                {
                    connection.Open();
                    using (MySqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            string checkSql = "SELECT COUNT(1) FROM db_user WHERE username = @Username OR email = @Email";
                            int exists = connection.ExecuteScalar<int>(checkSql, new { Username, Email }, transaction);
                            if (exists > 0)
                            {
                                GeneralError = "아이디 또는 이메일이 이미 사용중입니다.";
                                transaction.Rollback();
                                return;
                            }

                            string sql = @"
                            INSERT INTO db_user (username, password_hash, email, role_id, created_at, is_active) 
                            VALUES (@Username, @Password, @Email, @RoleId, CURRENT_TIMESTAMP, 1)";


                            int roleId;
                            switch (SelectedRole)
                            {
                                case "ADMIN":
                                    roleId = 1;
                                    break;
                                case "USER":
                                default:
                                    roleId = 2;
                                    break;
                            }

                            connection.Execute(sql, new
                            {
                                Username,
                                Password = _passwordBox.Password,
                                Email,
                                RoleId = roleId
                            }, transaction);

                            transaction.Commit();
                            MessageBox.Show("회원가입이 완료되었습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);

                            // 현재 Window 찾아서 닫기
                            if (Application.Current.Windows.OfType<RegisterWindow>().FirstOrDefault() is Window registerWindow)
                            {
                                // 새로운 로그인 창 생성
                                var loginWindow = new LoginWindow();
                                loginWindow.Show();

                                // 회원가입 창 닫기
                                registerWindow.Close();
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
                GeneralError = "회원가입 중 오류가 발생했습니다.";
                Debug.WriteLine($"Registration error: {ex.Message}");
            }
        }

        // UI 관련 메서드
        public void SetPasswordBoxes(PasswordBox passwordBox, PasswordBox confirmPasswordBox)
        {
            _passwordBox = passwordBox;
            _confirmPasswordBox = confirmPasswordBox;
        }
        #endregion
    }
}