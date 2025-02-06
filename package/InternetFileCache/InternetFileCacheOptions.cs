using System;

namespace DarkPeakLabs
{
    public class InternetFileCacheOptions
    {
        public string Path { get; set; }

        public Uri DownloadUrl { get; set; }

        public TimeSpan? DownloadTimeout { get; set; }

        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromDays(1);

        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
