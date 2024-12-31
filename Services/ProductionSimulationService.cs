using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Dapper;
using System.Threading;

namespace MES.Solution.Services
{
    public class ProductionSimulationService
    {
        private readonly string _connectionString;
        private bool _isSimulationRunning;
        private readonly object _lock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly InventoryChartService _inventoryService;


        public event EventHandler<ProductionEventArgs> ProductionUpdated;
        public event EventHandler<string> SimulationError;

        public ProductionSimulationService(string connectionString)
        {
            _connectionString = connectionString;
            _cancellationTokenSource = new CancellationTokenSource();
            _inventoryService = new InventoryChartService(connectionString);
        }

        public async Task StartSimulation(string workOrderCode)
        {
            lock (_lock)
            {
                if (_isSimulationRunning)
                    throw new InvalidOperationException("시뮬레이션이 이미 실행 중입니다.");
                _isSimulationRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            using (var conn = new MySqlConnection(_connectionString))
            {
                try
                {
                    await conn.OpenAsync();

                    var sql = @"
                    SELECT order_quantity, production_quantity 
                    FROM dt_production_plan 
                    WHERE work_order_code = @WorkOrderCode";

                    var planInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(sql,
                        new { WorkOrderCode = workOrderCode });

                    if (planInfo == null)
                        throw new Exception("작업지시를 찾을 수 없습니다.");

                    int targetQuantity = planInfo.order_quantity;
                    int currentQuantity = planInfo.production_quantity;

                    await UpdateWorkOrderStatus(conn, workOrderCode, "작업중", DateTime.Now);

                    while (_isSimulationRunning && currentQuantity < targetQuantity)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        await Task.Delay(1000, _cancellationTokenSource.Token);
                        currentQuantity += 1;

                        await UpdateProductionQuantity(conn, workOrderCode, currentQuantity);
                        OnProductionUpdated(new ProductionEventArgs(workOrderCode, currentQuantity, targetQuantity));

                        if (currentQuantity >= targetQuantity)
                        {
                            await CompleteSimulation(conn, workOrderCode);
                            break;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // 시뮬레이션이 취소됨
                    await UpdateWorkOrderStatus(conn, workOrderCode, "지연", null);
                }
                catch (Exception ex)
                {
                    OnSimulationError(ex.Message);
                    await UpdateWorkOrderStatus(conn, workOrderCode, "지연", null);
                    throw;
                }
                finally
                {
                    _isSimulationRunning = false;
                }
            }
        }

        public async Task PauseSimulation(string workOrderCode)
        {
            _isSimulationRunning = false;
            using (var conn = new MySqlConnection(_connectionString))
            {
                await UpdateWorkOrderStatus(conn, workOrderCode, "일시정지", null);
            }
        }

        public async Task ResumeSimulation(string workOrderCode)
        {
            await StartSimulation(workOrderCode);
        }

        public async Task StopSimulation(string workOrderCode)
        {
            _isSimulationRunning = false;
            _cancellationTokenSource.Cancel();

            using (var conn = new MySqlConnection(_connectionString))
            {
                await UpdateWorkOrderStatus(conn, workOrderCode, "지연", null);
            }
        }

        private async Task CompleteSimulation(MySqlConnection conn, string workOrderCode)
        {
            using (var transaction = conn.BeginTransaction())
            {
                try
                {
                    // 1. 작업지시 정보 및 제품 정보 조회
                    var sql = @"
                SELECT 
                    pp.product_code,
                    p.unit,
                    pp.production_quantity,
                    pp.employee_name
                FROM dt_production_plan pp
                JOIN dt_product p ON pp.product_code = p.product_code
                WHERE pp.work_order_code = @WorkOrderCode";

                    var productionInfo = await conn.QuerySingleAsync<dynamic>(sql,
                        new { WorkOrderCode = workOrderCode }, transaction);

                    // 2. 작업 완료 상태 업데이트
                    sql = @"
                UPDATE dt_production_plan 
                SET process_status = '완료',
                    completion_time = @CompletionTime
                WHERE work_order_code = @WorkOrderCode";

                    await conn.ExecuteAsync(sql, new
                    {
                        CompletionTime = DateTime.Now,
                        WorkOrderCode = workOrderCode
                    }, transaction);

                    // 3. 재고 증가 처리
                    await _inventoryService.UpdateStock(
                        productionInfo.product_code.ToString(),
                        (int)productionInfo.production_quantity,
                        "입고",
                        $"시뮬레이션 생산완료 (작업지시: {workOrderCode})"
                    );

                    // 4. 로그 기록
                    sql = @"
                INSERT INTO dt_user_activity_log 
                (user_id, action_type, action_detail, action_date)
                VALUES 
                (@UserId, @ActionType, @ActionDetail, @ActionDate)";

                    await conn.ExecuteAsync(sql, new
                    {
                        UserId = App.CurrentUser.UserId,
                        ActionType = "생산완료",
                        ActionDetail = $"작업번호: {workOrderCode}, 제품코드: {productionInfo.product_code}, 생산수량: {productionInfo.production_quantity}",
                        ActionDate = DateTime.Now
                    }, transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        private async Task UpdateProductionQuantity(MySqlConnection conn, string workOrderCode,
        int quantity)
        {
            var sql = @"
            UPDATE dt_production_plan 
            SET production_quantity = @Quantity
            WHERE work_order_code = @WorkOrderCode";

            await conn.ExecuteAsync(sql, new
            {
                Quantity = quantity,
                WorkOrderCode = workOrderCode
            });
        }

        private async Task UpdateWorkOrderStatus(MySqlConnection conn, string workOrderCode,
        string status, DateTime? startTime)
        {
            var sql = startTime.HasValue ?
                @"UPDATE dt_production_plan 
              SET process_status = @Status, start_time = @StartTime
              WHERE work_order_code = @WorkOrderCode" :
                @"UPDATE dt_production_plan 
              SET process_status = @Status
              WHERE work_order_code = @WorkOrderCode";

            var parameters = new DynamicParameters();
            parameters.Add("@Status", status);
            parameters.Add("@WorkOrderCode", workOrderCode);
            if (startTime.HasValue)
                parameters.Add("@StartTime", startTime.Value);

            await conn.ExecuteAsync(sql, parameters);
        }

        protected virtual void OnProductionUpdated(ProductionEventArgs e)
        {
            ProductionUpdated?.Invoke(this, e);
        }

        protected virtual void OnSimulationError(string message)
        {
            SimulationError?.Invoke(this, message);
        }
    }

    public class ProductionEventArgs : EventArgs
    {
        public string WorkOrderCode { get; }
        public int CurrentQuantity { get; }
        public int TargetQuantity { get; }

        public ProductionEventArgs(string workOrderCode, int currentQuantity, int targetQuantity)
        {
            WorkOrderCode = workOrderCode;
            CurrentQuantity = currentQuantity;
            TargetQuantity = targetQuantity;
        }
    }
}