using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace OctokitUrlMethodsGenerator.Http
{
    internal static class HttpHelper
    {
        public static Encoding GetEncoding(this HttpWebResponse webResponse)
        {
            var encoding = Encoding.UTF8;
            try
            {
                var contentType = webResponse.Headers["Content-Type"];
                if (contentType != null)
                {
                    const string charsetPrefix = "charset=";
                    var start = contentType.IndexOf(charsetPrefix, StringComparison.OrdinalIgnoreCase);
                    if (start != -1)
                    {
                        var name = contentType.Substring(start + charsetPrefix.Length);
                        if (name != null)
                            encoding = Encoding.GetEncoding(name);
                    }
                }
            }
            catch
            {
            }
            return encoding;
        }

        public static string DeserializeAsString(Stream stream, Encoding encoding)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (var sr = new StreamReader(stream, encoding))
                return sr.ReadToEnd();
        }
    }

    public abstract class PortableHttpClientBase
    {
        #region Methods

        protected abstract HttpWebRequest CreateRequest(Uri uri);

        private static string EncodeParameters(IDictionary<string, string> parameters, bool singleParamAsValue)
        {
            var paramsString = string.Empty;
            if (parameters != null && parameters.Count > 0)
                if (parameters.Count == 1 && singleParamAsValue)
                    paramsString = Uri.EscapeDataString(parameters.First().Value);
                else
                {
                    paramsString = parameters.Aggregate(string.Empty,
                        (current, pair) =>
                            current + Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value) + "&");
                    paramsString = paramsString.Remove(paramsString.Length - 1);
                }
            return paramsString;
        }

        private async Task<HttpWebResponse> InvokeAsync(string url, HttpMethod method, object data,
            CancellationToken cancellationToken)
        {
            var dictionary = data as IDictionary<string, string>;
            Uri uri;
            if (method == HttpMethod.Get || method == HttpMethod.Delete)
                uri = dictionary == null ? new Uri(url) : new Uri(url + "?" + EncodeParameters(dictionary, false));
            else
                uri = new Uri(url);

            var request = CreateRequest(uri);
            request.Method = method.ToString();
            if (method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                var asJson = dictionary == null;
                request.ContentType = asJson ? "application/json" : "application/x-www-form-urlencoded";
                var requestStreamTask = Task
                    .Factory
                    .FromAsync(request.BeginGetRequestStream, request.EndGetRequestStream, null);
                using (var requestStream = await requestStreamTask.ConfigureAwait(false))
                {
                    var dataString = asJson ? JsonConvert.SerializeObject(data) : EncodeParameters(dictionary, false);
                    var bytes = Encoding.UTF8.GetBytes(dataString);
                    requestStream.Write(bytes, 0, bytes.Length);
                }
            }
            try
            {
                var t = Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                if (cancellationToken.CanBeCanceled)
                    cancellationToken.Register(() => request.Abort());
                return (HttpWebResponse)await t.ConfigureAwait(false);
            }
            catch (WebException e)
            {
                var httpWebResponse = e.Response as HttpWebResponse;
                if (httpWebResponse != null)
                {
                    switch (httpWebResponse.StatusCode)
                    {
                        case HttpStatusCode.InternalServerError:
                            throw new InternalServiceException(e.Message, e);
                        case HttpStatusCode.BadGateway:
                            throw new InternetConnectionException(e.Message, e);
                        case HttpStatusCode.Unauthorized:
                            throw new AutorizationException(e.Message, e);
                    }
                }
                var status = e.Status.ToString();
                if ("NameResolutionFailure".Equals(status, StringComparison.OrdinalIgnoreCase))
                    throw new InternetConnectionException(e.Message, e);
                if ("Timeout".Equals(status, StringComparison.OrdinalIgnoreCase))
                    throw new RequestTimeoutException(e.Message, e);
                if ("ConnectFailure".Equals(status, StringComparison.OrdinalIgnoreCase))
                    throw new InternetConnectionException(e.Message, e);
                throw new UnknownWebException(e);
            }
        }

        #endregion

        #region Implementation of interfaces
        
        public async Task<string> GetAsync(Uri uri, HttpMethod method, object data = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var webResponse = await InvokeAsync(uri.OriginalString, method, data, cancellationToken).ConfigureAwait(false))
            using (var stream = webResponse.GetResponseStream())
            {
                var encoding = webResponse.GetEncoding();
                return HttpHelper.DeserializeAsString(stream, encoding);
            }
        }

        #endregion
    }

    internal class OctokitHttpClient : PortableHttpClientBase
    {
        private readonly string AppName = "octokit";

        private string GetAbsoluteUrl(Uri relativeUri)
        {
            var apiUrl = "https://api.github.com/";
            return new Uri(new Uri(apiUrl), relativeUri).OriginalString;
        }

        #region Overrides of PortableHttpClientBase

        protected override HttpWebRequest CreateRequest(Uri uri)
        {
            var httpWebRequest = WebRequest.CreateHttp(GetAbsoluteUrl(uri));
            httpWebRequest.Timeout = 30000;
            httpWebRequest.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
            httpWebRequest.UserAgent = AppName;
            return httpWebRequest;
        }

        #endregion
    }
}
