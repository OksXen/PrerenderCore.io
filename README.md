# PrerenderCore.io
PrerenderCore.io middleware for ASP.NET Core projects

Original: https://github.com/greengerong/Prerender_asp_mvc

1. Download the project. 
2. Build the project. 
3. Reference the project in your .NET Core Web app. 
4. Configure your appsettings.json. 
5. Configure your StartUp file. Code sample is below. 
6. Test it locally using Prerender Node Server. 

appsettings.json

{   
  "prerenderOptions": {
    "serviceUrl": "http://localhost:3000/",
    "blackList": [],
    "whiteList": [],
    "token": "1234567910",
    "proxy": {
      "url": "",
      "port": 80
    },
    "stripApplicationNameFromRequestUrl": false,
    "crawlerUserAgents": []
  }
}


Add the following in StartUP.cs.Configure(IApplicationBuilder app, IWebHostEnvironment env) function: 

var prerenderOptions = Configuration.GetSection("prerenderOptions").Get<PrerenderOptions>();
app.UsePrerenderMiddlewareWithParams(prerenderOptions);
  
That's it. 
Go here to find out how to test. https://github.com/greengerong/Prerender_asp_mvc





Test locally using Prerender node server on Windows. https://github.com/prerender/prerender
follow the steps here.

  
