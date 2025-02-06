using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DarkPeakLabs
{
    public class InternetFileCache
    {
        private readonly ILogger<InternetFileCache> _logger;
        private const string Folder = "d1748686-06b3-419d-9e9f-b99e92ae2eaa";
        private static readonly TimeSpan pollingInterval = TimeSpan.FromMilliseconds(300);
        private const int MaxAttempts = 3;

        public InternetFileCache()
        {
        }

        public InternetFileCache(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger<InternetFileCache>();
        }

        public void GetStream(InternetFileCacheOptions options, Action<Stream> action)
        {
            GetStreamAsync(
                options, 
                (stream) => 
                {
                    action.Invoke(stream);
                    return Task.CompletedTask;
                })
            .GetAwaiter().GetResult();
        }

        public async Task GetStreamAsync(InternetFileCacheOptions options, Func<Stream, Task> action)
        {
            _ = action ?? throw new ArgumentNullException(nameof(action));
            _ = options ?? throw new ArgumentNullException(nameof(options));

#if NETSTANDARD
            using HashAlgorithm sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(options.DownloadUrl.AbsolutePath));
#else
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(options.DownloadUrl.AbsolutePath));
#endif

            string path = Path.Combine(
                options.Path ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    Folder),
                $"{Convert.ToBase64String(hash)}.cache");

            using var fileStream = AcquireLock(path, options.LockTimeout);

            try
            {
                if (NeedsUpdate(fileStream, path, options.UpdateInterval))
                {
                    DownloadFile(options.DownloadUrl, fileStream, options.DownloadTimeout);
                    fileStream.Position = 0;
                }
                await action.Invoke(fileStream).ConfigureAwait(false);
            }
            finally
            {
                fileStream.Close();
                _logger?.LogFileLockReleased(path);
            }
        }

        private FileStream AcquireLock(string path, TimeSpan timeout)
        {
            CreateDirectoryIfNotExists(Path.GetDirectoryName(path));

            _logger?.LogAcquiringFileLock(path);
            TimeSpan waitTime = TimeSpan.Zero;
            do
            {
                if (TryAcquireFileLock(path, out var fileStream))
                {
                    _logger?.LogFileLockAcquired(path);
                    return fileStream;
                }
                Thread.Sleep(pollingInterval);
                waitTime += pollingInterval;
            }
            while (waitTime < timeout);

            throw new InternetFileCacheAcquireLockException($"Timeout acquiring file lock {path}");
        }

        private bool NeedsUpdate(FileStream fileStream, string path, TimeSpan updateInterval)
        {
            // zero length file indicates the file does not exist
            if (fileStream.Length == 0)
            {
                return true;
            }

            var lastWriteTime = File.GetLastWriteTimeUtc(path);
            _logger?.LogFoundExistingFile(path, fileStream.Length, lastWriteTime);

            return (DateTime.UtcNow - lastWriteTime) > updateInterval;
        }

        private void DownloadFile(Uri downloadUri, FileStream fileStream, TimeSpan? httpClientTimeout)
        {
            using var downloadStream = GetDownloadStream(downloadUri, httpClientTimeout);
            fileStream.Position = 0;
            downloadStream.CopyTo(fileStream);
        }

        private Stream GetDownloadStream(Uri downloadUri, TimeSpan? httpClientTimeout)
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Clear();
            var type = GetType();
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"{type.FullName}/{type.Assembly.GetName().Version}");

            if (httpClientTimeout.HasValue)
            {
                client.Timeout = httpClientTimeout.Value;
            }

            int attempt = 0;
            do
            {
                attempt++;
                _logger?.LogDownloadingDataFile(downloadUri, attempt);
                if (TryReadContentAsStream(client, downloadUri, out var stream, out var error))
                {
                    return stream;
                }

                if (attempt >= 3)
                {
                    _logger?.LogDataFileDownloadFailed(downloadUri, error.Message);
                    throw new InternetFileCacheDownloadException(error.Message, error);
                }

                Thread.Sleep(TimeSpan.FromSeconds(attempt));
            }
            while (true);
        }

        private static bool TryReadContentAsStream(HttpClient client, Uri downloadUri, out Stream stream, out Exception error)
        {
            try
            {
                var response = client.GetAsync(downloadUri).GetAwaiter().GetResult();
                stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                error = null;
                return true;
            }
            catch (HttpRequestException e)
            {
                stream = null;
                error = e;
                return false;
            }
            catch (TaskCanceledException e)
            {
                stream = null;
                error = e;
                return false;
            }
        }

        private static void CreateDirectoryIfNotExists(string path)
        {
            int attempt = 0;
            while(!Directory.Exists(path))
            {
                try
                {
                    attempt++;
                    Directory.CreateDirectory(path);
                }
                catch (IOException e)
                {
                    if (attempt >= MaxAttempts)
                    {
                        throw new InternetFileCacheAcquireLockException($"Unable to create folder {path}: {e.Message}", e);
                    }
                    Thread.Sleep(pollingInterval);
                }
            }
        }

        private static bool TryAcquireFileLock(string path, out FileStream fileStream)
        {
            try
            {
                fileStream = File.Open(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                fileStream = null;
                return false;
            }
        }
    }
}
