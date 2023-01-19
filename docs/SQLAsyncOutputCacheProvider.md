# How to use the SQLAsync OutputCache Provider
  1. Target your application to 4.6.2+.

        The OutputCacheProviderAsync interface was introduced in .NET Framework 4.6.2, therefore you need to target your application to .NET Framework 4.6.2 or above in order to use the Async OutputCache Module. Download the [.NET Framework 4.6.2 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=53321) if you do not have it installed yet and update your application’s web.config targetFrameworks attributes as demonstrated below:
```xml
<system.web>
  <compilation debug="true" targetFramework="4.6.2"/>
  <httpRuntime targetFramework="4.6.2"/>
</system.web>
```

  2. Add the [Microsoft.AspNet.OutputCache.OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/) NuGet package.
  3. Add the [Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider/) NuGet package.

        Use the NuGet package manager to install the Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider packages.  This will add a reference to Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.dll and add the following configuration into the web.config file.
```xml
<system.web>
  <caching>
    <outputCache defaultProvider="SQLAsyncOutputCacheProvider">
      <providers>
        <add name="SQLAsyncOutputCacheProvider" connectionStringName="DefaultConnection" UseInMemoryTable="[true|false]"
             type="Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.SQLAsyncOutputCacheProvider, Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"/>
      </providers>
    </outputCache>
  </caching>
</system.web>
```

1. *UseInMemoryTable* - Indicates whether to use Sql server 2016 In-Memory OLTP for the provider.

Now your applications will start using Async OutputCache Module with the SQLAsync OutputCache Provider.