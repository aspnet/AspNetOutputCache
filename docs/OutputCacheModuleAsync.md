# How to use the Async OutputCache Module
  1. Target your application to 4.6.2+.

        The OutputCacheProviderAsync interface was introduced in .NET Framework 4.6.2, therefore you need to target your application to .NET Framework 4.6.2 or above in order to use the Async OutputCache Module. Download the [.NET Framework 4.6.2 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=53321) if you do not have it installed yet and update your application’s web.config targetFrameworks attributes as demonstrated below:
```xml
<system.web>
  <compilation debug="true" targetFramework="4.6.2"/>
  <httpRuntime targetFramework="4.6.2"/>
</system.web>
```

  2. Add the [Microsoft.AspNet.OutputCache.OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/) NuGet package.

        Use the NuGet package manager to install the Microsoft.AspNet.OutputCache.OutputCacheModuleAsync package.  This will add a reference to the Microsoft.AspNet.OutputCache.OutputCacheModuleAsync.dll and add the following configuration into the web.config file.
```xml
<system.webServer>
  <modules>
    <remove name="OutputCache"/>
    <add name="OutputCache" type="Microsoft.AspNet.OutputCache.OutputCacheModuleAsync, Microsoft.AspNet.OutputCache.OutputCacheModuleAsync" preCondition="integratedMode"/>
  </modules>
</system.webServer>
```

Now your applications will start using Async OutputCache Module. If no outputcacheprovider is specified in web.config, the module will use a default synchronous in-memory provider, with that you won’t get the async benefits. Please consider using one of the OutputCache providers that builds on this package, or [implement an async OutputCache Provider of your own](https://devblogs.microsoft.com/dotnet/introducing-the-asp-net-async-outputcache-module/#how-to-implement-an-async-outputcache-provider).
