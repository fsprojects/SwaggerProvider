namespace SwaggerProvider.Internal.Compilers

open System
open System.Collections.Generic
open System.Net.Http
open System.Reflection
open System.Text.Json

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.FSharp.Quotations.Patterns
open Microsoft.OpenApi
open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils

open SwaggerProvider.Internal
open Swagger
open Swagger.Internal

// We cannot use record here
// TP cannot load DTC with OpenApiPathItem/OperationType props (from 3rd party assembly)
// Probably related to https://github.com/fsprojects/FSharp.TypeProviders.SDK/issues/274
type ApiCall = string * IOpenApiPathItem * HttpMethod

[<Struct>]
type PayloadType =
    | NoData
    | AppJson
    | AppOctetStream
    | AppFormUrlEncoded
    | MultipartFormData
    | TextPlain

    override x.ToString() =
        match x with
        | NoData -> "noData"
        | AppJson -> "json"
        | AppOctetStream -> "octetStream"
        | AppFormUrlEncoded -> "formUrlEncoded"
        | MultipartFormData -> "formData"
        | TextPlain -> "textPlain"

    member x.ToMediaType() =
        match x with
        | NoData -> null
        | AppJson -> MediaTypes.ApplicationJson
        | AppOctetStream -> MediaTypes.ApplicationOctetStream
        | AppFormUrlEncoded -> MediaTypes.ApplicationFormUrlEncoded
        | MultipartFormData -> MediaTypes.MultipartFormData
        | TextPlain -> MediaTypes.TextPlain

    static member Parse =
        function
        | "noData" -> NoData
        | "json" -> AppJson
        | "octetStream" -> AppOctetStream
        | "formUrlEncoded" -> AppFormUrlEncoded
        | "formData" -> MultipartFormData
        | "textPlain" -> TextPlain
        | name -> failwithf $"Payload '%s{name}' is not supported"

