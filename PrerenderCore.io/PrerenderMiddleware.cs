using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PrerenderCore.io
{
    public class PrerenderMiddleware
    {
        private PrerenderOptions _prerenderOptions;
        private static readonly string _Escaped_Fragment = "_escaped_fragment_";
        private readonly RequestDelegate _next;

        public PrerenderMiddleware(RequestDelegate next, IOptions<PrerenderOptions> prerenderOptions)
        {
            _next = next;
            _prerenderOptions = prerenderOptions.Value;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                var httpContext = context;
                var request = httpContext.Request;
                var response = string.Empty;

                if (ShouldShowPrerenderedPage(request))
                {
                    var result = GetPrerenderedPageResponse(request);

                    context.Response.StatusCode = (int)result.StatusCode;

                    // The WebHeaderCollection is horrible, so we enumerate like this!
                    // We are adding the received headers from the prerender service
                    for (var i = 0; i < result.Headers.Count; ++i)
                    {
                        var header = result.Headers.GetKey(i);
                        var values = result.Headers.GetValues(i);

                        if (values == null) continue;

                        foreach (var value in values)
                        {
                            context.Response.Headers.Add(header, value);
                        }
                    }

                    response = result.ResponseBody;
                    await context.Response.WriteAsync(response);
                }

                
                await _next.Invoke(context);
            }
            catch (System.Exception exception)
            {
                Debug.WriteLine(exception.ToString());
            }
        }

     
        private ResponseResult GetPrerenderedPageResponse(HttpRequest request)
        {
            var apiUrl = GetApiUrl(request);
            var webRequest = (HttpWebRequest)WebRequest.Create(apiUrl);
            webRequest.Method = "GET";

            IHeaderDictionary headersDictionary = request.Headers;
            webRequest.UserAgent = headersDictionary[HeaderNames.UserAgent].ToString();
            
            webRequest.AllowAutoRedirect = false;
            SetProxy(webRequest);
            SetNoCache(webRequest);

            // Add our key!
            if (_prerenderOptions.Token.IsNotBlank())
            {
                webRequest.Headers.Add("X-Prerender-Token", _prerenderOptions.Token);
            }

            try
            {
                // Get the web response and read content etc. if successful
                var webResponse = (HttpWebResponse)webRequest.GetResponse();
                var reader = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8);
                return new ResponseResult(webResponse.StatusCode, reader.ReadToEnd(), webResponse.Headers);
            }
            catch (WebException e)
            {
                // Handle response WebExceptions for invalid renders (404s, 504s etc.) - but we still want the content
                var reader = new StreamReader(e.Response.GetResponseStream(), Encoding.UTF8);
                return new ResponseResult(((HttpWebResponse)e.Response).StatusCode, reader.ReadToEnd(), e.Response.Headers);
            }
        }

        private void SetProxy(HttpWebRequest webRequest)
        {
            if (_prerenderOptions.Proxy != null && _prerenderOptions.Proxy.Url.IsNotBlank())
            {
                webRequest.Proxy = new WebProxy(_prerenderOptions.Proxy.Url, _prerenderOptions.Proxy.Port);
            }
        }

        private static void SetNoCache(HttpWebRequest webRequest)
        {
            webRequest.Headers.Add("Cache-Control", "no-cache");
            webRequest.ContentType = "text/html";
        }

        private string GetApiUrl(HttpRequest request)
        {
            var url = request.GetDisplayUrl();

            // url have the _escaped_fragment_ query string
            // Prerender server remove it before making a request, but caching plugins happen before prerender server remove it
            url = RemoveQueryStringByKey(url, "_escaped_fragment_");

            // Correct for HTTPS if that is what the request arrived at the load balancer as 
            // (AWS and some other load balancers hide the HTTPS from us as we terminate SSL at the load balancer!)
            if (string.Equals(request.Headers["X-Forwarded-Proto"], "https", StringComparison.InvariantCultureIgnoreCase))
            {
                url = url.Replace("http://", "https://");
            }

            var prerenderServiceUrl = _prerenderOptions.ServiceUrl;
            return prerenderServiceUrl.EndsWith("/")
                ? (prerenderServiceUrl + url)
                : string.Format("{0}/{1}", prerenderServiceUrl, url);
        }

        public static string RemoveQueryStringByKey(string url, string key)
        {
            var uri = new Uri(url);

            // this gets all the query string key value pairs as a collection
            var newQueryString = HttpUtility.ParseQueryString(uri.Query);

            // this removes the key if exists
            newQueryString.Remove(key);

            // this gets the page path from root without QueryString
            string pagePathWithoutQueryString = uri.GetLeftPart(UriPartial.Path);

            return newQueryString.Count > 0
                ? String.Format("{0}?{1}", pagePathWithoutQueryString, newQueryString)
                : pagePathWithoutQueryString;
        }


        private bool ShouldShowPrerenderedPage(HttpRequest request)
        {
            IHeaderDictionary headersDictionary = request.Headers;
            var userAgent = headersDictionary[HeaderNames.UserAgent].ToString();
            var url = request.GetDisplayUrl();
            string urlReferrer = headersDictionary[HeaderNames.Referer].ToString();           

            var blacklist = _prerenderOptions.Blacklist;
            if (blacklist != null && IsInBlackList(url, urlReferrer, blacklist))
            {
                return false;
            }

            var whiteList = _prerenderOptions.Whitelist;
            if (whiteList != null && !IsInWhiteList(url, whiteList))
            {
                return false;
            }

            if (HasEscapedFragment(request))
            {
                return true;
            }
            if (userAgent.IsBlank())
            {
                return false;
            }

            if (!IsInSearchUserAgent(userAgent))
            {
                return false;
            }
            if (IsInResources(url))
            {
                return false;
            }
            return true;

        }

        private bool IsInBlackList(string url, string referer, IEnumerable<string> blacklist)
        {
            if (blacklist == null) return false;

            return blacklist.Any(item =>
            {
                var regex = new Regex(item);
                return regex.IsMatch(url) || (referer.IsNotBlank() && regex.IsMatch(referer));
            });
        }

        private bool IsInWhiteList(string url, IEnumerable<string> whiteList)
        {
            if (whiteList == null) return false;

            return whiteList.Any(item => new Regex(item).IsMatch(url));
        }

        private bool IsInResources(string url)
        {
            var extensionsToIgnore = GetExtensionsToIgnore();
            return extensionsToIgnore.Any(item => url.ToLower().Contains(item.ToLower()));
        }

        private IEnumerable<String> GetExtensionsToIgnore()
        {
            var extensionsToIgnore = new List<string>(new[]{".js", ".css", ".less", ".png", ".jpg", ".jpeg",
                ".gif", ".pdf", ".doc", ".txt", ".zip", ".mp3", ".rar", ".exe", ".wmv", ".doc", ".avi", ".ppt", ".mpg",
                ".mpeg", ".tif", ".wav", ".mov", ".psd", ".ai", ".xls", ".mp4", ".m4a", ".swf", ".dat", ".dmg",
                ".iso", ".flv", ".m4v", ".torrent"});
            if (_prerenderOptions.ExtensionsToIgnore.IsNotEmpty())
            {
                extensionsToIgnore.AddRange(_prerenderOptions.ExtensionsToIgnore);
            }
            return extensionsToIgnore;
        }

        private bool IsInSearchUserAgent(string useAgent)
        {
            var crawlerUserAgents = GetCrawlerUserAgents();

            // We need to see if the user agent actually contains any of the partial user agents we have!
            // THE ORIGINAL version compared for an exact match...!
            return
                (crawlerUserAgents.Any(
                    crawlerUserAgent =>
                    useAgent.IndexOf(crawlerUserAgent, StringComparison.InvariantCultureIgnoreCase) >= 0));
        }

        private IEnumerable<String> GetCrawlerUserAgents()
        {
            var crawlerUserAgents = new List<string>(new[]
                {
                    "googlebot", "yahoo", "bingbot", "yandex", "baiduspider", "facebookexternalhit", "twitterbot", "rogerbot", "linkedinbot",
                    "embedly", "quora link preview", "showyoubot", "outbrain", "pinterest/0.",
                    "developers.google.com/+/web/snippet", "slackbot", "vkShare", "W3C_Validator",
                    "redditbot", "Applebot", "WhatsApp", "flipboard", "tumblr", "bitlybot",
                    "SkypeUriPreview", "nuzzel", "Discordbot", "Google Page Speed", "x-bufferbot"
                });

            if (_prerenderOptions.CrawlerUserAgents != null)
            {
                crawlerUserAgents.AddRange(_prerenderOptions.CrawlerUserAgents);
            }
            return crawlerUserAgents;
        }

        private bool HasEscapedFragment(HttpRequest request)
        {
            return request.Query.ContainsKey(_Escaped_Fragment);            
        }
    }
}
