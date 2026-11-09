namespace QuanLyThuVien.Services
{
    /// <summary>Chạy nền: định kỳ quét phiếu quá hạn và tự ghi tiền phạt.</summary>
    public class OverdueFeeWorker : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<OverdueFeeWorker> _log;

        public OverdueFeeWorker(IServiceScopeFactory scopes, ILogger<OverdueFeeWorker> log)
        {
            _scopes = scopes;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var fees = scope.ServiceProvider.GetRequiredService<LateFeeService>();
                    var changed = await fees.SyncOverdueAsync();
                    if (changed > 0) _log.LogInformation("Đã cập nhật {Count} khoản phạt quá hạn", changed);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Lỗi khi quét phiếu quá hạn");
                }

                try { await Task.Delay(Interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
