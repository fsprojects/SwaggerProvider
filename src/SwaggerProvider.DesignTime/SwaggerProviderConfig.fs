namespace SwaggerProvider

open System.Reflection
open ProviderImplementation.ProvidedTypes
open System
open FSharp.Data
open Swagger.Parser
open SwaggerProvider.Internal.Compilers

module private SwaggerProviderConfig =
    let NameSpace = "SwaggerProvider"

    let internal typedSwaggerProvider (ctx: ProvidedTypesContext) asmLocation =
        let asm = Assembly.LoadFrom asmLocation
        let swaggerProvider = ProvidedTypeDefinition(asm, NameSpace, "SwaggerProvider", Some typeof<obj>, isErased = false)
        
        let staticParam name ty doc (def: 'a Option) = 
            let p = 
                match def with 
                | Some d -> ProvidedStaticParameter(name, ty, d) 
                | None -> ProvidedStaticParameter(name, ty)
            p.AddXmlDoc(doc)
            p

        let staticParams =
            [ ProvidedStaticParameter("Schema", typeof<string>)
              ProvidedStaticParameter("Headers", typeof<string>, "")
              ProvidedStaticParameter("IgnoreOperationId", typeof<bool>, false)
              ProvidedStaticParameter("IgnoreControllerPrefix", typeof<bool>, true)
              ProvidedStaticParameter("ProvideNullable", typeof<bool>, false)
              ProvidedStaticParameter("PreferAsync", typeof<bool>, false)]

        //TODO: Add use operationID flag
        swaggerProvider.AddXmlDoc
            """<summary>Statically typed Swagger provider.</summary>
               <param name='Schema'>Url or Path to Swagger schema file.</param>
               <param name='Headers'>Headers that will be used to access the schema.</param>
               <param name='IgnoreOperationId'>IgnoreOperationId tells SwaggerProvider not to use `operationsId` and generate method names using `path` only. Default value `false`</param>
               <param name='IgnoreControllerPrefix'>IgnoreControllerPrefix tells SwaggerProvider not to parse `operationsId` as `<controllerName>_<methodName>` and generate one client class for all operations. Default value `true`</param>
               <param name='ProvideNullable'>Provide `Nullable<_>` for not required properties, instread of `Option<_>`</param>
               <param name='PreferAsync'>PreferAsync tells the SwaggerProvider to generate async actions of type `Async<'T>` instead of `Task<'T>`. Defaults to `false`</param>"""

        swaggerProvider.DefineStaticParameters(
            parameters=staticParams,
            instantiationFunction = (fun typeName args ->
                let tempAsm = ProvidedAssembly()
                let schemaPathRaw = args.[0] :?> string
                let ignoreOperationId = args.[2] :?> bool
                let ignoreControllerPrefix = args.[3] :?> bool
                let provideNullable = args.[4] :?> bool
                let asAsync = args.[5] :?> bool

                let schemaData =
                    match schemaPathRaw.StartsWith("http", true, null) with
                    | true  ->
                        let headersStr = args.[1] :?> string
                        let headers =
                            headersStr.Split('|')
                            |> Seq.choose (fun x ->
                                let pair = x.Split('=')
                                if (pair.Length = 2)
                                then Some (pair.[0],pair.[1])
                                else None
                            )
                        Http.RequestString(schemaPathRaw, headers=headers,
                            customizeHttpRequest = fun req ->
                                req.Credentials <- Net.CredentialCache.DefaultNetworkCredentials
                                req)
                    | false ->
                        schemaPathRaw |> IO.File.ReadAllText

                let schema = SwaggerParser.parseSchema schemaData

                // Create Swagger provider type
                let baseTy = Some typeof<obj>
                let ty = ProvidedTypeDefinition(tempAsm, NameSpace, typeName, baseTy, isErased = false, hideObjectMethods = true)
                ty.AddXmlDoc ("Swagger Provider for " + schema.Host)
                ty.AddMember <| ProvidedConstructor([], invokeCode = fun _ -> <@@ () @@>)

                let defCompiler = DefinitionCompiler(schema, provideNullable)
                let opCompiler = OperationCompiler(schema, defCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync)

                opCompiler.CompileProvidedClients(defCompiler.Namespace)
                ty.AddMembers <| defCompiler.Namespace.GetProvidedTypes() // Add all provided types

                tempAsm.AddTypes [ty]

                ty
            ))
        swaggerProvider


