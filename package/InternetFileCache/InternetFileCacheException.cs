using System;

namespace DarkPeakLabs
{
    public class InternetFileCacheException : Exception
    {
        public InternetFileCacheException()
        {
        }

        public InternetFileCacheException(string message) : base(message)
        {
        }

        public InternetFileCacheException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}