namespace SwaggerProvider.Internal.v3.Compilers

open System
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.OpenApi.Models
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils

open SwaggerProvider.Internal
open Swagger
open Swagger.Internal

// We cannot use record here
// TP cannot load DTC with OpenApiPathItem/OperationType props (from 3rd party assembly)
// Probably related to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/274
type ApiCall = string * OpenApiPathItem * OperationType

type PayloadType =
    | NoBody
    | Body
    | FormData
    | FormUrlEncoded

    override x.ToString() =
        match x with
        | NoBody -> "noBody"
        | Body -> "body"
        | FormData -> "formData"
        | FormUrlEncoded -> "formUrlEncoded"

    static member Parse =
        function
        | "noBody" -> NoBody
        | "body" -> Body
        | "formData" -> FormData
        | "formUrlEncoded" -> FormUrlEncoded
        | name -> failwithf "Payload '%s' is not supported" name

/// Object for compiling operations.
type OperationCompiler(schema: OpenApiDocument, defCompiler: DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync: bool) =
    let compileOperation (providedMethodName: string) (apiCall: ApiCall) =
        let (path, pathItem, opTy) = apiCall
        let operation = pathItem.Operations.[opTy]

        if String.IsNullOrWhiteSpace providedMethodName then
            failwithf "Operation name could not be empty. See '%s/%A'" path opTy

        let unambiguousName(par: OpenApiParameter) =
            sprintf "%sIn%A" par.Name par.In

        let openApiParameters =
            seq {
                yield! pathItem.Parameters
                yield! operation.Parameters
            }
            |> Seq.toList

        let parameters =
            /// handles deduping Swagger parameter names if the same parameter name
            /// appears in multiple locations in a given operation definition.
            let uniqueParamName existing (current: OpenApiParameter) =
                let name = niceCamelName current.Name

                if Set.contains name existing then
                    let fqName = unambiguousName current
                    Set.add fqName existing, fqName
                else
                    Set.add name existing, name

            let (|ApplicationJson|_|)(requestBody: OpenApiRequestBody) =
                let bestKey =
                    requestBody.Content.Keys
                    |> Seq.tryFind(fun s -> s.StartsWith(MediaTypes.ApplicationJson, StringComparison.InvariantCultureIgnoreCase))
                    |> Option.defaultValue MediaTypes.ApplicationJson

                match requestBody.Content.TryGetValue bestKey with
                | true, mediaTyObj -> Some(mediaTyObj)
                | _ -> None

            let (|FormUrlEncodedContent|_|)(requestBody: OpenApiRequestBody) =
                match requestBody.Content.TryGetValue "application/x-www-form-urlencoded" with
                | true, mediaTyObj -> Some(mediaTyObj)
                | _ -> None

            let (|MultipartFormData|_|)(requestBody: OpenApiRequestBody) =
                match requestBody.Content.TryGetValue "multipart/form-data" with
                | true, mediaTyObj -> Some(mediaTyObj)
                | _ -> None

            let (|NoMediaType|_|)(requestBody: OpenApiRequestBody) =
                if requestBody.Content.Count = 0 then Some() else None

            let bodyParam =
                if isNull operation.RequestBody then
                    None
                else
                    let param (payloadType: PayloadType) schema =
                        OpenApiParameter(
                            In = Nullable<_>(), // In Body parameter indicator
                            Name = payloadType.ToString(),
                            Schema = schema,
                            Required = true //operation.RequestBody.Required
                        )
                        |> Some

                    match operation.RequestBody with
                    | ApplicationJson mediaTyObj -> param Body mediaTyObj.Schema
                    | MultipartFormData mediaTyObj -> param FormData mediaTyObj.Schema
                    | FormUrlEncodedContent mediaTyObj -> param FormUrlEncoded mediaTyObj.Schema
                    | NoMediaType ->
                        // Assume that server treat it as `applicationJson`
                        let defSchema = OpenApiSchema() // todo: we need to test it
                        param NoBody defSchema
                    // TODO: application/octet-stream
                    | _ ->
                        let keys = operation.RequestBody.Content.Keys |> String.concat ";"
                        failwithf "Operation '%s' does not contain supported media types [%A]" operation.OperationId keys

            let required, optional =
                seq {
                    yield! openApiParameters

                    if bodyParam.IsSome then
                        yield bodyParam.Value
                }
                |> Seq.distinctBy(fun op -> op.Name, op.In)
                |> Seq.toList
                |> List.partition(fun x -> x.Required)

            ((Set.empty, []), List.append required optional)
            ||> List.fold(fun (names, parameters) current ->
                let (names, paramName) = uniqueParamName names current

                let paramType =
                    defCompiler.CompileTy providedMethodName paramName current.Schema current.Required

                let providedParam =
                    if current.Required then
                        ProvidedParameter(paramName, paramType)
                    else
                        let paramDefaultValue = defCompiler.GetDefaultValue paramType
                        ProvidedParameter(paramName, paramType, false, paramDefaultValue)

                (names, providedParam :: parameters))
            |> snd
            // because we built up our list in reverse order with the fold,
            // reverse it again so that all required properties come first
            |> List.rev

        // find the inner type value
        let retTy =
            let okResponse = // BUG :  wrong selector
                operation.Responses
                |> Seq.tryFind(fun resp -> resp.Key = "200" || resp.Key = "201") // or default

            match okResponse with
            | Some(kv) ->
                // TODO: FTW media type ?
                match kv.Value.Content.TryGetValue MediaTypes.ApplicationJson with
                | true, mediaTy ->
                    if isNull mediaTy.Schema then
                        Some <| typeof<unit>
                    else
                        Some
                        <| defCompiler.CompileTy providedMethodName "Response" mediaTy.Schema true
                | false, _ ->
                    match kv.Value.Content.TryGetValue MediaTypes.ApplicationOctetStream with
                    | true, mediaTy -> // The only really expected type here is IO.Stream
                        if isNull mediaTy.Schema then
                            Some <| typeof<IO.Stream>
                        else
                            Some
                            <| defCompiler.CompileTy providedMethodName "Response" mediaTy.Schema true
                    | false, _ -> None
            | None -> None

        let overallReturnType =
            ProvidedTypeBuilder.MakeGenericType(
                (if asAsync then
                     typedefof<Async<unit>>
                 else
                     typedefof<System.Threading.Tasks.Task<unit>>),
                [ defaultArg retTy (typeof<unit>) ]
            )

        let (errorCodes, errorDescriptions) =
            operation.Responses
            |> Seq.choose(fun x ->
                let code = x.Key

                if code.StartsWith("2") then
                    None
                else
                    Option.ofObj x.Value.Description
                    |> Option.map(fun desc -> (code, desc)))
            |> Seq.toArray
            |> Array.unzip

        let m =
            ProvidedMethod(
                providedMethodName,
                parameters,
                overallReturnType,
                invokeCode =
                    fun args ->
                        let this =
                            Expr.Coerce(args.[0], typeof<ProvidedApiClientBase>)
                            |> Expr.Cast<ProvidedApiClientBase>

                        let httpMethod = opTy.ToString()

                        let headers =
                            let jsonConsumable = true // TODO: take a look at media types
                            // op.Consumes |> Seq.exists (fun mt -> mt="application/json")
                            <@
                                if jsonConsumable then
                                    [ "Content-Type", MediaTypes.ApplicationJson ]
                                else
                                    []
                            @>

                        // Locates parameters matching the arguments
                        let mutable payloadExp = None

                        let parameters =
                            List.tail args // skip `this` param
                            |> List.choose (function
                                | ShapeVar sVar as expr ->
                                    let param =
                                        openApiParameters
                                        |> Seq.tryFind(fun x ->
                                            // pain point: we have to make sure that the set of names we search for here are the same as the set of names generated when we make `parameters` above
                                            let baseName = niceCamelName x.Name
                                            baseName = sVar.Name || (unambiguousName x) = sVar.Name)

                                    match param with
                                    | Some(par) -> Some(par, expr)
                                    | _ ->
                                        let payloadType = PayloadType.Parse sVar.Name

                                        match payloadExp with
                                        | None ->
                                            payloadExp <- Some(payloadType, Expr.Coerce(expr, typeof<obj>))
                                            None
                                        | Some(_) ->
                                            failwithf
                                                "More than one payload parameter is specified: '%A' & '%A'"
                                                payloadType
                                                (payloadExp.Value |> fst)
                                | _ -> failwithf "Function '%s' does not support functions as arguments." providedMethodName)

                        // Makes argument a string // TODO: Make body an exception
                        let coerceString exp =
                            let obj = Expr.Coerce(exp, typeof<obj>) |> Expr.Cast<obj>
                            <@ let x = (%obj) in RuntimeHelpers.toParam x @>

                        let rec coerceQueryString name expr =
                            let obj = Expr.Coerce(expr, typeof<obj>)
                            <@ let o = (%%obj: obj) in RuntimeHelpers.toQueryParams name o (%this) @>

                        // Partitions arguments based on their locations
                        let (path, queryParams, headers) =
                            let (path, queryParams, headers, cookies) =
                                ((<@ path @>, <@ [] @>, headers, <@ [] @>), parameters)
                                ||> List.fold(fun (path, query, headers, cookies) (param: OpenApiParameter, valueExpr) ->
                                    if param.In.HasValue then
                                        let name = param.Name

                                        match param.In.Value with
                                        | ParameterLocation.Path ->
                                            let value = coerceString valueExpr
                                            let pattern = sprintf "{%s}" name
                                            let path' = <@ Regex.Replace(%path, pattern, %value) @>
                                            (path', query, headers, cookies)
                                        | ParameterLocation.Query ->
                                            let listValues = coerceQueryString name valueExpr
                                            let query' = <@ List.append %query %listValues @>
                                            (path, query', headers, cookies)
                                        | ParameterLocation.Header ->
                                            let value = coerceString valueExpr
                                            let headers' = <@ (name, %value) :: (%headers) @>
                                            (path, query, headers', cookies)
                                        | ParameterLocation.Cookie ->
                                            let value = coerceString valueExpr
                                            let cookies' = <@ (name, %value) :: (%cookies) @>
                                            (path, query, headers, cookies')
                                        | x -> failwithf "Unsupported parameter location '%A'" x
                                    else
                                        failwithf "This should not happen, payload expression is already parsed")

                            let headers' =
                                <@
                                    let cookieHeader =
                                        %cookies
                                        |> Seq.filter(snd >> isNull >> not)
                                        |> Seq.map(fun (name, value) -> String.Format("{0}={1}", name, value))
                                        |> String.concat ";"

                                    ("Cookie", cookieHeader) :: (%headers)
                                @>

                            (path, queryParams, headers')


                        let httpRequestMessage =
                            <@
                                let msg = RuntimeHelpers.createHttpRequest httpMethod %path %queryParams
                                RuntimeHelpers.fillHeaders msg %headers
                                msg
                            @>

                        let httpRequestMessageWithPayload =
                            match payloadExp with
                            | None -> httpRequestMessage
                            | Some(NoBody, _) -> httpRequestMessage
                            | Some(Body, body) ->
                                <@
                                    let valueStr = (%this).Serialize(%%body: obj)
                                    let content = RuntimeHelpers.toStringContent(valueStr)
                                    let msg = %httpRequestMessage
                                    msg.Content <- content
                                    msg
                                @>
                            | Some(FormData, formData) ->
                                <@
                                    let data = RuntimeHelpers.getPropertyValues(%%formData: obj)
                                    let content = RuntimeHelpers.toMultipartFormDataContent data
                                    let msg = %httpRequestMessage
                                    msg.Content <- content
                                    msg
                                @>
                            | Some(FormUrlEncoded, formUrlEncoded) ->
                                <@
                                    let data = RuntimeHelpers.getPropertyValues(%%formUrlEncoded: obj)
                                    let content = RuntimeHelpers.toFormUrlEncodedContent(data)
                                    let msg = %httpRequestMessage
                                    msg.Content <- content
                                    msg
                                @>

                        let action =
                            <@ (%this).CallAsync(%httpRequestMessageWithPayload, errorCodes, errorDescriptions) @>

                        let responseObj =
                            let innerReturnType = defaultArg retTy null

                            <@
                                let x = %action

                                task {
                                    let! response = x
                                    let! content = response.ReadAsStringAsync()
                                    return (%this).Deserialize(content, innerReturnType)
                                }
                            @>

                        let responseStream =
                            <@
                                let x = %action

                                task {
                                    let! response = x
                                    let! data = response.ReadAsStreamAsync()
                                    return data
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

                        // if we're an async method, then we can just return the above, coerced to the overallReturnType.
                        // if we're not async, then run that^ through Async.RunSynchronously before doing the coercion.
                        if not asAsync then
                            match retTy with
                            | None -> responseUnit.Raw
                            | Some t when t = typeof<IO.Stream> -> <@ %responseStream @>.Raw
                            | Some t -> Expr.Coerce(<@ RuntimeHelpers.taskCast t %responseObj @>, overallReturnType)
                        else
                            let awaitTask t =
                                <@ Async.AwaitTask(%t) @>

                            match retTy with
                            | None -> (awaitTask responseUnit).Raw
                            | Some t when t = typeof<IO.Stream> -> <@ %(awaitTask responseStream) @>.Raw
                            | Some t -> Expr.Coerce(<@ RuntimeHelpers.asyncCast t %(awaitTask responseObj) @>, overallReturnType)
            )

        if not <| String.IsNullOrEmpty(operation.Summary) then
            m.AddXmlDoc(operation.Summary) // TODO: Use description of parameters in docs

        if operation.Deprecated then
            m.AddObsoleteAttribute("Operation is deprecated", false)

        m

    static member GetMethodNameCandidate (apiCall: ApiCall) skipLength ignoreOperationId =
        let (path, pathItem, opTy) = apiCall
        let operation = pathItem.Operations.[opTy]

        if ignoreOperationId || String.IsNullOrWhiteSpace(operation.OperationId) then
            let (_, pathParts) =
                (path.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries), (false, []))
                ||> Array.foldBack(fun x (nextIsArg, pathParts) ->
                    if x.StartsWith("{") then
                        (true, pathParts)
                    else
                        (false, (if nextIsArg then singularize x else x) :: pathParts))

            String.Join("_", (opTy.ToString()) :: pathParts)
        else
            operation.OperationId.Substring(skipLength)
        |> nicePascalName

    member __.CompileProvidedClients(ns: NamespaceAbstraction) =
        let defaultHost =
            if schema.Servers.Count = 0 then
                null
            else
                schema.Servers.[0].Url

        let baseTy = Some typeof<ProvidedApiClientBase>
        let baseCtor = baseTy.Value.GetConstructors().[0]

        List.ofSeq schema.Paths
        |> List.collect(fun path ->
            List.ofSeq path.Value.Operations
            |> List.map(fun kv -> path.Key, path.Value, kv.Key))
        |> List.groupBy(fun (_, pathItem, opTy) ->
            if ignoreControllerPrefix then
                String.Empty //
            else
                let op = pathItem.Operations.[opTy]

                if isNull op.OperationId then
                    String.Empty
                else
                    let ind = op.OperationId.IndexOf("_")

                    if ind <= 0 then
                        String.Empty
                    else
                        op.OperationId.Substring(0, ind))
        |> List.iter(fun (clientName, operations) ->
            let tyName = ns.ReserveUniqueName clientName "Client"

            let ty =
                ProvidedTypeDefinition(tyName, baseTy, isErased = false, isSealed = false, hideObjectMethods = true)

            ns.RegisterType(tyName, ty)

            if not <| String.IsNullOrEmpty clientName then
                ty.AddXmlDoc(sprintf "Client for '%s_*' operations" clientName)

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
                    invokeCode = (fun args -> <@@ () @@>),
                    BaseConstructorCall =
                        fun args ->
                            let httpClient = <@ RuntimeHelpers.getDefaultHttpClient defaultHost @> :> Expr

                            let args' =
                                match args with
                                | [ instance; options ] -> [ instance; httpClient; options ]
                                | _ -> failwithf "unexpected arguments received %A" args

                            (baseCtor, args')
                )
                ProvidedConstructor(
                    [],
                    invokeCode = (fun args -> <@@ () @@>),
                    BaseConstructorCall =
                        fun args ->
                            let httpClient = <@ RuntimeHelpers.getDefaultHttpClient defaultHost @> :> Expr

                            let args' =
                                match args with
                                | [ instance ] -> [ instance; httpClient; <@@ null @@> ]
                                | _ -> failwithf "unexpected arguments received %A" args

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