/// Object for compiling operations.
type OperationCompiler(schema: OpenApiDocument, defCompiler: DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync: bool) =
    let toParamMethod =
        match <@@ RuntimeHelpers.toParam(null) @@> with
        | Call(None, m, _) -> m
        | _ -> failwith "Cannot extract toParam MethodInfo"

    let toQueryParamsMethod =
        match <@@ RuntimeHelpers.toQueryParams "" null Unchecked.defaultof<ProvidedApiClientBase> @@> with
        | Call(None, m, _) -> m
        | _ -> failwith "Cannot extract toQueryParams MethodInfo"

    let resolveCastMethod(ownerType: Type) =
        ownerType.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
        |> Array.tryFind(fun m ->
            m.Name = "cast"
            && m.IsGenericMethodDefinition
            && m.GetGenericArguments().Length = 1
            && m.GetParameters().Length = 1)
        |> Option.defaultWith(fun () -> failwithf "Cannot extract %s.cast<'T> MethodInfo" ownerType.FullName)

    let taskCastMethod = resolveCastMethod typeof<TaskExtensions>
    let asyncCastMethod = resolveCastMethod typeof<AsyncExtensions>

    let stringPairListExpr(items: (string * string) list) : Expr<(string * string) list> =
        let empty = <@ [] @>

        (empty, List.rev items)
        ||> List.fold(fun acc (name, value) ->
            let nameExpr = Expr.Value(name, typeof<string>) |> Expr.Cast<string>
            let valueExpr = Expr.Value(value, typeof<string>) |> Expr.Cast<string>

            <@ (%nameExpr, %valueExpr) :: %acc @>)

    let typedListExpr(items: Expr<'T> list) : Expr<'T list> =
        let empty = <@ [] @>

        (empty, List.rev items)
        ||> List.fold(fun acc item -> <@ %item :: %acc @>)

    let compileOperation (providedMethodName: string) (apiCall: ApiCall) =
        let path, pathItem, opTy = apiCall
        let operation = pathItem.Operations[opTy]

        if String.IsNullOrWhiteSpace providedMethodName then
            failwithf $"Operation name could not be empty. See '%s{path}/%A{opTy}'"

        let unambiguousName(par: IOpenApiParameter) =
            $"%s{par.Name}In%A{par.In}"

        let openApiParameters =
            [ if not(isNull pathItem.Parameters) then
                  yield! pathItem.Parameters
              if not(isNull operation.Parameters) then
                  yield! operation.Parameters ]

        let (|MediaType|_|) contentType (content: IDictionary<string, OpenApiMediaType>) =
            if isNull content then
                None
            else
                match content.TryGetValue contentType with
                | true, mediaTyObj -> Some mediaTyObj
                | _ -> None

        let (|TextReturn|_|)(input: string) =
            if input.StartsWith("text/") then Some(input) else None

        let (|TextMediaType|_|)(content: IDictionary<string, OpenApiMediaType>) =
            if isNull content then
                None
            else
                content.Keys |> Seq.tryPick (|TextReturn|_|)

        let (|NoMediaType|_|)(content: IDictionary<string, OpenApiMediaType>) =
            if isNull content || content.Count = 0 then Some() else None

        let payloadTy, payloadMime, parameters, ctArgIndex, apiParamByProvidedName =
            /// handles de-duplicating Swagger parameter names if the same parameter name
            /// appears in multiple locations in a given operation definition.
            let uniqueParamName usedNames (param: IOpenApiParameter) =
                let name = niceCamelName param.Name

                if usedNames |> Set.contains name then
                    let fqName = unambiguousName param
                    Set.add fqName usedNames, fqName
                else
                    Set.add name usedNames, name

            let bodyFormatAndParam =
                if isNull operation.RequestBody then
                    None
                else
                    let formatAndParam (payloadType: PayloadType) schema =
                        let p =
                            OpenApiParameter(
                                In = Nullable<_>(), // In Body parameter indicator
                                Name = payloadType.ToString(),
                                Schema = schema,
                                Required = true //operation.RequestBody.Required
                            )
                            :> IOpenApiParameter

                        Some(payloadType, p)

                    match operation.RequestBody.Content with
                    | MediaType MediaTypes.ApplicationJson mediaTyObj -> formatAndParam AppJson mediaTyObj.Schema
                    | MediaType MediaTypes.ApplicationOctetStream mediaTyObj -> formatAndParam AppOctetStream mediaTyObj.Schema
                    | MediaType MediaTypes.MultipartFormData mediaTyObj -> formatAndParam MultipartFormData mediaTyObj.Schema
                    | MediaType MediaTypes.ApplicationFormUrlEncoded mediaTyObj -> formatAndParam AppFormUrlEncoded mediaTyObj.Schema
                    | MediaType MediaTypes.TextPlain mediaTyObj -> formatAndParam TextPlain mediaTyObj.Schema
                    | NoMediaType ->
                        // RequestBody declared but with no content entries: expose a placeholder
                        // noData parameter, but do not emit an HTTP request body.
                        let defSchema = OpenApiSchema()
                        formatAndParam NoData defSchema
                    | _ ->
                        let keys = operation.RequestBody.Content.Keys |> String.concat ";"

                        let operationId =
                            if String.IsNullOrWhiteSpace(operation.OperationId) then
                                $"%s{path}/%A{opTy}"
                            else
                                operation.OperationId

                        failwithf $"Operation '%s{operationId}' does not contain supported media types [%A{keys}]"

            let payloadTy = bodyFormatAndParam |> Option.map fst |> Option.defaultValue NoData

            let requiredOpenApiParams, optionalOpenApiParams =
                [ yield! openApiParameters
                  if bodyFormatAndParam.IsSome then
                      yield bodyFormatAndParam.Value |> snd ]
                |> List.distinctBy(fun op -> op.Name, op.In)
                |> List.partition(_.Required)

            let buildProvidedParameters usedNames (paramList: IOpenApiParameter list) =
                ((usedNames, [], []), paramList)
                ||> List.fold(fun (names, parameters, lookup) current ->
                    let names, paramName = uniqueParamName names current

                    let paramType =
                        defCompiler.CompileTy providedMethodName paramName current.Schema current.Required

                    let providedParam =
                        if current.Required then
                            ProvidedParameter(paramName, paramType)
                        else
                            let paramDefaultValue = defCompiler.GetDefaultValue paramType
                            ProvidedParameter(paramName, paramType, false, paramDefaultValue)

                    (names, providedParam :: parameters, (paramName, current) :: lookup))
                |> fun (finalNames, ps, lookup) -> finalNames, List.rev ps, List.rev lookup

            let namesAfterRequired, requiredProvidedParams, requiredLookup =
                buildProvidedParameters Set.empty requiredOpenApiParams

            let _, optionalProvidedParams, optionalLookup =
                buildProvidedParameters namesAfterRequired optionalOpenApiParams

            let apiParamByProvidedName =
                requiredLookup @ optionalLookup
                |> List.choose(fun (paramName, param) -> if param.In.HasValue then Some(paramName, param) else None)
                |> Map.ofList

            let ctArgIndex, parameters =
                let scope = UniqueNameGenerator()

                (requiredProvidedParams @ optionalProvidedParams)
                |> List.iter(fun p -> scope.MakeUnique p.Name |> ignore)

                let ctName = scope.MakeUnique "cancellationToken"

                let ctParam =
                    ProvidedParameter(ctName, typeof<Threading.CancellationToken>, false, null)
                // CT is appended last to preserve existing positional argument calls
                let ctArgIndex =
                    List.length requiredProvidedParams
                    + List.length optionalProvidedParams

                ctArgIndex, requiredProvidedParams @ optionalProvidedParams @ [ ctParam ]

            payloadTy, payloadTy.ToMediaType(), parameters, ctArgIndex, apiParamByProvidedName

        // find the inner type value
        let okResponse =
            operation.Responses
            |> Seq.tryFind(fun resp -> resp.Key = "200")
            |> Option.orElseWith(fun () ->
                operation.Responses
                |> Seq.tryFind(fun resp ->
                    let (ok, code) = Int32.TryParse(resp.Key)
                    ok && code >= 200 && code < 300))
            |> Option.orElseWith(fun () -> operation.Responses |> Seq.tryFind(fun resp -> resp.Key = "default"))

        let retMimeAndTy =
            okResponse
            |> Option.bind(fun kv ->
                match kv.Value.Content with
                | MediaType MediaTypes.ApplicationJson mediaTy ->
                    let ty =
                        if isNull mediaTy.Schema then
                            typeof<unit>
                        else
                            defCompiler.CompileTy providedMethodName "Response" mediaTy.Schema true

                    Some(MediaTypes.ApplicationJson, ty)
                | MediaType MediaTypes.ApplicationOctetStream mediaTy ->
                    let ty =
                        if isNull mediaTy.Schema then
                            typeof<IO.Stream>
                        else
                            defCompiler.CompileTy providedMethodName "Response" mediaTy.Schema true

                    Some(MediaTypes.ApplicationOctetStream, ty)
                | TextMediaType mediaTy -> Some(mediaTy, typeof<string>)
                | _ -> None)

        let retMime = retMimeAndTy |> Option.map fst |> Option.defaultValue null
        let retTy = retMimeAndTy |> Option.map snd

        let overallReturnType =
            let wrapperTy =
                if asAsync then
                    typedefof<Async<unit>>
                else
                    typedefof<System.Threading.Tasks.Task<unit>>

            let genericTy = retTy |> Option.defaultValue typeof<unit>
            ProvidedTypeBuilder.MakeGenericType(wrapperTy, [ genericTy ])

        let errorCodes, errorDescriptions =
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

        let fixedHeaders =
            [ if not(isNull payloadMime) then
                  "Content-Type", payloadMime
              if not(isNull retMime) then
                  "Accept", retMime ]

        let m =
            ProvidedMethod(
                providedMethodName,
                parameters,
                overallReturnType,
                invokeCode =
                    fun args ->
                        let this =
                            Expr.Coerce(args[0], typeof<ProvidedApiClientBase>)
                            |> Expr.Cast<ProvidedApiClientBase>

                        let httpMethod = opTy.ToString()

                        let headers = stringPairListExpr fixedHeaders

                        // Locates parameters matching the arguments
                        let mutable payloadExp = None

                        // CT is inserted at ctArgIndex. Extract it by position.
                        let apiArgs, ct =
                            let allArgs = List.tail args // skip `this`
                            let ctArg = List.item ctArgIndex allArgs

                            let apiArgs =
                                allArgs
                                |> List.indexed
                                |> List.choose(fun (i, a) -> if i = ctArgIndex then None else Some a)

                            apiArgs, Expr.Cast<Threading.CancellationToken>(ctArg)

                        let parameters =
                            apiArgs
                            |> List.choose (function
                                | ShapeVar sVar as expr ->
                                    match apiParamByProvidedName |> Map.tryFind sVar.Name with
                                    | Some(par) -> Some(par, expr)
                                    | _ ->
                                        let payloadType = PayloadType.Parse sVar.Name

                                        match payloadExp with
                                        | None ->
                                            payloadExp <- Some(payloadType, Expr.Coerce(expr, typeof<obj>))
                                            None
                                        | Some _ ->
                                            failwithf
                                                $"More than one payload parameter is specified: '%A{payloadType}' & '%A{payloadExp.Value |> fst}'"
                                | _ -> failwithf $"Function '%s{providedMethodName}' does not support functions as arguments.")

                        // Makes argument a string // TODO: Make body an exception
                        // NOTE: avoid `let x = ...` in quotation literals — they share a single Var
                        // object across all calls, causing "duplicate key" exceptions in ProvidedTypes
                        // when the same helper is called for multiple parameters in one operation.
                        // Instead, build the call expression directly without an intermediate binding.
                        let coerceString exp =
                            let obj = Expr.Coerce(exp, typeof<obj>)
                            Expr.Call(toParamMethod, [ obj ]) |> Expr.Cast<string>

                        let rec coerceQueryString name expr =
                            let obj = Expr.Coerce(expr, typeof<obj>)

                            Expr.Call(toQueryParamsMethod, [ Expr.Value name; obj; this ])
                            |> Expr.Cast<(string * string) list>

                        // Partitions arguments based on their locations
                        let path, queryParamLists, headers, cookies =
                            let path, queryParamLists, headers, cookies =
                                ((<@ path @>, [], headers, <@ [] @>), parameters)
                                ||> List.fold(fun (path, queryParamLists, headers, cookies) (param: IOpenApiParameter, valueExpr) ->
                                    if param.In.HasValue then
                                        let name = param.Name

                                        match param.In.Value with
                                        | ParameterLocation.Path ->
                                            let value = coerceString valueExpr
                                            let pattern = $"{{%s{name}}}"
                                            let path' = <@ (%path).Replace(pattern, %value) @>
                                            (path', queryParamLists, headers, cookies)
                                        | ParameterLocation.Query ->
                                            let listValues = coerceQueryString name valueExpr
                                            (path, listValues :: queryParamLists, headers, cookies)
                                        | ParameterLocation.Header ->
                                            let value = coerceString valueExpr
                                            let headers' = <@ (name, %value) :: (%headers) @>
                                            (path, queryParamLists, headers', cookies)
                                        | ParameterLocation.Cookie ->
                                            let value = coerceString valueExpr
                                            let cookies' = <@ (name, %value) :: (%cookies) @>
                                            (path, queryParamLists, headers, cookies')
                                        | x -> failwithf $"Unsupported parameter location '%A{x}'"
                                    else
                                        failwithf "This should not happen, payload expression is already parsed")

                            path, List.rev queryParamLists, headers, cookies

                        let queryParamLists = typedListExpr queryParamLists

                        let httpRequestMessage =
                            <@
                                let msg =
                                    RuntimeHelpers.createHttpRequestFromQueryLists httpMethod %path %queryParamLists

                                RuntimeHelpers.fillHeadersAndCookies msg %headers %cookies
                                msg
                            @>

                        let httpRequestMessageWithPayload =
                            match payloadExp with
                            | None -> httpRequestMessage
                            | Some(NoData, _) -> httpRequestMessage
                            | Some(AppJson, body) ->
                                <@
                                    let valueStr = (%this).Serialize(%%body: obj)
                                    let msg = %httpRequestMessage
                                    msg.Content <- RuntimeHelpers.toStringContent(valueStr)
                                    msg
                                @>
                            | Some(AppOctetStream, streamObj) ->
                                <@
                                    let stream: obj = %%streamObj
                                    let msg = %httpRequestMessage
                                    msg.Content <- RuntimeHelpers.toStreamContent(stream, payloadMime)
                                    msg
                                @>
                            | Some(MultipartFormData, formData) ->
                                <@
                                    let data = RuntimeHelpers.getPropertyValues(%%formData: obj)
                                    let msg = %httpRequestMessage
                                    msg.Content <- RuntimeHelpers.toMultipartFormDataContent data
                                    msg
                                @>
                            | Some(AppFormUrlEncoded, formUrlEncoded) ->
                                <@
                                    let data = RuntimeHelpers.getPropertyValues(%%formUrlEncoded: obj)
                                    let msg = %httpRequestMessage
                                    msg.Content <- RuntimeHelpers.toFormUrlEncodedContent(data)
                                    msg
                                @>
                            | Some(TextPlain, textObj) ->
                                <@
                                    let text = (%%textObj: obj).ToString()
                                    let msg = %httpRequestMessage
                                    msg.Content <- RuntimeHelpers.toTextContent(text)
                                    msg
                                @>

                        let action =
                            <@ (%this).CallAsync(%httpRequestMessageWithPayload, errorCodes, errorDescriptions, %ct) @>

                        let responseObj() =
                            let innerReturnType = defaultArg retTy null

                            <@
                                let x = %action
                                let ct = %ct

                                task {
                                    let! response = x
                                    let! content = RuntimeHelpers.readContentAsString response.Content ct
                                    return (%this).Deserialize(content, innerReturnType)
                                }
                            @>

                        let responseStream() =
                            <@
                                let x = %action
                                let ct = %ct

                                task {
                                    let! response = x
                                    let! data = RuntimeHelpers.readContentAsStream response.Content ct
                                    return data
                                }
                            @>

                        let responseString() =
                            <@
                                let x = %action
                                let ct = %ct

                                task {
                                    let! response = x
                                    let! data = RuntimeHelpers.readContentAsString response.Content ct
                                    return data
                                }
                            @>

                        let responseUnit() =
                            <@
                                let x = %action

                                task {
                                    let! _ = x
                                    return ()
                                }
                            @>

                        // Build only the response quotation needed for this operation's return shape.
                        // For typed JSON responses, emit direct generic cast calls so generated clients
                        // do not pay MethodInfo.Invoke costs on every API call.
                        if not asAsync then
                            match retTy with
                            | None -> (responseUnit()).Raw
                            | Some t when t = typeof<IO.Stream> -> <@ %(responseStream()) @>.Raw
                            | Some t ->
                                match retMime with
                                | TextReturn _ -> <@ %(responseString()) @>.Raw
                                | _ ->
                                    let castMethod = ProvidedTypeBuilder.MakeGenericMethod(taskCastMethod, [ t ])

                                    Expr.Call(castMethod, [ responseObj() ])
                                    |> fun e -> Expr.Coerce(e, overallReturnType)
                        else
                            let awaitTask t =
                                <@ Async.AwaitTask(%t) @>

                            match retTy with
                            | None -> (awaitTask(responseUnit())).Raw
                            | Some t when t = typeof<IO.Stream> -> <@ %(awaitTask(responseStream())) @>.Raw
                            | Some t ->
                                match retMime with
                                | TextReturn _ -> <@ %(awaitTask(responseString())) @>.Raw
                                | _ ->
                                    let castMethod = ProvidedTypeBuilder.MakeGenericMethod(asyncCastMethod, [ t ])

                                    Expr.Call(castMethod, [ awaitTask(responseObj()) ])
                                    |> fun e -> Expr.Coerce(e, overallReturnType)
            )

        let xmlDoc =
            let buildParamDesc(p: IOpenApiParameter) =
                let enumDoc =
                    if not(isNull p.Schema) then
                        XmlDoc.buildEnumDoc p.Schema.Enum
                    else
                        None

                XmlDoc.combineDescAndEnum p.Description enumDoc

            let paramDescriptions =
                [ for p in openApiParameters -> niceCamelName p.Name, buildParamDesc p
                  if not(isNull operation.RequestBody) then
                      yield niceCamelName(payloadTy.ToString()), operation.RequestBody.Description ]

            let returnDoc =
                okResponse
                |> Option.bind(fun kv -> kv.Value.Description |> Option.ofObj)
                |> Option.filter(String.IsNullOrWhiteSpace >> not)

            XmlDoc.buildXmlDoc operation.Summary operation.Description paramDescriptions returnDoc

        if not(String.IsNullOrEmpty xmlDoc) then
            m.AddXmlDoc xmlDoc

        if operation.Deprecated then
            m.AddObsoleteAttribute("Operation is deprecated", false)

        m

    static member GetMethodNameCandidate (apiCall: ApiCall) skipLength ignoreOperationId =
        let path, pathItem, opTy = apiCall
        let operation = pathItem.Operations[opTy]

        if ignoreOperationId || String.IsNullOrWhiteSpace(operation.OperationId) then
            let _, pathParts =
                (path.Split([| '/' |], StringSplitOptions.RemoveEmptyEntries), (false, []))
                ||> Array.foldBack(fun x (nextIsArg, pathParts) ->
                    if x.StartsWith("{") then
                        (true, pathParts)
                    else
                        (false, (if nextIsArg then singularize x else x) :: pathParts))

            String.Join("_", opTy.ToString() :: pathParts)
        else
            operation.OperationId.Substring(skipLength)
        |> nicePascalName

    member _.CompileProvidedClients(ns: NamespaceAbstraction) =
        let defaultHost =
            if schema.Servers.Count = 0 then
                null
            else
                schema.Servers[0].Url

        let baseTy = Some typeof<ProvidedApiClientBase>
        let baseCtor = baseTy.Value.GetConstructors().[0]

        List.ofSeq schema.Paths
        |> List.collect(fun path ->
            // if path.Value.UnresolvedReference then
            //     failwith
            //         $"TP does not support unresolved paths / external references. Path '%s{path.Key}' refer to '%s{path.Value.Reference.ReferenceV3}'"

            let safeSeq s =
                if isNull s then Seq.empty else s

            List.ofSeq(safeSeq path.Value.Operations)
            |> List.map(fun kv -> path.Key, path.Value, kv.Key))

        |> List.groupBy(fun (_, pathItem, opTy) ->
            if ignoreControllerPrefix then
                String.Empty //
            else
                let op = pathItem.Operations[opTy]

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
                ty.AddXmlDoc $"Client for '%s{clientName}_*' operations"

            [ ProvidedConstructor(
                  [ ProvidedParameter("httpClient", typeof<HttpClient>)
                    ProvidedParameter("options", typeof<JsonSerializerOptions>) ],
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
              ) ]
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
                let uniqueName = methodNameScope.MakeUnique name
                compileOperation uniqueName op)
            |> ty.AddMembers)
