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
