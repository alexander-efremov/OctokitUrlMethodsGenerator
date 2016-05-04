using System;

namespace OctokitUrlMethodsGenerator.Http
{
    public class RequestTimeoutException : Exception
    {
        #region Constructors

        public RequestTimeoutException()
        {
        }

        public RequestTimeoutException(string message) : base(message)
        {
        }

        public RequestTimeoutException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}