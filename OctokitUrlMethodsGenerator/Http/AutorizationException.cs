using System;

namespace OctokitUrlMethodsGenerator.Http
{
    public class AutorizationException : Exception
    {
        #region Constructors

        public AutorizationException()
        {
        }

        public AutorizationException(string message) : base(message)
        {
        }

        public AutorizationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        #endregion
    }
}