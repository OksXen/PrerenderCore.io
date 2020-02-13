using System.Collections.Generic;

namespace PrerenderCore.io
{
    public class PrerenderOptions
    {
        public PrerenderOptions()
        {
            ServiceUrl = "http://service.prerender.io/";
        }

        public string ServiceUrl { get; set; }
        public string Token { get; set; }

        public IEnumerable<string> Blacklist { get; set; }
        public IEnumerable<string> Whitelist { get; set; }

        public IEnumerable<string> ExtensionsToIgnore { get; set; }

        public PrerenderProxyOptions Proxy { get; set; }
        
        public bool StripApplicationNameFromRequestUrl { get; set; }

        public IEnumerable<string> CrawlerUserAgents { get; set; }
    }
}
