## Introduction
OutputCacheModule is ASP.NET’s default handler for storing the generated output of pages, controls, and HTTP responses.  This content can then be reused when appropriate to improve performance. Prior to the .NET Framework 4.6.2, the OutputCache Module did not support async read/write to the storage. You can find more details on [this blog post](https://blogs.msdn.microsoft.com/webdev/2016/12/05/introducing-the-asp-net-async-outputcache-module/).

## How to build
1. Open a [VS developer command prompt](https://docs.microsoft.com/en-us/dotnet/framework/tools/developer-command-prompt-for-vs)
2. Run build.cmd. This will build Nuget packages and run all the unit tests.
3. All the build artifacts will be under AspNetOutputCache\bin\Release\ folder.

## How to contribute
Information on contributing to this repo is in the [Contributing Guide](CONTRIBUTING.md).

## The Following Packages Are Built In This Repo
  * [OutputCacheModuleAsync](docs/OutputCacheModuleAsync.md)
  * [SQLAsyncOutputCacheProvider](docs/SQLAsyncOutputCacheProvider.md)
  * [CosmosDBTableAsyncOutputCacheProvider](docs/CosmosDBTableAsyncOutputCacheProvider.md)
