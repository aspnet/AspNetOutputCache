# Microsoft.AspNet.OutputCache.OutputCacheModuleAsync

## Overview

`OutputCacheModuleAsync` is an asynchronous implementation of ASP.NET's (4.X) output caching module. It provides performance improvements for web applications by caching the output of HTTP responses to reduce the need to execute the same request processing multiple times.

## Features

- Asynchronous caching operations for better scalability
- Conditional request processing (If-Modified-Since, If-None-Match)
- Customizable cache expiration policies
- VaryBy parameter support (VaryByHeader, VaryByParam, VaryByCustom)

## Integrations

The `OutputCacheModuleAsync` is designed to work with any cache provider that implements the `OutputCacheProviderAsync` base class, allowing you to store cached content in various backends like SQL Server, Redis, or custom storage solutions. Two such providers are provided from the this same [repository](https://github.com/aspnet/AspNetOutputCache/):
  - [SQLAsyncOutputCacheProvider](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider/)
  - [CosmosDBTableAsyncOutputCacheProvider](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider/)

## Requirements

- .NET Framework 4.6.2 or later (Full framework - not .NET Core)

## Usage

1. **Target your application to .NET Framework 4.6.2 or later**
   
   The `OutputCacheProviderAsync` interface was introduced in .NET Framework 4.6.2, therefore you need to target your application to .NET Framework 4.6.2 or above in order to use the Async OutputCache Module. Download the [.NET Framework 4.6.2 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=53321) if you do not have it installed yet and update your application’s `web.config` targetFramework attributes as demonstrated below:

   ```xml
   <system.web>
     <compilation debug="true" targetFramework="4.6.2"/>
     <httpRuntime targetFramework="4.6.2"/>
   </system.web>
   ```

1. **Add the Microsoft.AspNet.OutputCache.OutputCacheModuleAsync NuGet package**

   Use the NuGet package manager to install the [Microsoft.AspNet.OutputCache.OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/) package. This will add a reference to the `Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.dll` and add the following configuration into the `web.config` file.

   ```xml
   <system.webServer>
     <modules>
       <remove name="OutputCache"/>
       <add name="OutputCache" type="Microsoft.AspNet.OutputCache.OutputCacheModuleAsync, Microsoft.AspNet.OutputCache.OutputCacheModuleAsync" preCondition="integratedMode"/>
     </modules>
   </system.webServer>
   ```

1. **Enable Output Caching in your application**

   Configure web forms applications for output caching by adding the following to the applications `web.config` file:

   ```xml
   <system.web>
     <caching>
       <outputCache enableOutputCache="true" />
       <outputCacheSettings>
         <outputCacheProfiles>
           <add name="CacheFor60Seconds" duration="60" varyByParam="none" />
         </outputCacheProfiles>
       </outputCacheSettings>
     </caching>
   </system.web>
   ```

   You can also enable output caching in your MVC applications by adding the `OutputCache` attribute to your controllers or actions. For example:
   ```csharp
   [OutputCache(Duration = 60, VaryByParam = "none")]
   public ActionResult Index()
   {
       // This content will be cached for 60 seconds
       return View();
   }
   ```
   Now the applications will start using the Async OutputCache Module.
   
   If there are special requirements of a cache store that are not met by these released providers, consider [implementing an async OutputCache Provider of your own](https://devblogs.microsoft.com/dotnet/introducing-the-asp-net-async-outputcache-module/#how-to-implement-an-async-outputcache-provider).

## Updates

### v1.0.4

  - Bug Fix: Fixed an issue with cache expiration policies not being applied correctly.

### v1.0.1

  - Added checks and support for kernel cache APIs in several places to ensure proper handling of kernel cache entries. Also updated the `DependencyRemovedCallback` method to invalidate kernel cache entries when dependencies change.
  - Added `[Serializable]` attributes to several classes to ensure they can be serialized correctly, which helps in caching scenarios.

### v1.0.0

  - Initial release.
