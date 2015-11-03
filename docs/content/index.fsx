(*** hide ***)
// This block of code is omitted in the generated HTML documentation. Use
// it to define helpers that you do not want to show in the documentation.
#I "../../bin"

[<Literal>]
let filePath = __SOURCE_DIRECTORY__ + "../../../tests/SwaggerProvider.Tests/Schemas/PetStore.Swagger.json"
(**
SwaggerProvider
======================

Documentation

<div class="row">
  <div class="span1"></div>
  <div class="span6">
    <div class="well well-small" id="nuget">
      The SwaggerProvider library can be <a href="https://nuget.org/packages/SwaggerProvider">installed from NuGet</a>:
      <pre>PM> Install-Package SwaggerProvider</pre>
    </div>
  </div>
  <div class="span1"></div>
</div>

Example
-------

This example demonstrates using a function defined in this sample library.
First we generate the Swagger Provider. This can be done either by supplying a filepath or a URI. In either case
the optional argument Headers may also be used. Headers supplied here will be used in all REST calls.

*)

#r "SwaggerProvider/SwaggerProvider.dll"
open SwaggerProvider

type PetStore = SwaggerProvider<"http://petstore.swagger.io/v2/swagger.json">
type PetStore1 = SwaggerProvider<filePath>
type PetStore2 = SwaggerProvider<filePath, "Content-Type,application/json">

(**
![alt text](img/DefinitionInference.png "Intellisense for the Swagger Definitions")
![alt text](img/OperationsInference.png "Intellisense for the Swagger Operations")
![alt text](img/Instantiation.png "Intellisense for the Swagger Instantiations")
![alt text](img/Invocation.png "Intellisense for the Swagger Invocations")

Samples & documentation
-----------------------

The library comes with comprehensible documentation.
It can include tutorials automatically generated from `*.fsx` files in [the content folder][content].
The API reference is automatically generated from Markdown comments in the library implementation.

 * [Tutorial](tutorial.html) contains a further explanation of this sample library.

 * [API Reference](reference/index.html) contains automatically generated documentation for all types, modules
   and functions in the library. This includes additional brief samples on using most of the
   functions.

Contributing and copyright
--------------------------

The project is hosted on [GitHub][gh] where you can [report issues][issues], fork
the project and submit pull requests. If you're adding a new public API, please also
consider adding [samples][content] that can be turned into a documentation. You might
also want to read the [library design notes][readme] to understand how it works.

The library is available under Public Domain license, which allows modification and
redistribution for both commercial and non-commercial purposes. For more information see the
[License file][license] in the GitHub repository.

  [content]: https://github.com/fsprojects/SwaggerProvider/tree/master/docs/content
  [gh]: https://github.com/fsprojects/SwaggerProvider
  [issues]: https://github.com/fsprojects/SwaggerProvider/issues
  [readme]: https://github.com/fsprojects/SwaggerProvider/blob/master/README.md
  [license]: https://github.com/fsprojects/SwaggerProvider/blob/master/LICENSE.txt
*)
