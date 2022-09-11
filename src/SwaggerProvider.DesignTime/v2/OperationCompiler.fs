namespace SwaggerProvider.Internal.v2.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.v2.Parser.Schema
open Swagger.Internal

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open SwaggerProvider.Internal
open Swagger

/// Object for compiling operations.
type OperationCompiler(schema: SwaggerObject, defCompiler: DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync: bool) =
    let compileOperation (methodName: string) (op: OperationObject) =
        if String.IsNullOrWhiteSpace methodName then
            failwithf $"Operation name could not be empty. See '%s{op.Path}/%A{op.Type}'"

        let parameters =
            /// handles deduping Swagger parameter names if the same parameter name
            /// appears in multiple locations in a given operation definition.
            let uniqueParamName existing (current: ParameterObject) =
                let name = niceCamelName current.Name

                if Set.contains name existing then
                    Set.add current.UnambiguousName existing, current.UnambiguousName
                else
                    Set.add name existing, name

            let required, optional = op.Parameters |> Array.partition(fun x -> x.Required)

            Array.append required optional
            |> Array.fold
                (fun (names, parameters) current ->
                    let names, paramName = uniqueParamName names current

                    let paramType =
                        defCompiler.CompileTy methodName paramName current.Type current.Required

                    let providedParam =
                        if current.Required then
                            ProvidedParameter(paramName, paramType)
                        else
                            let paramDefaultValue = defCompiler.GetDefaultValue paramType
                            ProvidedParameter(paramName, paramType, false, paramDefaultValue)

                    (names, providedParam :: parameters))
                (Set.empty, [])
            |> snd
            // because we built up our list in reverse order with the fold,
            // reverse it again so that all required properties come first
            |> List.rev

        // find the inner type value
        let retTy =
            let okResponse = // BUG :  wrong selector
                op.Responses
                |> Array.tryFind(fun (code, _) -> (code.IsSome && (code.Value = 200 || code.Value = 201)) || code.IsNone)

            match okResponse with
            | Some(_, resp) ->
                match resp.Schema with
                | None -> None
                | Some ty -> Some <| defCompiler.CompileTy methodName "Response" ty true
            | None -> None

        let overallReturnType =
            ProvidedTypeBuilder.MakeGenericType(
                (if asAsync then
                     typedefof<Async<unit>>
                 else
                     typedefof<System.Threading.Tasks.Task<unit>>),
                [ defaultArg retTy typeof<unit> ]
            )

        let m =
            ProvidedMethod(
                methodName,
                parameters,
                overallReturnType,
                invokeCode =
                    fun args ->
                        let this =
                            Expr.Coerce(args[0], typeof<ProvidedApiClientBase>)
                            |> Expr.Cast<ProvidedApiClientBase>

                        let httpMethod = op.Type.ToString()
                        let basePath = schema.BasePath

                        let headers =
                            let jsonConsumable =
                                op.Consumes |> Seq.exists(fun mt -> mt = MediaTypes.ApplicationJson)

                            let jsonProducible =
                                op.Produces |> Seq.exists(fun mt -> mt = MediaTypes.ApplicationJson)

                            <@
                                [|
                                    if jsonProducible then
                                        "Accept", MediaTypes.ApplicationJson
                                    if jsonConsumable then
                                        "Content-Type", MediaTypes.ApplicationJson
                                |]
                            @>

                        // Locates parameters matching the arguments
                        let parameters =
                            List.tail args // skip `this` param
                            |> List.map (function
                                | ShapeVar sVar as expr ->
                                    let param =
                                        op.Parameters
                                        |> Array.find(fun x ->
                                            // pain point: we have to make sure that the set of names we search for here are the same as the set of names generated when we make `parameters` above
                                            let baseName = niceCamelName x.Name
                                            baseName = sVar.Name || x.UnambiguousName = sVar.Name)

                                    param, expr
                                | _ -> failwithf $"Function '%s{methodName}' does not support functions as arguments.")

                        // Makes argument a string // TODO: Make body an exception
                        let coerceString defType (format: CollectionFormat) exp =
                            let obj = Expr.Coerce(exp, typeof<obj>) |> Expr.Cast<obj>
                            <@ let x = (%obj) in RuntimeHelpers.toParam x @>

                        let rec coerceQueryString name expr =
                            let obj = Expr.Coerce(expr, typeof<obj>)
                            <@ let o = (%%obj: obj) in RuntimeHelpers.toQueryParams name o (%this) @>

                        let replacePathTemplate (path: Expr<string>) (name: string) (value: Expr<string>) =
                            let pattern = $"{{%s{name}}}"
                            <@ Regex.Replace(%path, pattern, %value) @>

                        let addPayload load (param: ParameterObject) (exp: Expr) =
                            let name = param.Name
                            let var = coerceString param.Type param.CollectionFormat exp

                            match load with
                            | Some(FormData, b) -> Some(FormData, <@@ Seq.append %%b [ name, (%var: string) ] @@>)
                            | None ->
                                match param.In with
                                | Body -> Some(Body, Expr.Coerce(exp, typeof<obj>))
                                | _ -> Some(FormData, <@@ (seq [ name, (%var: string) ]) @@>)
                            | _ -> failwith("Can only contain one payload")

                        let addQuery (quer: Expr<(string * string) list>) name (exp: Expr) =
                            let listValues = coerceQueryString name exp
                            <@ List.append %quer %listValues @>

                        let addHeader (heads: Expr<(string * string)[]>) name (value: Expr<string>) =
                            <@ Array.append %heads [| name, %value |] @>

                        // Partitions arguments based on their locations
                        let path, payload, queries, heads =
                            let mPath = op.Path

                            parameters
                            |> List.fold
                                (fun (path, load, quer, head) (param: ParameterObject, exp) ->
                                    let name = param.Name

                                    match param.In with
                                    | Path ->
                                        let value = coerceString param.Type param.CollectionFormat exp
                                        (replacePathTemplate path name value, load, quer, head)
                                    | FormData
                                    | Body -> (path, addPayload load param exp, quer, head)
                                    | Query -> (path, load, addQuery quer name exp, head)
                                    | Header ->
                                        let value = coerceString param.Type param.CollectionFormat exp
                                        (path, load, quer, addHeader head name value))
                                (<@ mPath @>, None, <@ [] @>, headers)

                        let address = <@ RuntimeHelpers.combineUrl basePath %path @>

                        let innerReturnType = defaultArg retTy null

                        let httpRequestMessage =
                            <@ RuntimeHelpers.createHttpRequest httpMethod %address %queries @>

                        let httpRequestMessageWithPayload =
                            match payload with
                            | None -> httpRequestMessage
                            | Some(FormData, b) ->
                                <@
                                    let data = (%%b: seq<string * string>) |> Seq.map(fun (k, v) -> (k, box v))
                                    let content = RuntimeHelpers.toFormUrlEncodedContent data
                                    let msg = %httpRequestMessage
                                    msg.Content <- content
                                    msg
                                @>
                            | Some(Body, b) ->
                                <@
                                    let valueStr = (%this).Serialize(%%b: obj)
                                    let content = RuntimeHelpers.toStringContent(valueStr)
                                    let msg = %httpRequestMessage
                                    msg.Content <- content
                                    msg
                                @>
                            | Some(x, _) -> failwith("Payload should not be able to have type: " + string x)

                        let action =
                            <@
                                let msg = %httpRequestMessageWithPayload
                                RuntimeHelpers.fillHeaders msg %heads

                                task {
                                    let! response = (%this).HttpClient.SendAsync(msg)
                                    return response.EnsureSuccessStatusCode().Content
                                }
                            @>

                        let responseObj =
                            <@
                                let x = %action

                                task {
                                    let! response = x
                                    let! content = response.ReadAsStringAsync()
                                    return (%this).Deserialize(content, innerReturnType)
                                }
                            @>

                        let responseUnit =
                            <@
                                let x = %action

                                task {
                                    let! _ = x
                                    return ()
                                }
                            @>

                        let awaitTask t =
                            <@ Async.AwaitTask(%t) @>

                        // if we're an async method, then we can just return the above, coerced to the overallReturnType.
                        // if we're not async, then run that^ through Async.RunSynchronously before doing the coercion.
                        match asAsync, retTy with
                        | false, Some t -> Expr.Coerce(<@ RuntimeHelpers.taskCast t %responseObj @>, overallReturnType)
                        | false, None -> responseUnit.Raw
                        | true, Some t -> Expr.Coerce(<@ RuntimeHelpers.asyncCast t %(awaitTask responseObj) @>, overallReturnType)
                        | true, None -> (awaitTask responseUnit).Raw
            )

        if not <| String.IsNullOrEmpty(op.Summary) then
            m.AddXmlDoc(op.Summary) // TODO: Use description of parameters in docs

        if op.Deprecated then
            m.AddObsoleteAttribute("Operation is deprecated", false)

        m

    static member GetMethodNameCandidate (op: OperationObject) skipLength ignoreOperationId =
        if ignoreOperationId || String.IsNullOrWhiteSpace(op.OperationId) then
            let _, pathParts =
                (op.Path.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries), (false, []))
                ||> Array.foldBack(fun x (nextIsArg, pathParts) ->
                    if x.StartsWith("{") then
                        (true, pathParts)
                    else
                        (false, (if nextIsArg then singularize x else x) :: pathParts))

            String.Join("_", op.Type.ToString() :: pathParts)
        else
            op.OperationId.Substring(skipLength)
        |> nicePascalName

    member _.CompileProvidedClients(ns: NamespaceAbstraction) =
        let defaultHost =
            let protocol =
                match schema.Schemes with
                | [||] -> "http" // Should use the scheme used to access the Swagger definition itself.
                | array -> array[0]

            $"%s{protocol}://%s{schema.Host}"

        let baseTy = Some typeof<ProvidedApiClientBase>
        let baseCtor = baseTy.Value.GetConstructors().[0]

        List.ofArray schema.Paths
        |> List.groupBy(fun x ->
            if ignoreControllerPrefix then
                String.Empty //
            else
                let ind = x.OperationId.IndexOf("_")

                if ind <= 0 then
                    String.Empty
                else
                    x.OperationId.Substring(0, ind))
        |> List.iter(fun (clientName, operations) ->
            let tyName = ns.ReserveUniqueName clientName "Client"

            let ty =
                ProvidedTypeDefinition(tyName, baseTy, isErased = false, isSealed = false, hideObjectMethods = true)

            ns.RegisterType(tyName, ty)

            if not <| String.IsNullOrEmpty clientName then
                ty.AddXmlDoc $"Client for '%s{clientName}_*' operations"

            [
                ProvidedConstructor(
                    [
                        ProvidedParameter("httpClient", typeof<HttpClient>)
                        ProvidedParameter("options", typeof<JsonSerializerOptions>)
                    ],
                    invokeCode =
                        (fun args ->
                            match args with
                            | [] -> failwith "Generated constructors should always pass the instance as the first argument!"
                            | _ -> <@@ () @@>),
                    BaseConstructorCall = fun args -> (baseCtor, args)
                )
                ProvidedConstructor(
                    [ ProvidedParameter("httpClient", typeof<HttpClient>) ],
                    invokeCode =
                        (fun args ->
                            match args with
                            | [] -> failwith "Generated constructors should always pass the instance as the first argument!"
                            | _ -> <@@ () @@>),
                    BaseConstructorCall =
                        fun args ->
                            let args' = args @ [ <@@ null @@> ]
                            (baseCtor, args')
                )
                ProvidedConstructor(
                    [ ProvidedParameter("options", typeof<JsonSerializerOptions>) ],
                    invokeCode = (fun _ -> <@@ () @@>),
                    BaseConstructorCall =
                        fun args ->
                            let httpClient = <@ RuntimeHelpers.getDefaultHttpClient defaultHost @> :> Expr

                            let args' =
                                match args with
                                | [ instance; options ] -> [ instance; httpClient; options ]
                                | _ -> failwithf $"unexpected arguments received %A{args}"

                            (baseCtor, args')
                )
                ProvidedConstructor(
                    [],
                    invokeCode = (fun _ -> <@@ () @@>),
                    BaseConstructorCall =
                        fun args ->
                            let httpClient = <@ RuntimeHelpers.getDefaultHttpClient defaultHost @> :> Expr

                            let args' =
                                match args with
                                | [ instance ] -> [ instance; httpClient; <@@ null @@> ]
                                | _ -> failwithf $"unexpected arguments received %A{args}"

                            (baseCtor, args')
                )
            ]
            |> ty.AddMembers

            let methodNameScope = UniqueNameGenerator()

            operations
            |> List.map(fun op ->
                let skipLength =
                    if String.IsNullOrEmpty clientName then
                        0
                    else
                        clientName.Length + 1

                let name = OperationCompiler.GetMethodNameCandidate op skipLength ignoreOperationId
                compileOperation (methodNameScope.MakeUnique name) op)
            |> ty.AddMembers)
