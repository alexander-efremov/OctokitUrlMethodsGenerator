using System;
using System.Net;

namespace OctokitUrlMethodsGenerator.Http
{
    public class UnknownWebException : Exception
    {
        #region Constructors

        public UnknownWebException(WebException innerException) : base(innerException.Message, innerException)
        {
        }

        #endregion
    }
}