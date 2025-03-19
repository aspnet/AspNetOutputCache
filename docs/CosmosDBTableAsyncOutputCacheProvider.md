# Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider

## Overview

`CosmosDBTableAsyncOutputCacheProvider` is an asynchronous output cache provider for ASP.NET that uses Azure Cosmos DB Table API as the backend storage. This allows for distributed caching across web applications hosted in different regions, providing high availability and low latency.

## Features

- Asynchronous operations for improved scalability via integration with [OutputCacheModuleAsync](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.OutputCacheModuleAsync/)
- Distributed caching support using Azure Cosmos DB Table API

## Requirements

- .NET Framework 4.6.2 or later (Full framework - not .NET Core)
- Azure Cosmos DB account with Table API

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
   - [Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider](https://www.nuget.org/packages/Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider/)

   This will add a reference to the necessary assemblies and add the following configuration into the `web.config` file.

   ```xml
   <system.web>
     <caching>
       <outputCache defaultProvider="CosmosDBTableAsyncOutputCacheProvider">
         <providers>
           <add name="CosmosDBTableAsyncOutputCacheProvider" connectionStringName="StorageConnectionStringForOutputCacheProvider" tableName="[TableName]"
                type="Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider.CosmosDBTableAsyncOutputCacheProvider, Microsoft.AspNet.OutputCache.CosmosDBTableAsyncOutputCacheProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35" />
         </providers>
       </outputCache>
     </caching>
   </system.web>
   ```

1. **Further Configuration**

   Be sure that `web.config` includes both the connection string and the `OutputCacheModuleAsync` configuration:

   ```xml
   <configuration>
     <appSettings>
       <add key="CosmosDBEndpoint" value="https://<your-account>.documents.azure.com:443/" />
       <add key="CosmosDBKey" value="<your-account-key>" />
       <add key="CosmosDBDatabase" value="OutputCache" />
       <add key="CosmosDBTable" value="CacheEntries" />
     </appSettings>
     
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

### v1.1.1

  - Dependency version updates.

### v1.1.0

  - Migrated to using `Azure.Data.Tables` SDK for Cosmos access.

### v1.0.0

  - Initial release.
