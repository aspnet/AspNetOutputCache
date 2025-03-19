# Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider

## Overview

`SQLAsyncOutputCacheProvider` is an asynchronous SQL Server-based output cache provider for ASP.NET. It enables storing ASP.NET output cache data in a SQL Server database, allowing for distributed caching across web farm scenarios.

## Features

- Asynchronous database operations for improved scalability via integration with [OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/)
- Traditional or In-Memory OLTP tables for cache storage

## Requirements

- .NET Framework 4.6.2 or later (Full framework - not .NET Core)
- SQL Server 2014 (12.0) or later for In-Memory OLTP support

## Usage

1. **Target your application to .NET Framework 4.6.2 or later**

   The `OutputCacheProviderAsync` interface was introduced in .NET Framework 4.6.2, therefore you need to target your application to .NET Framework 4.6.2 or above in order to use the Async OutputCache Module. Download the [.NET Framework 4.6.2 Developer Pack](https://www.microsoft.com/en-us/download/details.aspx?id=53321) if you do not have it installed yet and update your application's `web.config` targetFramework attributes as demonstrated below:

   ```xml
   <system.web>
     <compilation debug="true" targetFramework="4.6.2"/>
     <httpRuntime targetFramework="4.6.2"/>
   </system.web>
   ```

1. **Add NuGet packages**

   Use the NuGet package manager to install:

    - [Microsoft.AspNet.OutputCache.OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/)
    - [Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider/)

   This will add a reference to the necessary assemblies and configuration similar to the following into the `web.config` file.

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

1. **Further Configuration**

   Be sure that `web.config` includes both the connection string and the `OutputCacheModuleAsync` configuration:

   ```xml
   <configuration>
     <connectionStrings>
       <add name="SQLOutputCache" 
            connectionString="Data Source=<myserver>;Initial Catalog=OutputCache;Integrated Security=True" 
            providerName="System.Data.SqlClient" />
     </connectionStrings>

     <system.webServer>
       <modules>
         <remove name="OutputCache" />
         <add name="OutputCache" 
              type="Microsoft.AspNet.OutputCache.OutputCacheModuleAsync, Microsoft.AspNet.OutputCache.OutputCacheModuleAsync" 
              preCondition="integratedMode" />
       </modules>
     </system.webServer>
   </configuration>
   ```

## Updates

### v1.0.1

  - Initial release.
