using Microsoft.AspNetCore.Builder;

namespace PrerenderCore.io
{
    public static class PrerenderMiddlewareExtensions
    {
        public static IApplicationBuilder UseMyHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PrerenderMiddleware>();
        }
    }
}
