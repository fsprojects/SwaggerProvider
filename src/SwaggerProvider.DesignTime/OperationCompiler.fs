namespace SwaggerProvider 
[<System.Flags>]
type OperationTypes =
/// Generate synchronous HTTP calls
| Sync = 1
/// Generate HTTP calls that return Async<'T>
| Async = 2
/// Generate HTTP calls that return Task<'T>
| Task = 4
/// Generate all versions of the HTTP calls
| All = 128

namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open Swagger.Parser.Schema
open Swagger.Internal

open System
open FSharp.Data

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open System.Text.RegularExpressions
open SwaggerProvider.Internal
open System.Threading.Tasks
open SwaggerProvider

module ReflectionHelper = 
    let asyncCast = 
        let castFn = typeof<AsyncExtensions>.GetMethod("cast")
        fun runtimeTy (asyncOp: Async<obj>) -> 
            castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|asyncOp|])

    let taskCast = 
        let castFn = typeof<TaskExtensions>.GetMethod("cast")
        fun runtimeTy (task: Task<obj>) ->
            castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|task|])    

/// Object for compiling operations.
type OperationCompiler (schema:SwaggerObject, defCompiler:DefinitionCompiler, ignoreControllerPrefix, ignoreOperationId, genMethods : OperationTypes) =
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
        
        let methodTypes = [ if genMethods.HasFlag(OperationTypes.All) || genMethods.HasFlag(OperationTypes.Async) then yield OperationTypes.Async
                            if genMethods.HasFlag(OperationTypes.All) || genMethods.HasFlag(OperationTypes.Task) then yield OperationTypes.Task
                            if genMethods.HasFlag(OperationTypes.All) || genMethods.HasFlag(OperationTypes.Sync) then yield OperationTypes.Sync ]

        let generateReturnType (t: Type option) (method: OperationTypes) = 
            match method, t with
            | OperationTypes.Async, Some t -> typedefof<Async<unit>>.MakeGenericType(t)
            | OperationTypes.Async, None -> typeof<Async<unit>>
            | OperationTypes.Task, Some t -> typedefof<Task<unit>>.MakeGenericType(t)
            | OperationTypes.Task, None -> typeof<Task<unit>>
            | OperationTypes.Sync, Some t -> t
            | OperationTypes.Sync, None -> typeof<unit>
            | _ -> failwithf "unknown OperationTypes %O" method

        [ for method in methodTypes do
            let overallReturnType = generateReturnType retTy method
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
                let restCall = op.Type.ToString()

                let customizeHttpRequest =
                    <@ fun (request:Net.HttpWebRequest) ->
                         if restCall = "Post"
                             then request.ContentLength <- 0L
                         (%customizeHttpRequest) request @>

                let innerReturnType = defaultArg retTy null
                let action = 
                    match payload with
                    | None ->
                        <@ Http.AsyncRequestString(%address,
                                httpMethod = restCall,
                                headers = %heads,
                                query = %queries,
                                customizeHttpRequest = %customizeHttpRequest) @>
                    | Some (FormData, b) ->
                        <@ Http.AsyncRequestString(%address,
                                httpMethod = restCall,
                                headers = %heads,
                                body = HttpRequestBody.FormValues (%%b: seq<string*string>),
                                query = %queries,
                                customizeHttpRequest = %customizeHttpRequest) @>
                    | Some (Body, b)     ->
                        <@ let body = RuntimeHelpers.serialize (%%b: obj)
                           Http.AsyncRequestString(%address,
                                httpMethod = restCall,
                                headers = %heads,
                                body = HttpRequestBody.TextRequest body,
                                query = %queries,
                                customizeHttpRequest = %customizeHttpRequest) @>
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
                
                let sync e = <@ Async.RunSynchronously(%e) @>
                let task t = <@ Async.StartAsTask(%t) @>
                // if we're an async method, then we can just return the above, coerced to the overallReturnType.
                // if we're not async, then run that^ through Async.RunSynchronously before doing the coercion.
                match method, retTy with
                | OperationTypes.Async, Some t -> Expr.Coerce(<@ ReflectionHelper.asyncCast t %responseObj @>, overallReturnType)
                | OperationTypes.Async, None -> responseUnit.Raw
                | OperationTypes.Task, Some t -> Expr.Coerce(<@ ReflectionHelper.taskCast t %(task responseObj) @>, overallReturnType)
                | OperationTypes.Task, None -> Expr.Coerce(task responseUnit, overallReturnType)
                | OperationTypes.Sync, Some _ -> Expr.Coerce(sync responseObj, overallReturnType)
                | OperationTypes.Sync, None -> Expr.Coerce(sync responseUnit, overallReturnType)
                | _ -> failwithf "unknown OperationTypes %A" method
            )
            if not <| String.IsNullOrEmpty(op.Summary)
                then m.AddXmlDoc(op.Summary) // TODO: Use description of parameters in docs
            if op.Deprecated
                then m.AddObsoleteAttribute("Operation is deprecated", false)
            yield m
        ]

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
            operations |> List.collect (fun op ->
                let skipLength = if String.IsNullOrEmpty clientName then 0 else clientName.Length + 1
                let name = OperationCompiler.GetMethodNameCandidate op skipLength ignoreOperationId
                compileOperation (methodNameScope.MakeUnique name) op)
            |> ty.AddMembers
        )
