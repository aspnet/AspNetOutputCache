## Introduction
OutputCacheModule is ASP.NET’s default handler for storing the generated output of pages, controls, and HTTP responses.  This content can then be reused when appropriate to improve performance. Prior to the .NET Framework 4.6.2, the OutputCache Module did not support async read/write to the storage. You can find more details on [this blog post](https://blogs.msdn.microsoft.com/webdev/2016/12/05/introducing-the-asp-net-async-outputcache-module/).

## How to build
1. Open a [VS developer command prompt](https://docs.microsoft.com/en-us/dotnet/framework/tools/developer-command-prompt-for-vs)
2. Run build.cmd. This will build Nuget package and run all the unit tests.
3. All the build artifacts will be under AspNetOutputCache\bin\Release\ folder.

## How to contribute
Information on contributing to this repo is in the [Contributing Guide](CONTRIBUTING.md).

## Settings of the module and providers

#### Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider

The settings of this provider is located in the following configuration section in web.config.
```
<caching>
    <outputCache defaultProvider="SQLAsyncOutputCacheProvider">
    <providers>
        <add name="SQLAsyncOutputCacheProvider" connectionStringName="DefaultConnection" UseInMemoryTable="[true|false]"
        type="Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider.SQLAsyncOutputCacheProvider, Microsoft.AspNet.OutputCache.SQLAsyncOutputCacheProvider, Version=1.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"/>
    </providers>
    </outputCache>
</caching>
```

1. *UseInMemoryTable* - Indicates whether to use Sql server 2016 In-Memory OLTP for the provider.