using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using Dapper;
using System.Configuration;
using System.Diagnostics;
using MES.Solution.Helpers;
using MES.Solution.Views;
using MES.Solution.Models;
using System.Linq;
using MES.Solution.Services;

namespace MES.Solution.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        #region Fields
        // 서비스 관련
        private readonly string _connectionString;
        private readonly LogService _logService;

        // 사용자 입력 관련
        private string _username = string.Empty;
        private string _password;
        private string _errorMessage = string.Empty;
        private bool _saveId = false;

        // 잠금 관련
        private DateTime? _lockoutUntil = null;

        // 명령 관련
        private RelayCommand _loginCommand;
        private RelayCommand _registerCommand;
        #endregion


        #region Events
        // ViewModelBase에서 PropertyChanged 상속
        #endregion


        #region Constructor
        public LoginViewModel()
        {
            try
            {
                // 서비스 초기화
                _logService = new LogService();
                _connectionString = ConfigurationManager.ConnectionStrings["MESDatabase"].ConnectionString;

                // 명령 초기화
                RegisterCommand = new RelayCommand(ExecuteRegister);
                LoginCommand = new RelayCommand(ExecuteLogin, CanExecuteLogin);

                // 아이디 저장 확인
                if (Properties.Settings.Default.SaveId)
                {
                    Username = Properties.Settings.Default.LastUsername;
                    SaveId = true;

                }

                // DB 연결 테스트
                using (MySqlConnection connection = new MySqlConnection(_connectionString))
                {
                    connection.Open();
                    Debug.WriteLine("DB 연결 성공");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"초기화 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"Initialization error: {ex.Message}");
            }
        }
        #endregion


        #region Properties
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value))
                {
                    (_loginCommand)?.RaiseCanExecuteChanged();
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
                    (_loginCommand)?.RaiseCanExecuteChanged();
                }
            }
        }
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        public bool SaveId
        {
            get => _saveId;
            set => SetProperty(ref _saveId, value);
        }
        #endregion


        #region Commands
        public ICommand LoginCommand
        {
            get => _loginCommand;
            private set => SetProperty(ref _loginCommand, (RelayCommand)value);
        }
        public ICommand RegisterCommand
        {
            get => _registerCommand;
            private set => SetProperty(ref _registerCommand, (RelayCommand)value);
        }
        #endregion


        #region Methods
        //초기화 메서드

        // 실행 메서드
        private async void ExecuteLogin()
        {
            try
            {
                Debug.WriteLine($"Connecting with connection string: {_connectionString}");

                var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
                if (loginWindow == null)
                {
                    ErrorMessage = "시스템 오류가 발생했습니다.";
                    return;
                }

                var passwordBox = loginWindow.FindName("PasswordBox") as PasswordBox;
                if (passwordBox == null)
                {
                    ErrorMessage = "로그인 정보를 가져올 수 없습니다.";
                    return;
                }
                string password = passwordBox.Password;

                using (MySqlConnection connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();

                    string userExistsQuery = "SELECT * FROM db_user WHERE username = @Username";
                    Debug.WriteLine($"Checking user existence: {Username}");

                    dynamic user = await connection.QueryFirstOrDefaultAsync(userExistsQuery, new { Username });

                    if (user == null)
                    {
                        Debug.WriteLine("User not found");
                        ErrorMessage = "사용자를 찾을 수 없습니다.";
                        return;
                    }

                    if (user.password_hash != password)
                    {
                        Debug.WriteLine("Invalid password");
                        ErrorMessage = "비밀번호가 일치하지 않습니다.";
                        return;
                    }

                    string roleQuery = "SELECT role_name FROM user_roles WHERE role_id = @RoleId";
                    string roleName = await connection.QueryFirstOrDefaultAsync<string>(roleQuery, new { RoleId = user.role_id });

                    Debug.WriteLine($"Login successful. Username: {user.username}, Role: {roleName}");

                    string actionDetail = $"사용자: {user.username}, 역할: {roleName}";
                    await _logService.SaveLogAsync(user.user_id, "로그인", actionDetail);

                    await connection.ExecuteAsync(
                        "UPDATE db_user SET last_login = CURRENT_TIMESTAMP WHERE user_id = @UserId",
                        new { UserId = user.user_id }
                    );

                    Properties.Settings.Default.LastUsername = SaveId ? Username : string.Empty;
                    Properties.Settings.Default.SaveId = SaveId;
                    Properties.Settings.Default.Save();

                    App.CurrentUser = new LoginModel
                    {
                        UserId = user.user_id,
                        UserName = user.username,
                        Email = user.email,
                        UserRole = roleName,
                        LoggedInTime = DateTime.Now
                    };

                    // 메인 윈도우 생성 및 표시
                    MainWindow mainWindow = new MainWindow();
                    mainWindow.Show();

                    // 현재 로그인 창 찾아서 닫기
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is LoginWindow)
                        {
                            window.Close();
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "로그인 중 오류가 발생했습니다.";
                Debug.WriteLine($"Login error: {ex.Message}");
            }
        }
        private void ExecuteRegister()
        {
            try
            {
                // 회원가입 창 생성
                RegisterWindow registerWindow = new RegisterWindow();

                // 현재 로그인 창 찾기
                var loginWindow = Application.Current.Windows.OfType<LoginWindow>().FirstOrDefault();
                if (loginWindow != null)
                {
                    loginWindow.Close();
                }

                // 회원가입 창 표시
                registerWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Register window error: {ex.Message}");
                MessageBox.Show("회원가입 창을 열 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private bool CanExecuteLogin()
        {
            if (_lockoutUntil.HasValue && DateTime.Now < _lockoutUntil.Value)
            {
                return false;
            }
            return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
        }

        // 상태 확인 메서드
        private void ClearLoginData()
        {
            Password = string.Empty;
            Username = string.Empty;
            // 다른 데이터 초기화...
        }
        #endregion
    }
}