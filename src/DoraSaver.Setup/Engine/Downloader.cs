using System.Net;
using System.Net.Http;

namespace DoraSaver.Setup.Engine;

/// <summary>
/// Downloads files and only hands out bytes whose SHA-256 matches, verified in memory so the checked
/// data is exactly the data that gets used. Verified files are cached for reinstalls.
/// After repeated network failures the remaining downloads fail fast instead of each waiting minutes.
/// </summary>
internal sealed class Downloader : IDisposable
{
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10)];
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(60);
    private const int FailFastAfter = 2;

    private readonly HttpClient _http;
    private readonly string _cacheFolder;
    private int _consecutiveNetworkFailures;

    public Downloader(string cacheFolder)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        _cacheFolder = cacheFolder;
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.None })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DoraSaverSetup/1.0 (+https://github.com/zhehaosun717/dora-screensaver-revival)");
    }

    public async Task<byte[]> GetAsync(string url, string sha256, CancellationToken cancel)
    {
        string cached = Path.Combine(_cacheFolder, sha256 + ".bin");
        if (File.Exists(cached))
        {
            byte[] fromCache = File.ReadAllBytes(cached);
            if (Hash.Sha256(fromCache) == sha256)
            {
                return fromCache;
            }
        }

        int attempts = _consecutiveNetworkFailures >= FailFastAfter ? 1 : RetryDelays.Length + 1;
        Exception? last = null;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(RetryDelays[attempt - 1], cancel).ConfigureAwait(false);
            }

            try
            {
                byte[] data = await DownloadBytesAsync(url, cancel).ConfigureAwait(false);
                _consecutiveNetworkFailures = 0;
                string actual = Hash.Sha256(data);
                if (actual != sha256)
                {
                    // A different file will not become the right one by retrying.
                    throw new InvalidDataException($"SHA-256 mismatch for {url}: expected {sha256}, got {actual}");
                }

                TryWriteCache(cached, data);
                return data;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException && !cancel.IsCancellationRequested)
            {
                last = ex;
            }
        }

        _consecutiveNetworkFailures++;
        throw new IOException($"Download failed: {url} ({last?.Message})", last);
    }

    public void Dispose() => _http.Dispose();

    private async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancel)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        attempt.CancelAfter(AttemptTimeout);
        using HttpResponseMessage response = await _http.GetAsync(url, HttpCompletionOption.ResponseContentRead, attempt.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private void TryWriteCache(string path, byte[] data)
    {
        try
        {
            Directory.CreateDirectory(_cacheFolder);
            File.WriteAllBytes(path, data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The cache only saves a download next time.
        }
    }
}
