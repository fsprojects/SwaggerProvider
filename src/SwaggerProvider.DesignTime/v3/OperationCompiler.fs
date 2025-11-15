namespace SwaggerProvider.Internal.v3.Compilers

open System
open System.Collections.Generic
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
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

    override x.ToString() =
        match x with
        | NoData -> "noData"
        | AppJson -> "json"
        | AppOctetStream -> "octetStream"
        | AppFormUrlEncoded -> "formUrlEncoded"
        | MultipartFormData -> "formData"

    member x.ToMediaType() =
        match x with
        | NoData -> null
        | AppJson -> MediaTypes.ApplicationJson
        | AppOctetStream -> MediaTypes.ApplicationOctetStream
        | AppFormUrlEncoded -> MediaTypes.ApplicationFormUrlEncoded
        | MultipartFormData -> MediaTypes.MultipartFormData

    static member Parse =
        function
        | "noData" -> NoData
        | "json" -> AppJson
        | "octetStream" -> AppOctetStream
        | "formUrlEncoded" -> AppFormUrlEncoded
        | "formData" -> MultipartFormData
        | name -> failwithf $"Payload '%s{name}' is not supported"

/// Object for compiling operations.
type OperationCompiler(schema: OpenApiDocument, defCompiler: DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync: bool) =
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

        let (|MediaType|_|) contentType (content: IDictionary<string, IOpenApiMediaType>) =
            match content.TryGetValue contentType with
            | true, mediaTyObj -> Some mediaTyObj
            | _ -> None

        let (|TextReturn|_|)(input: string) =
            if input.StartsWith("text/") then Some(input) else None

        let (|TextMediaType|_|)(content: IDictionary<string, IOpenApiMediaType>) =
            content.Keys |> Seq.tryPick (|TextReturn|_|)

        let (|NoMediaType|_|)(content: IDictionary<string, IOpenApiMediaType>) =
            if content.Count = 0 then Some() else None

        let payloadMime, parameters =
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
                    | NoMediaType ->
                        // Assume that server treat it as `applicationJson`
                        let defSchema = OpenApiSchema() // todo: we need to test it
                        formatAndParam NoData defSchema
                    | _ ->
                        let keys = operation.RequestBody.Content.Keys |> String.concat ";"
                        failwithf $"Operation '%s{operation.OperationId}' does not contain supported media types [%A{keys}]"

            let payloadTy = bodyFormatAndParam |> Option.map fst |> Option.defaultValue NoData

            let orderedParameters =
                let required, optional =
                    [ yield! openApiParameters
                      if bodyFormatAndParam.IsSome then
                          yield bodyFormatAndParam.Value |> snd ]
                    |> List.distinctBy(fun op -> op.Name, op.In)
                    |> List.partition(_.Required)

                List.append required optional

            let providedParameters =
                ((Set.empty, []), orderedParameters)
                ||> List.fold(fun (names, parameters) current ->
                    let names, paramName = uniqueParamName names current

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

            payloadTy.ToMediaType(), providedParameters

        // find the inner type value
        let retMimeAndTy =
            let okResponse =
                operation.Responses
                |> Seq.tryFind(fun resp -> resp.Key = "200" || resp.Key.StartsWith("20") || resp.Key = "default")

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

                        let headers =
                            <@
                                [ if not(isNull payloadMime) then
                                      "Content-Type", payloadMime
                                  if not(isNull retMime) then
                                      "Accept", MediaTypes.ApplicationJson ]
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
                                        | Some _ ->
                                            failwithf
                                                $"More than one payload parameter is specified: '%A{payloadType}' & '%A{payloadExp.Value |> fst}'"
                                | _ -> failwithf $"Function '%s{providedMethodName}' does not support functions as arguments.")

                        // Makes argument a string // TODO: Make body an exception
                        let coerceString exp =
                            let obj = Expr.Coerce(exp, typeof<obj>) |> Expr.Cast<obj>
                            <@ let x = (%obj) in RuntimeHelpers.toParam x @>

                        let rec coerceQueryString name expr =
                            let obj = Expr.Coerce(expr, typeof<obj>)
                            <@ let o = (%%obj: obj) in RuntimeHelpers.toQueryParams name o (%this) @>

                        // Partitions arguments based on their locations
                        let path, queryParams, headers =
                            let path, queryParams, headers, cookies =
                                ((<@ path @>, <@ [] @>, headers, <@ [] @>), parameters)
                                ||> List.fold(fun (path, query, headers, cookies) (param: IOpenApiParameter, valueExpr) ->
                                    if param.In.HasValue then
                                        let name = param.Name

                                        match param.In.Value with
                                        | ParameterLocation.Path ->
                                            let value = coerceString valueExpr
                                            let pattern = $"{{%s{name}}}"
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
                                        | x -> failwithf $"Unsupported parameter location '%A{x}'"
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
                                    msg.Content <- RuntimeHelpers.toStreamContent(stream)
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

                        let responseString =
                            <@
                                let x = %action

                                task {
                                    let! response = x
                                    let! data = response.ReadAsStringAsync()
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
                            | Some t ->
                                match retMime with
                                | TextReturn _ -> <@ %responseString @>.Raw
                                | _ -> Expr.Coerce(<@ RuntimeHelpers.taskCast t %responseObj @>, overallReturnType)
                        else
                            let awaitTask t =
                                <@ Async.AwaitTask(%t) @>

                            match retTy with
                            | None -> (awaitTask responseUnit).Raw
                            | Some t when t = typeof<IO.Stream> -> <@ %(awaitTask responseStream) @>.Raw
                            | Some t ->
                                match retMime with
                                | TextReturn _ -> <@ %(awaitTask responseString) @>.Raw
                                | _ -> Expr.Coerce(<@ RuntimeHelpers.asyncCast t %(awaitTask responseObj) @>, overallReturnType)
            )

        if not <| String.IsNullOrEmpty(operation.Summary) then
            m.AddXmlDoc(operation.Summary) // TODO: Use description of parameters in docs

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
                compileOperation (methodNameScope.MakeUnique name) op)
            |> ty.AddMembers)
