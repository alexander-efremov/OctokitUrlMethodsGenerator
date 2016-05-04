using System;

namespace OctokitUrlMethodsGenerator.Http
{
    public class InternetConnectionException : Exception
    {
        #region Constructors

        public InternetConnectionException()
        {
        }

        public InternetConnectionException(string message) : base(message)
        {
        }

        public InternetConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}