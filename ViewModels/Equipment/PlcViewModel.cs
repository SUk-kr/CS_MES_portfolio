using ActUtlTypeLib;
using MES.Solution.Helpers;
using MES.Solution.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MES.Solution.ViewModels.Equipment
{
    public class PlcViewModel : INotifyPropertyChanged
    {
        #region Fields
        // 서비스 관련
        private readonly LogService _logService;

        // PLC 관련
        private readonly Dictionary<int, ActUtlType> PLCs = new Dictionary<int, ActUtlType>();
        private readonly Dictionary<int, DispatcherTimer> ConnectionCheckTimers;
        private readonly Dictionary<int, DispatcherTimer> WatchJobCheckTimers;
        private readonly Dictionary<int, bool> ConnectionStates;
        private readonly Dictionary<int, string> CurrentActions;
        private readonly Dictionary<int, int[]> MonitorData;
        private static readonly Dictionary<int, int> PlcStationNumbers = new Dictionary<int, int>
        {
            { 1, 1 }, // 라인 1의 스테이션 번호
            { 2, 2 }, // 라인 2의 스테이션 번호
            { 3, 3 }  // 라인 3의 스테이션 번호
        };

        // 상태 관련
        private string _operationStatus;
        private string _quantity = "1";
        private string _selectedAction;
        private const int MAX_QUANTITY = 10;
        private static int iReconnetCount = 0;

        // 컬렉션 관련
        private ObservableCollection<string> _availableActions;
        #endregion


        #region Events
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion


        #region Constructor
        public PlcViewModel()
        {
            // LogService 초기화
            _logService = new LogService();

            // 컬렉션 초기화
            AvailableActions = new ObservableCollection<string> { "동작 1", "동작 2", "동작 3" };
            PlcStatuses = new ObservableCollection<PlcStatusModel>();
            WatchJobCheckTimers = new Dictionary<int, DispatcherTimer>();
            ConnectionStates = new Dictionary<int, bool>();//실행저장용
            CurrentActions = new Dictionary<int, string>();//동작저장용
            MonitorData = new Dictionary<int, int[]>();
            PlcStatuses  = new ObservableCollection<PlcStatusModel>();

            // 동작 리스트 초기화
            AvailableActions = new ObservableCollection<string>
            {
                "동작 1",
                "동작 2",
                "동작 3"
            };

            ConnectionCheckTimers = new Dictionary<int, DispatcherTimer>();

            // 기본 동작 설정
            SelectedAction = AvailableActions[0];

            // 실행 명령 설정
            ExecuteActionCommand = new RelayCommand(ExecuteAction);

            // PLC 인스턴스 초기화
            for (int i = 1; i <= 3; i++)
            {
                PLCs[i] = new ActUtlType();
                MonitorData[i] = new int[1];
                ConnectionStates[i] = false;

                // 연결 감시용 타이머
                var connectionTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                int lineNumber = i;
                connectionTimer.Tick += (s, e) => ConnectionCheckTimer_Tick(lineNumber);
                ConnectionCheckTimers[i] = connectionTimer;

                // 동작 감시용 타이머
                var watchJobTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                watchJobTimer.Tick += (s, e) => WatchJobCheckTimer_Tick(lineNumber);
                WatchJobCheckTimers[i] = watchJobTimer;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                for (int i = 1; i <= 3; i++)
                {
                    PLCs[i] = new ActUtlType();
                    MonitorData[i] = new int[1];
                    ConnectionStates[i] = false;

                    // 연결 감시용 타이머
                    var connectionTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    int lineNumber = i;
                    connectionTimer.Tick += (s, e) => ConnectionCheckTimer_Tick(lineNumber);
                    ConnectionCheckTimers[i] = connectionTimer;

                    // 동작 감시용 타이머
                    var watchJobTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    watchJobTimer.Tick += (s, e) => WatchJobCheckTimer_Tick(lineNumber);
                    WatchJobCheckTimers[i] = watchJobTimer;
                }

                InitializePlcStatuses();
            });
        }
        #endregion


        #region Properties
        public string OperationStatus
        {
            get { return _operationStatus; }
            set
            {
                _operationStatus = value;
                OnPropertyChanged();
            }
        }
        public string Quantity
        {
            get => _quantity;
            set
            {
                if (int.TryParse(value, out int parsedValue))
                {
                    if (parsedValue > MAX_QUANTITY)
                    {
                        _quantity = MAX_QUANTITY.ToString();
                        MessageBox.Show($"최대 {MAX_QUANTITY}개까지만 입력 가능합니다.");
                    }
                    else if (parsedValue < 1)
                    {
                        _quantity = "1";
                    }
                    else
                    {
                        _quantity = parsedValue.ToString();
                    }
                }
                else
                {
                    _quantity = "1";
                }
                OnPropertyChanged();
            }
        }
        public string SelectedAction
        {
            get { return _selectedAction; }
            set
            {
                _selectedAction = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<string> AvailableActions
        {
            get { return _availableActions; }
            set
            {
                _availableActions = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<PlcStatusModel> PlcStatuses { get; }
        #endregion


        #region Commands
        public ICommand ExecuteActionCommand { get; }
        #endregion


        #region Methods
        // 초기화 메서드
        //private void InitializePlc() { /*...*/ }
        private void InitializePlcStatuses()
        {
            for (int i = 1; i <= 3; i++)
            {
                // PlcStatusModel 먼저 생성 및 추가
                var plc = new PlcStatusModel(this, i)
                {
                    Name = $"라인 {i}",
                    Status = "연결 끊김",
                    StatusColor = new SolidColorBrush(Colors.Gray),
                    AvailableActions = new ObservableCollection<string> { "동작 1", "동작 2", "동작 3" },
                    SelectedAction = "동작 1"
                };
                PlcStatuses.Add(plc);

                // 그 다음 PLC 연결 상태 확인
                var plcInstance = PLCs[i];
                try
                {
                    plcInstance.ActLogicalStationNumber = PlcStationNumbers[i];
                    int data = 0;
                    int readRet = plcInstance.GetDevice("SM400", out data);

                    if (readRet == 0 && data == 1)
                    {
                        // PLC가 이미 연결되어 있는 경우
                        StartConnectionMonitoring(i);
                        ConnectionStates[i] = true;
                    }
                    else
                    {
                        // 연결이 안되어 있는 경우
                        ConnectionStates[i] = false;
                    }
                }
                catch
                {
                    // 오류 발생시 상태만 false로
                    ConnectionStates[i] = false;
                }
            }
        }
        private void StartConnectionMonitoring(int lineNumber)//시작시 한번만 돎
        {
            try
            {
                var plc = PLCs[lineNumber];
                string deviceLabel = "SM400";//PLC시작신호로 변경요망
                int size = 1;
                int monitorCycle = 1;
                MonitorData[lineNumber][0] = 1;

                int ret = plc.EntryDeviceStatus(deviceLabel, size, monitorCycle, ref MonitorData[lineNumber][0]);

                if (ret == 0)
                {
                    ConnectionStates[lineNumber] = true;
                    ConnectionCheckTimers[lineNumber].Start();
                    UpdatePlcStatus(lineNumber, "연결됨", Colors.Green);
                }
                else
                {
                    ConnectionStates[lineNumber] = false;
                    UpdatePlcStatus(lineNumber, "연결 실패", Colors.Red);
                    MessageBox.Show($"라인 {lineNumber} 모니터링 시작 실패. 에러코드: {ret}");
                }
            }
            catch (Exception ex)
            {
                ConnectionStates[lineNumber] = false;
                UpdatePlcStatus(lineNumber, "오류 발생", Colors.Red);
                MessageBox.Show($"라인 {lineNumber} 모니터링 설정 중 오류 발생: {ex.Message}");
            }
        }

        // 타이머 관련 메서드
        private async void ConnectionCheckTimer_Tick(int lineNumber)//시간마다 체크(연결감시용)
        {
            try
            {
                var plc = PLCs[lineNumber];
                int data = 0;
                int ret = plc.GetDevice("SM400", out data);


                await Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ret == 0 && data == 1)
                    {
                        if (!ConnectionStates[lineNumber])
                        {
                            ConnectionStates[lineNumber] = true;
                            UpdatePlcStatus(lineNumber, "연결됨", Colors.Green);
                        }
                    }
                    else
                    {
                        if (ConnectionStates[lineNumber])
                        {
                            if (iReconnetCount > 4)//5번 돌때까지 기달려봄(자동재접속유지됨)
                            {
                                ConnectionStates[lineNumber] = false;
                                UpdatePlcStatus(lineNumber, "연결 끊김", Colors.Gray);
                                plc.Close();//완전히 끊음
                                ConnectionCheckTimers[lineNumber].Stop();//연결감시 타이머 정지
                                WatchJobCheckTimers[lineNumber].Stop();//동작감시 타이머 정지
                                iReconnetCount = 0;
                                MessageBox.Show($"라인 {lineNumber} PLC 연결이 끊어졌습니다!");
                            }
                            iReconnetCount++;
                        }
                    }

                    return System.Threading.Tasks.Task.CompletedTask;
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ConnectionStates[lineNumber] = false;
                    UpdatePlcStatus(lineNumber, "오류 발생", Colors.Red);
                    ConnectionCheckTimers[lineNumber].Stop();
                    WatchJobCheckTimers[lineNumber].Stop();
                    MessageBox.Show($"라인 {lineNumber} 연결 확인 중 오류 발생: {ex.Message}");
                });
            }
        }
        private void WatchJobCheckTimer_Tick(int lineNumber)//시간마다 체크(동작감시용)
        {
            try
            {
                var plc = PLCs[lineNumber];
                int watchdata = 0;
                int watchret = plc.GetDevice("M8164", out watchdata);//완료신호

                Application.Current.Dispatcher.Invoke(async () =>
                {
                    if (watchdata == 1)
                    {
                        string currentAction = "알 수 없는 동작";
                        if (CurrentActions.ContainsKey(lineNumber))
                        {
                            currentAction = CurrentActions[lineNumber];
                        }
                        WatchJobCheckTimers[lineNumber].Stop();

                        plc.SetDevice("D200", 0);
                        plc.SetDevice("D210", 0);
                        UpdatePlcStatus(lineNumber, "동작 완료", Colors.LawnGreen);
                        MessageBox.Show($"라인 {lineNumber}의 {currentAction} 실행 완료");

                        // 동작 완료 로그
                        string actionDetail = $"라인: {lineNumber}, 동작: {currentAction} 완료";
                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "PLC 동작 완료", actionDetail);
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    WatchJobCheckTimers[lineNumber].Stop();
                    if (CurrentActions.ContainsKey(lineNumber))//오류시에 동작저장삭제
                    {
                        CurrentActions.Remove(lineNumber);
                    }
                    MessageBox.Show($"라인 {lineNumber} 동작 감시 중 오류 발생: {ex.Message}");
                });
            }
        }

        // PLC 동작 메서드
        private void ExecuteAction()
        {
            // 실행 버튼 클릭 시 동작을 실행
            if (!string.IsNullOrEmpty(SelectedAction))
            {
                // 선택된 동작 처리
                MessageBox.Show($"선택된 동작: {SelectedAction}  실행!");
                // 실제 PLC에 동작을 실행하는 로직을 추가할 수 있습니다.
            }
            else
            {
                MessageBox.Show("동작을 선택하세요.");
            }
        }
        public async void ExecutePlcAction(int lineNumber, string action, int actioncount)
        {
            try
            {
                // PLC 연결 상태 확인
                if (!ConnectionStates[lineNumber])
                {
                    MessageBox.Show("PLC가 연결되어 있지 않습니다. 먼저 PLC를 연결해주세요.");
                    return;
                }

                var plc = PLCs[lineNumber];
                UpdatePlcStatus(lineNumber, $"{action} 실행 중", Colors.Orange);

                try
                {
                    // 모드 설정
                    int mode = 0;
                    switch (action)
                    {
                        case "동작 1": mode = 1; break;
                        case "동작 2": mode = 2; break;
                        case "동작 3": mode = 3; break;
                        default:
                            MessageBox.Show("지원하지 않는 동작입니다.");
                            return;
                    }

                    // 모드와 갯수 설정
                    int ret1 = plc.SetDevice("D200", mode);  // 모드 설정
                    int ret2 = plc.SetDevice("D210", actioncount);  // 갯수 설정

                    if (ret1 == 0 && ret2 == 0)
                    {
                        CurrentActions[lineNumber] = $"{action} ({actioncount}개)";
                        MessageBox.Show($"라인 {lineNumber}의 {action} {actioncount}개 실행 시작");
                        WatchJobCheckTimers[lineNumber].Start();

                        // 로그 기록
                        string actionDetail = $"라인: {lineNumber}, 실행: {action}, 갯수: {actioncount}";
                        await _logService.SaveLogAsync(App.CurrentUser.UserId, "PLC 동작 실행", actionDetail);
                    }
                    else
                    {
                        throw new Exception($"PLC 쓰기 실패 (에러코드: {ret1}, {ret2})");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"{action} 실행 중 오류: {ex.Message}");
                }

                // 동작 완료 후 상태 업데이트
                //UpdatePlcStatus(lineNumber, "연결됨", Colors.Green);
            }
            catch (Exception ex)
            {
                UpdatePlcStatus(lineNumber, "오류 발생", Colors.Red);
                MessageBox.Show($"동작 실행 중 오류가 발생했습니다: {ex.Message}");
            }
        }
        public void ConnectPlc(PlcStatusModel plc, int lineNumber)
        {
            try
            {
                var plcInstance = PLCs[lineNumber];
                plcInstance.ActLogicalStationNumber = PlcStationNumbers[lineNumber];
                int ret = plcInstance.Open();

                if (ret == 0)
                {
                    StartConnectionMonitoring(lineNumber);
                    OperationStatus = "연결됨";  // 연결 상태 업데이트
                    UpdatePlcStatus(lineNumber, "연결됨", Colors.Green);  // 상태 업데이트
                    MessageBox.Show($"라인 {lineNumber} PLC 연결 성공");
                }
                else
                {
                    // 연결 실패시 타이머 중지
                    ConnectionCheckTimers[lineNumber].Stop();
                    UpdatePlcStatus(lineNumber, "연결 실패", Colors.Red);
                    OperationStatus = "연결 실패";  // 연결 실패 상태 업데이트
                    MessageBox.Show($"라인 {lineNumber} PLC 연결 실패\n에러코드: 0x{ret:x8} [HEX]");
                }
            }
            catch (Exception ex)
            {
                // 오류 발생시 타이머 중지
                ConnectionCheckTimers[lineNumber].Stop();
                UpdatePlcStatus(lineNumber, "오류 발생", Colors.Red);
                OperationStatus = "오류 발생";  // 오류 상태 업데이트
                MessageBox.Show($"라인 {lineNumber} 연결 중 오류 발생: {ex.Message}");
            }
        }
        public void DisconnectPlc(PlcStatusModel plc, int lineNumber)
        {
            try
            {
                ConnectionCheckTimers[lineNumber].Stop();//연결 감시 타이머 중지
                WatchJobCheckTimers[lineNumber].Stop();//동작 감시 타이머 중지
                PLCs[lineNumber].FreeDeviceStatus();
                PLCs[lineNumber].Close();
                ConnectionStates[lineNumber] = false;
                UpdatePlcStatus(lineNumber, "연결 끊김", Colors.Gray);
                OperationStatus = "연결 끊김";  // 연결 해제 상태 업데이트
                MessageBox.Show($"라인 {lineNumber} PLC 연결 해제 완료");
            }
            catch (Exception ex)
            {
                OperationStatus = "연결 해제 오류";  // 해제 오류 상태 업데이트
                MessageBox.Show($"라인 {lineNumber} 연결 해제 중 오류 발생: {ex.Message}");
            }
        }
        public void InspectPlc(PlcStatusModel plc, int lineNumber)
        {
            OperationStatus = "점검 중";  // 점검 중 상태 업데이트
            UpdatePlcStatus(lineNumber, "점검 중", Colors.Cyan);

            // PLC 점검 로직 구현
            // 예시로 임시 메시지 출력
            MessageBox.Show($"라인 {lineNumber} PLC 점검 중...");

            // 점검 완료 후 상태 업데이트
            //UpdatePlcStatus(lineNumber, "점검 완료", Colors.Green);
            //OperationStatus = "점검 완료";  // 점검 완료 상태 업데이트
        }

        // 상태 업데이트 메서드
        private void UpdatePlcStatus(int lineNumber, string status, Color color)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var plc = PlcStatuses[lineNumber - 1];
                if (plc != null)
                {
                    plc.Status = status;
                    plc.StatusColor = new SolidColorBrush(color);
                }
            });
        }
        private void ShowPlcDetails(PlcStatusModel plc)
        {

            // 상세 정보를 처리하는 로직 추가
            MessageBox.Show($"PLC 상세 정보: {plc.Name} 상태는 {plc.Status}");
        }

        // 리소스 정리
        public void PLCCleanup()
        {
            // PLC 리소스 정리 코드
            foreach (var timer in ConnectionCheckTimers.Values)
            {
                timer.Stop();
            }

            foreach (var kvp in PLCs)
            {
                try
                {
                    kvp.Value.FreeDeviceStatus();
                    kvp.Value.Close();
                }
                catch { }
            }
        }

        // PropertyChanged 알림
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}