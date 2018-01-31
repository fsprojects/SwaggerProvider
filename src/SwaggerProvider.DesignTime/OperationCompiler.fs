namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open Swagger.Parser.Schema
open Swagger.Internal

open System

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open System.Text.RegularExpressions
open SwaggerProvider.Internal
open System.Net.Http
open System.Collections.Generic

/// Object for compiling operations.
type OperationCompiler (schema:SwaggerObject, defCompiler:DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, asAsync: bool, asyncTy: Type, taskTy: Type) =
    let compileOperation (methodName:string) (op:OperationObject) =
        if String.IsNullOrWhiteSpace methodName
            then failwithf "Operation name could not be empty. See '%s/%A'" op.Path op.Type

        let parameters =
            /// handles deduping Swagger parameter names if the same parameter name 
            /// appears in multiple locations in a given operation definition.
            let uniqueParamName existing (current: ParameterObject) = 
                let name = niceCamelName current.Name
                if Set.contains name existing 
                then
                    Set.add current.UnambiguousName existing, current.UnambiguousName
                else
                    Set.add name existing, name                
                
            let required, optional = op.Parameters |> Array.partition (fun x->x.Required)
            
            Array.append required optional
            |> Array.fold (fun (names,parameters) current -> 
               let (names, paramName) = uniqueParamName names current
               let paramType = defCompiler.CompileTy methodName paramName current.Type current.Required
               let providedParam = 
                   if current.Required then ProvidedParameter(paramName, paramType)
                   else 
                       let paramDefaultValue = defCompiler.GetDefaultValue paramType
                       ProvidedParameter(paramName, paramType, false, paramDefaultValue)
               (names, providedParam :: parameters)
            ) (Set.empty, [])
            |> snd
            // because we built up our list in reverse order with the fold, 
            // reverse it again so that all required properties come first
            |> List.rev
        
        // find the innner type value
        let retTy =
            let okResponse = // BUG :  wrong selector
                op.Responses |> Array.tryFind (fun (code, _) ->
                    (code.IsSome && (code.Value = 200 || code.Value = 201)) || code.IsNone)
            match okResponse with
            | Some (_,resp) ->
                match resp.Schema with
                | None -> None
                | Some ty -> Some <| defCompiler.CompileTy methodName "Response" ty true
            | None -> None
        
        let generateReturnType (retTy: Type) asAsync = 
            if asAsync then ProvidedTypeBuilder.MakeGenericType(asyncTy, [retTy])
            else ProvidedTypeBuilder.MakeGenericType(taskTy, [retTy])

        let overallReturnType = generateReturnType (defaultArg retTy (typeof<unit>)) asAsync

        let m = ProvidedMethod(methodName, parameters, overallReturnType, invokeCode = fun args ->
            let thisTy = typeof<ProvidedSwaggerBaseType>
            let this = Expr.Coerce(args.[0], thisTy) |> Expr.Cast<ProvidedSwaggerBaseType>
            let host = <@ (%this).Host @>
            let headers = <@ (%this).Headers @>
            let customizeHttpRequest = <@ (%this).CustomizeHttpRequest @> 
            
            let basePath =
                let basePath = schema.BasePath
                <@ RuntimeHelpers.combineUrl %host basePath @>

            // Fit headers into quotation
            let headers =
                let jsonConsumable = op.Consumes |> Seq.exists (fun mt -> mt="application/json")
                <@ let ctHeaderExist = %headers |> Array.exists (fun (h,_)->h="Content-Type")
                   if not(ctHeaderExist) && jsonConsumable
                   then Array.append [|"Content-Type","application/json"|] %headers
                   else %headers @>

            // Locates parameters matching the arguments
            let parameters =
                List.tail args // skip `this` param
                |> List.map (function
                    | ShapeVar sVar as expr ->
                        let param =
                            op.Parameters
                            |> Array.find (fun x -> 
                                // pain point: we have to make sure that the set of names we search for here are the same as the set of names generated when we make `parameters` above
                                let baseName = niceCamelName x.Name
                                baseName = sVar.Name || x.UnambiguousName = sVar.Name )
                        param, expr
                    | _  ->
                        failwithf "Function '%s' does not support functions as arguments." methodName
                    )

            // Makes argument a string // TODO: Make body an exception
            let coerceString defType (format : CollectionFormat) exp =
                let obj = Expr.Coerce(exp, typeof<obj>) |> Expr.Cast<obj>
                <@ (%obj).ToString() @>

            let rec corceQueryString name expr =
                let obj = Expr.Coerce(expr, typeof<obj>)
                <@ let o = (%%obj : obj)
                   RuntimeHelpers.toQueryParams name o @>

            let replacePathTemplate (path: Expr<string>) (name: string) (value: Expr<string>) =
                <@ Regex.Replace(%path, sprintf "{%s}" name, %value) @>

            let addPayload load (param : ParameterObject) (exp : Expr) =
                let name = param.Name
                let var = coerceString param.Type param.CollectionFormat exp
                match load with
                | Some (FormData, b) -> Some (FormData, <@@ Seq.append %%b [name, (%var : string)] @@>)
                | None               -> match param.In with
                                        | Body -> Some (Body, Expr.Coerce (exp, typeof<obj>))
                                        | _    -> Some (FormData, <@@ (seq [name, (%var : string)]) @@>)
                | _                  -> failwith ("Can only contain one payload")

            let addQuery (quer: Expr<(string*string) list>) name (exp : Expr) =
                let listValues = corceQueryString name exp
                <@ List.append %quer %listValues @>

            let addHeader (heads: Expr<(string * string) []>) name (value: Expr<string>) =
                <@ Array.append %heads [|name, %value|] @>

            // Partitions arguments based on their locations
            let (path, payload, queries, heads) =
                let mPath = op.Path
                parameters
                |> List.fold (
                    fun (path, load, quer, head) (param : ParameterObject, exp) ->
                        let name = param.Name
                        match param.In with
                        | Path   -> 
                            let value = coerceString param.Type param.CollectionFormat exp
                            (replacePathTemplate path name value, load, quer, head)
                        | FormData
                        | Body   -> (path, addPayload load param exp, quer, head)
                        | Query  -> (path, load, addQuery quer name exp, head)
                        | Header -> 
                            let value = coerceString param.Type param.CollectionFormat exp
                            (path, load, quer, addHeader head name value)
                    )
                    ( <@ mPath @>, None, <@ [] @>, headers)

            let address = <@ RuntimeHelpers.combineUrl %basePath %path @>
            let restCall = op.Type

            let httpMethodForRestCall restCall = 
                match restCall with
                | Get -> HttpMethod.Get
                | Put -> HttpMethod.Put
                | Post -> HttpMethod.Post
                | Delete -> HttpMethod.Delete
                | Options -> HttpMethod.Options
                | Head -> HttpMethod.Head
                | Patch -> HttpMethod("Patch")

            let customizeHttpRequest =
                <@ fun (request:HttpRequestMessage) ->
                     if restCall = Post && request.Content <> null
                     then request.Content.Headers.ContentLength <- Nullable<_> 0L
                     (%customizeHttpRequest) request @>

            let innerReturnType = defaultArg retTy null

            let httpRequestMessage = 
                <@ 
                   let requestUrl = 
                    let uriB = UriBuilder %address
                    let newQueries = %queries |> Seq.map (fun (name, value) -> sprintf "%s=%s" (Uri.EscapeDataString name) (Uri.EscapeDataString value)) |> String.concat "&"
                    if String.IsNullOrEmpty uriB.Query
                    then uriB.Query <- sprintf "%s&%s" uriB.Query newQueries
                    else uriB.Query <- sprintf "%s?%s" uriB.Query newQueries 
                    uriB.Uri
                   let msg = new HttpRequestMessage(httpMethodForRestCall restCall, requestUrl)
                   for (name, value) in %heads do msg.Headers.Add(name, value)
                   msg @>

            let sendMessage message = 
                async {
                    let! response = RuntimeHelpers.httpClient.SendAsync(message) |> Async.AwaitTask
                    return! response.EnsureSuccessStatusCode().Content.ReadAsStringAsync() |> Async.AwaitTask
                }

            let action = 
                match payload with
                | None ->
                    <@ (%customizeHttpRequest) %httpRequestMessage |> sendMessage @>
                | Some (FormData, b) ->
                    <@ let keyValues = (%%b: seq<string*string>) |> Seq.map KeyValuePair
                       let msg = %httpRequestMessage
                       msg.Content <- new FormUrlEncodedContent(keyValues)
                       let msg = (%customizeHttpRequest) msg
                       sendMessage msg @>
                | Some (Body, b)     ->
                    <@ let content = new StringContent(RuntimeHelpers.serialize (%%b: obj), Text.Encoding.UTF8, "application/json")
                       let msg = %httpRequestMessage
                       msg.Content <- content
                       let msg = (%customizeHttpRequest) msg
                       sendMessage msg @>
                | Some (x, _) -> failwith ("Payload should not be able to have type: " + string x)
      
            let responseObj = 
                <@ async {
                    let! response = %action
                    return RuntimeHelpers.deserialize response innerReturnType
                   } @>

            let responseUnit =
                <@ async {
                    let! _ = %action
                    return ()
                   } @>
            
            let task t = <@ Async.StartAsTask(%t) @>

            // if we're an async method, then we can just return the above, coerced to the overallReturnType.
            // if we're not async, then run that^ through Async.RunSynchronously before doing the coercion.
            match asAsync, retTy with
            | true, Some t -> Expr.Coerce(<@ RuntimeHelpers.asyncCast t %responseObj @>, overallReturnType)
            | true, None -> responseUnit.Raw
            | false, Some t -> Expr.Coerce(<@ RuntimeHelpers.taskCast t %(task responseObj) @>, overallReturnType)
            | false, None -> Expr.Coerce(task responseUnit, overallReturnType)
            )
        if not <| String.IsNullOrEmpty(op.Summary)
            then m.AddXmlDoc(op.Summary) // TODO: Use description of parameters in docs
        if op.Deprecated
            then m.AddObsoleteAttribute("Operation is deprecated", false)
        m

    static member GetMethodNameCandidate (op:OperationObject) skipLength ignoreOperationId =
        if ignoreOperationId || String.IsNullOrWhiteSpace(op.OperationId)
        then
            [|  yield op.Type.ToString()
                yield!
                    op.Path.Split('/')
                    |> Array.filter (fun x ->
                        not <| (String.IsNullOrEmpty(x) || x.StartsWith("{")))
            |] |> fun arr -> String.Join("_", arr)
        else op.OperationId.Substring(skipLength)
        |> nicePascalName

    member __.CompileProvidedClients(ns:NamespaceAbstraction) =
        let defaultHost =
            let protocol =
                match schema.Schemes with
                | [||]  -> "http" // Should use the scheme used to access the Swagger definition itself.
                | array -> array.[0]
            sprintf "%s://%s" protocol schema.Host
        let baseTy = Some typeof<ProvidedSwaggerBaseType>
        let baseCtor = baseTy.Value.GetConstructors().[0]

        List.ofArray schema.Paths
        |> List.groupBy (fun x ->
            if ignoreControllerPrefix then String.Empty //
            else
                let ind = x.OperationId.IndexOf("_")
                if ind <= 0 then String.Empty
                else x.OperationId.Substring(0, ind) )
        |> List.iter (fun (clientName, operations) ->
            let tyName = ns.ReserveUniqueName clientName "Client"
            let ty = ProvidedTypeDefinition(tyName, baseTy, isErased = false, hideObjectMethods = true)
            ns.RegisterType(tyName, ty)
            ty.AddXmlDoc (sprintf "Client for '%s_*' operations" clientName)

            ty.AddMember <|
                ProvidedConstructor(
                    [ProvidedParameter("host", typeof<string>, optionalValue = defaultHost)],
                    invokeCode = (fun args ->
                        match args with
                        | [] -> failwith "Generated constructors should always pass the instance as the first argument!"
                        | _ -> <@@ () @@>),
                    BaseConstructorCall = fun args -> (baseCtor, args))

            let methodNameScope = UniqueNameGenerator()
            operations |> List.map (fun op ->
                let skipLength = if String.IsNullOrEmpty clientName then 0 else clientName.Length + 1
                let name = OperationCompiler.GetMethodNameCandidate op skipLength ignoreOperationId
                compileOperation (methodNameScope.MakeUnique name) op)
            |> ty.AddMembers
        )
