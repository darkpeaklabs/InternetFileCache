using System;

namespace DarkPeakLabs
{
    [Serializable]
    public class InternetFileCacheDownloadException : InternetFileCacheAcquireLockException
    {
        public InternetFileCacheDownloadException()
        {
        }

        public InternetFileCacheDownloadException(string message) : base(message)
        {
        }

        public InternetFileCacheDownloadException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}