using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace PrerenderCore.io
{
    public static class PrerenderMiddlewareWithParamsExtensions
    {
        public static IApplicationBuilder UsePrerenderMiddlewareWithParams(
        this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<PrerenderMiddleware>();
        }

        public static IApplicationBuilder UsePrerenderMiddlewareWithParams(
            this IApplicationBuilder builder, PrerenderOptions prerenderOptions)
        {
            return builder.UseMiddleware<PrerenderMiddleware>(
                new OptionsWrapper<PrerenderOptions>(prerenderOptions));
        }
    }
}
