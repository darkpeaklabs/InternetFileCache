using System.Security.Cryptography;
using System.Text;

namespace DarkPeakLabs.Test
{
    public class InternetFileCacheTest : IDisposable
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<InternetFileCacheTest> _logger;
        private const string fileContent = "Hello Test!";
        private const string testUrl = "http://localhost:3000";
        private readonly string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "18e9f6d4-5996-478e-ad31-d4579beb55f7");

        public InternetFileCacheTest()
        {
            _loggerFactory = LoggerFactory.Create((builder) =>
            {
                builder
                    .AddDebug()
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Debug);
            });

            _logger = _loggerFactory.CreateLogger<InternetFileCacheTest>();
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

# pragma warning disable xUnit1031
        [Fact]
        public void TestSingleThread()
        {
            ClearCache();

            int downloadCount = 0;

            TestWithWebServer(
                testAction: () => 
                { 
                    InternetFileCache cache = new(_loggerFactory);
                    cache.GetStream(CreateOptions(), (stream) => TestStreamAsync(stream).GetAwaiter().GetResult());
                    cache.GetStream(CreateOptions(), (stream) => TestStreamAsync(stream).GetAwaiter().GetResult());
                },
                
                downloadAction: () => 
                {
                    downloadCount++;
                });

            Assert.Equal(1, downloadCount);
        }

        [Fact]
        public void TestFileUpdate()
        {
            ClearCache();

            int downloadCount = 0;

            TestWithWebServer(
                testAction: () => 
                { 
                    InternetFileCache cache = new(_loggerFactory);
                    var options = CreateOptions();
                    cache.GetStream(options, (stream) => TestStreamAsync(stream).GetAwaiter().GetResult());
                    Thread.Sleep(3000);
                    options.UpdateInterval = TimeSpan.FromSeconds(2);
                    cache.GetStream(options, (stream) => TestStreamAsync(stream).GetAwaiter().GetResult());
                },
                
                downloadAction: () => 
                {
                    downloadCount++;
                });

            Assert.Equal(2, downloadCount);
        }

        [Fact]
        public void TestMultiThread()
        {
            ClearCache();

            int threads = 10;
            int rounds = 10;
            int maxSleep = 2000;

            int downloadCount = 0;

            TestWithWebServer(
                testAction: () => 
                { 
                    var tasks = Enumerable.Range(1, threads).Select(x => TestThread(x, rounds, maxSleep));
                    Task.WhenAll(tasks).GetAwaiter().GetResult();
                },
                
                downloadAction: () => 
                {
                    downloadCount++;
                    _logger.LogInformation($"Current download count: {downloadCount}");
                });

            Assert.Equal(1, downloadCount);
        }
# pragma warning restore xUnit1031

        private void ClearCache()
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private async Task TestThread(int id, int rounds, int maxSleep)
        {
            InternetFileCache cache = new(_loggerFactory);

            for (int i = 0; i < rounds; i++)
            {
                await cache.GetStreamAsync(CreateOptions(), (stream) => TestStreamAsync(stream));
                var sleep = RandomNumberGenerator.GetInt32(100, maxSleep);
                _logger.LogInformation($"[{id}] Content obtained, round: {i}, sleep: {sleep}");
                await Task.Delay(maxSleep);
            }
        }

        private async Task TestStreamAsync(Stream stream)
        {
            using StreamReader reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            Assert.Equal(fileContent, content);
        }

        private InternetFileCacheOptions CreateOptions()
        {
            return new InternetFileCacheOptions()
            {
                Path = path,
                DownloadUrl = new Uri($"{testUrl}/download")
            };
        }

        void TestWithWebServer(Action testAction, Action downloadAction)
        {
            var app = WebApplication.Create();
            app.Urls.Add(testUrl);
            app.MapGet("/download", () =>
            {
                var mimeType = "text/plain";
                downloadAction.Invoke();
                return Results.File(Encoding.UTF8.GetBytes(fileContent), contentType: mimeType);
            });
            app.Start();

            try
            {
                testAction.Invoke();
            }
            finally
            {
                app.StopAsync().GetAwaiter().GetResult();
            }
        }
    }
}