#### 0.10.0 - December 8, 2019
- OpenApiClientProvider
- .NET Core 3.1 support

#### 0.10.0-beta13 - December 8, 2019
- UUID/Guid support [#138](https://github.com/fsprojects/SwaggerProvider/issues/138)

#### 0.10.0-beta12 - November 19, 2019
- Fixed null reference for schemas without Components definitions

#### 0.10.0-beta11 - November 10, 2019
- Delete method incorrectly cased [#120](https://github.com/fsprojects/SwaggerProvider/issues/120)
- File handling - upload/download files as `IO.Stream` [#131](https://github.com/fsprojects/SwaggerProvider/pull/131)
- DateTimeOffset as a default `date-time` type [#133](https://github.com/fsprojects/SwaggerProvider/pull/133)
- Custom OpenApiException instead of HttpRequestException [134](https://github.com/fsprojects/SwaggerProvider/pull/134)

#### 0.10.0-beta10 - November 6, 2019
- Support REST convention with pluralized entities in mode with IgnoreOperationId=true [#121](https://github.com/fsprojects/SwaggerProvider/pull/121)
- Fixed null refs in optional headers [#123](https://github.com/fsprojects/SwaggerProvider/pull/123)

#### 0.10.0-beta09 - November 5, 2019
- TP parameter names are unified [#129](https://github.com/fsprojects/SwaggerProvider/pull/129)

#### 0.10.0-beta08 - November 3, 2019
- Migration to new Type Provider SDK [#88](https://github.com/fsprojects/SwaggerProvider/pull/88) [#125](https://github.com/fsprojects/SwaggerProvider/pull/125)
- SwaggerParser moved to runtime assembly (and exposed as API to library users) - [#90](https://github.com/fsprojects/SwaggerProvider/pull/90) 
- Added runtime dependency on YamlDotNet
- Removed dependency on FSharp.Data/JsonValue (replaced by JSON.NET)
- Added full-init constructor for provided types [#91](https://github.com/fsprojects/SwaggerProvider/pull/91)
- Option/Nullable types for not required properties [#93](https://github.com/fsprojects/SwaggerProvider/pull/93)
- `Http.fs` is moved to `SwaggerProvider.Runtime.dll` and `nicePascalName` applied to provided types, because FSI cannot load types with special characters in name (like `[` and `]`) - fix for [#94](https://github.com/fsprojects/SwaggerProvider/issues/94)
- Provide namespaces as nested types [#96](https://github.com/fsprojects/SwaggerProvider/issues/96)
- Added ability to generate multiple clients (one per controller) This feature is turned off by default and is controlled by `IgnoreControllerPrefix` parameter [#45](https://github.com/fsprojects/SwaggerProvider/issues/45#issuecomment-240554229)
- Supported parsing of schemas with inheritance in types defined inside operation parameters
- Added Async & Task-based calls for the Operations [#21](https://github.com/fsprojects/SwaggerProvider/issues/21)
- Do not reference design-time assembly when install from nuget [#104](https://github.com/fsprojects/SwaggerProvider/pull/104)
- Migration to `HttpClient`-based communication [#105](https://github.com/fsprojects/SwaggerProvider/pull/105) [#100](https://github.com/fsprojects/SwaggerProvider/issues/100)
- Fixed null reference in query params [#126](https://github.com/fsprojects/SwaggerProvider/issues/126)
- OpenApiClientProvider [#117](https://github.com/fsprojects/SwaggerProvider/pull/117)

#### 0.8.2 - January 19, 2017
* Fix probing locations [#97](https://github.com/fsprojects/SwaggerProvider/pull/97)

#### 0.8.1 - August 21, 2017
* Removed caching and dependency on FSharp.Configuration context [#84](https://github.com/fsprojects/SwaggerProvider/issues/84)

#### 0.8.0 - August 8, 2017
* Better support for optional parameters [#82](https://github.com/fsprojects/SwaggerProvider/issues/82)

#### 0.7.1 - June 1, 2017
* Newtonsoft.Json v10.0.2

#### 0.7.0 - May 26, 2017
* Supported Mono 5.0.1.1

#### 0.6.1 - April 15, 2017
* `ToString` is overridden for each generated type [#52](https://github.com/fsprojects/SwaggerProvider/issues/52)
* Removed reference from `Swagger.Runtime.dll` to `YamlDotNet.dll`

#### 0.6.0 - April 13, 2017
* Supported `allOf` composition with `properties` definition in the same SchemaObject - https://github.com/fsprojects/SwaggerProvider/issues/72
* Supported wrappers around primitive types - https://github.com/APIs-guru/openapi-directory/issues/98
* No runtime dependency on YamlDotNet
* NuGet dependency on FSharp.Core

#### 0.5.7 - March 12, 2017
- Improved URL construction [#66](https://github.com/fsprojects/SwaggerProvider/pull/66)

#### 0.5.6 - August 31, 2016
- Added NTLM auth for schema request [#50](https://github.com/fsprojects/SwaggerProvider/issues/50)

#### 0.5.5 - August 20, 2016
- Allow to configure protocol together with host name [#41](https://github.com/fsprojects/SwaggerProvider/issues/41)

#### 0.5.4 - August 19, 2016
- FIXED: SwaggerProvider and byte array [#46](https://github.com/fsprojects/SwaggerProvider/issues/46)

#### 0.5.3 - July 10, 2016
- Supported Newtonsoft.Json v9.0.1
- FIXED: props and fields name collision during quotes compilation [#38](https://github.com/fsprojects/SwaggerProvider/pull/38)

#### 0.5.2 - June 23, 2016
- FIXED: 201 status codes should be used as a return type for operations [#34](https://github.com/fsprojects/SwaggerProvider/issues/34)

#### 0.5.1 - April 30, 2016
- FIXED: Collisions in provided type names [#27](https://github.com/fsprojects/SwaggerProvider/issues/27)

#### 0.5.0 - April 19, 2016
- BREAKING CHANGE: Instance methods for provided operations with configurable `Host`, `Headers` and `modifiable web requests`
- Configurable operation name (`IgnoreOperationId` parameter)
- Support of unordered type definitions in schema (for Azure APIs)
- Allow for custom headers per-request [#22](https://github.com/fsprojects/SwaggerProvider/issues/22)
- Migration to `FsUnitTyped` + better testing

#### 0.4.0 - April 10, 2016
- Added support of anonymous types generations - https://github.com/fsprojects/SwaggerProvider/pull/24
- Added support of recursively dependent type definitions
- Added support of `$refs` in DefinitionProperty - https://github.com/fsprojects/SwaggerProvider/issues/23
- Added support of operations without `operationId`
- Better XML docs

#### 0.3.6 - April 2 2016
* Updated JSON.NET version up to v8.0.3
* Added support of model composition in path's response schema
* Added support of composite types like ("type": [ "string", "null" ])

#### 0.3.5 - February 25 2016
* Added ability to override Host property at runtime - https://github.com/fsprojects/SwaggerProvider/issues/15

#### 0.3.4 - January 20 2016
* Fixed generation of obsolete provided methods - https://github.com/fsprojects/FSharp.TypeProviders.StarterPack/issues/70

#### 0.3.3 - January 18 2016
* Fixed code generation for PetStore schema (Removed deprecated attributes from methods)
* Updated JSON.NET version up to v8.0.2

#### 0.3.2 - December 23 2015
* Migration to .NET 4.5

#### 0.3.1 - December 23 2015
* Fixed docs and bug in `SwaggerProvider.fsx`

#### 0.3.0 - December 22 2015
* Added support of schemes in YAML format

#### 0.2.0 - December 13 2015
* `AssemblyResolve` handler that resolve location of 3rd party dependencies
* Added dependencies on `Newtonsoft.Json` NuGet package

#### 0.1.3-beta - December 7 2015
* Bug fixes

#### 0.1.2-beta - November 22 2015
* Fixed bug in the query builder for POST and PUT requests
* Added tests for PUT & DELETE requests

#### 0.1.1-beta - November 19 2015
* Supported serialization of basic data types for passing in query
* Fixed bugs in query builder logic
* Added support of nice names for provided parameters
* Added support of JSON serialization for properties with nice names
* Auto Content-Type:application/json header to POST queries when it is supported
* Fixed bug in float compilation
* Added communication tests for data transferring to the server

#### 0.1.0-beta - November 17 2015
* Improved speed: Added caching for generated types
* Improved support of Swashbuckle generated schemas
* Fixed bug in compilation to IL
* Fixed bug in POST calls (Content-Length is set to 0)
* Fixed bug in definition type names beatification
* Fixed type coerce bug in provided methods
* Fixed NuGet package
* Added Swashbuckle.OWIN.API Server with REST API and communication/deserialization tests

#### 0.0.5-alpha - November 16 2015
* Added support of object composition

#### 0.0.4-alpha - November 13 2015
* Added support of Dictionaries
* Added tests for all samples from Swagger specification

#### 0.0.3-alpha - November 12 2015
* Implemented new Swagger JSON schema parser
* Added tests for parsing 200+ real-world Swagger schemas

#### 0.0.2-alpha - November 03 2015 (Delegate)
* Added instantiation of Swagger Definitions
* Added invocation of Swagger Operations
* Added global HTTP header option to the Swagger Provider constructor

#### 0.0.1-alpha - April 20 2015 (Sergey Tihon)
* Added Swagger Definition and Operations compilation
* Initial release
