using System;

namespace DarkPeakLabs
{
    public class InternetFileCacheAcquireLockException : InternetFileCacheException
    {
        public InternetFileCacheAcquireLockException()
        {
        }

        public InternetFileCacheAcquireLockException(string message) : base(message)
        {
        }

        public InternetFileCacheAcquireLockException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}