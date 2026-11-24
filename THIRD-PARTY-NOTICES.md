# Third-party notices

Navigation Harbour itself is licensed under the MIT License (see `LICENSE`).

The Windows x64 distribution also contains the third-party software listed below. Each component remains under its
own licence. The full licence texts are in the `bin/licenses/` folder of the distribution.

## Runtime (bundled, self-contained)

| Component | Licence | Notice |
|---|---|---|
| .NET runtime and libraries (`Microsoft.NETCore.App.Runtime.win-x64` 10.0.7) | MIT, Copyright (c) .NET Foundation and Contributors | `bin/licenses/dotnet-LICENSE.txt`, `bin/licenses/dotnet-runtime-THIRD-PARTY-NOTICES.txt` |
| ASP.NET Core runtime (`Microsoft.AspNetCore.App.Runtime.win-x64` 10.0.7) | MIT, Copyright (c) .NET Foundation and Contributors | `bin/licenses/dotnet-LICENSE.txt`, `bin/licenses/aspnetcore-runtime-THIRD-PARTY-NOTICES.txt` |

The two `THIRD-PARTY-NOTICES` files above are supplied by Microsoft and list the further components inside the .NET runtime.

## Backend NuGet packages

| Package | Version | Licence | Copyright |
|---|---|---|---|
| Microsoft.EntityFrameworkCore, .Relational, .Abstractions, .Sqlite, .Sqlite.Core | 10.0.7 | MIT | (c) Microsoft Corporation |
| Microsoft.Data.Sqlite.Core | 10.0.7 | MIT | (c) Microsoft Corporation |
| Microsoft.Extensions.DependencyModel | 10.0.7 | MIT | (c) Microsoft Corporation |
| Microsoft.OpenApi | 2.7.5 | MIT | (c) Microsoft Corporation |
| Swashbuckle.AspNetCore (Swagger, SwaggerGen, SwaggerUI) | 10.1.7 | MIT | Swashbuckle.AspNetCore contributors (domaindrivendev) |
| Serilog, Serilog.AspNetCore, .Extensions.Hosting, .Extensions.Logging, .Settings.Configuration, .Formatting.Compact, .Sinks.Console, .Sinks.Debug, .Sinks.File | 4.3.0 / 10.0.0 / 6.1.1 / 3.0.0 / 7.0.0 | Apache-2.0 | Copyright Serilog Contributors |
| SQLitePCLRaw.bundle_e_sqlite3, .core, .provider.e_sqlite3, .lib.e_sqlite3 | 2.1.13 | Apache-2.0 | Copyright 2014-2024 SourceGear, LLC |

- The native SQLite library (`e_sqlite3.dll`, from SQLitePCLRaw.lib.e_sqlite3) contains SQLite, which is in the public domain.
- Swashbuckle.AspNetCore.SwaggerUI embeds the Swagger UI web assets (Apache-2.0, Copyright SmartBear Software). In
  the standard Navigation Harbour configuration the Swagger pages are enabled only in the Development environment.
- The Apache-2.0 licence text is in `bin/licenses/Apache-2.0.txt`.
- The MIT licence text is the one in `bin/licenses/dotnet-LICENSE.txt` (Microsoft packages) and `bin/licenses/react-MIT.txt`.

## Frontend (compiled into `bin/wwwroot/assets`)

| Package | Licence | Copyright |
|---|---|---|
| react, react-dom, scheduler (19.x) | MIT | Copyright (c) Meta Platforms, Inc. and affiliates. |

Licence text: `bin/licenses/react-MIT.txt`. Build-time tools (Vite, TypeScript, ESLint and similar) are not part of the distribution.

## Fonts and images

The distribution contains no third-party fonts or images. `favicon.svg` and `icons.svg` are part of Navigation Harbour.
