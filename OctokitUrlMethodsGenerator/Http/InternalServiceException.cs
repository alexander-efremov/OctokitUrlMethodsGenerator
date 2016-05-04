using System;

namespace OctokitUrlMethodsGenerator.Http
{
    public class InternalServiceException : Exception
    {
        #region Constructors

        public InternalServiceException()
        {
        }

        public InternalServiceException(string message) : base(message)
        {
        }

        public InternalServiceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}