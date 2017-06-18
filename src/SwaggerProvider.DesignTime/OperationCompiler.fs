namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal
open SwaggerProvider.Internal.Schema

open System
open FSharp.Data

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.ExprShape
open System.Text.RegularExpressions

type CompiledOperationType = | Synchronous | Asynchronous | Task

/// Object for compiling operations.
type OperationCompiler (schema:SwaggerObject, defCompiler:DefinitionCompiler) =
    let openAsync = typeof<Async<_>>.GetGenericTypeDefinition()
    let openTask = typeof<System.Threading.Tasks.Task<_>>.GetGenericTypeDefinition()

    let compileOperation (methodName:string) (op:OperationObject) (compileType : CompiledOperationType) =
        if String.IsNullOrWhiteSpace methodName
            then failwithf "Operation name could not be empty. See '%s/%A'" op.Path op.Type

        let parameters =
            [let required, optional = op.Parameters |> Array.partition (fun x->x.Required)
             for x in Array.append required optional ->
                let paramName = niceCamelName x.Name
                ProvidedParameter(paramName, defCompiler.CompileTy methodName paramName x.Type x.Required)]
        let okResponse = // BUG : wrong selector
                (op.Responses 
                |> Array.tryFind (fun (code, resp) -> (code.IsSome && (code.Value = 200 || code.Value = 201)) || code.IsNone)
                |> Option.bind (fun (i, resp) -> resp.Schema))
        let retTy =
            let underlyingType = 
                match okResponse with
                | None -> typeof<unit>
                | Some t -> defCompiler.CompileTy methodName "Response" t true
            
            match compileType with
            | Synchronous -> underlyingType
            | Asynchronous -> openAsync.MakeGenericType([| underlyingType |])
            | Task -> openTask.MakeGenericType([| underlyingType |])

        let m = ProvidedMethod(methodName, parameters, retTy)
        if not <| String.IsNullOrEmpty(op.Summary)
            then m.AddXmlDoc(op.Summary) // TODO: Use description of parameters in docs
        if op.Deprecated
            then m.AddObsoleteAttribute("Operation is deprecated", false)

        m.InvokeCode <- fun args ->
            let thisTy = typeof<SwaggerProvider.Internal.ProvidedSwaggerBaseType>
            let this = Expr.Coerce(args.[0], thisTy)
            let host = Expr.PropertyGet(this, thisTy.GetProperty("Host"))
            let headers = Expr.PropertyGet(this, thisTy.GetProperty("Headers"))
            let customizeHttpRequest = Expr.PropertyGet(this, thisTy.GetProperty("CustomizeHttpRequest"))

            let basePath =
                let basePath = schema.BasePath
                <@ SwaggerProvider.Internal.RuntimeHelpers.combineUrl (%%host : string) basePath @>

            // Fit headers into quotation
            let headers =
                let jsonConsumable = op.Consumes |> Seq.exists (fun mt -> mt="application/json") // todo: what about text/json and other silly json encodings?
                <@@
                    let headersArr = (%%headers:(string*string)[])
                    let ctHeaderExist = headersArr |> Array.exists (fun (h,_)->h="Content-Type")
                    if not(ctHeaderExist) && jsonConsumable
                    then Array.append [|"Content-Type","application/json"|] headersArr
                    else headersArr
                @@>
                //let headerPairs =
                //    seq {
                //        yield! headers
                //        if (headers |> Seq.exists (fun (h,_)->h="Content-Type") |> not) then
                //            if (op.Consumes |> Seq.exists (fun mt -> mt="application/json")) then
                //                yield "Content-Type","application/json"
                //    }
                //    |> List.ofSeq
                //    |> List.map (fun (h1,h2) -> Expr.NewTuple [Expr.Value(h1);Expr.Value(h2)])
                //Expr.NewArray (typeof<Tuple<string,string>>, headerPairs)

            // Locates parameters matching the arguments
            let parameters =
                List.tail args // skip `this` param
                |> List.map (function
                    | ShapeVar sVar as expr ->
                        let param =
                            op.Parameters
                            |> Array.find (fun x -> niceCamelName x.Name = sVar.Name) // ???
                        param, expr
                    | _  ->
                        failwithf "Function '%s' does not support functions as arguments." m.Name
                    )


            // Makes argument a string // TODO: Make body an exception
            let coerceString defType (format : CollectionFormat) exp =
                let obj = Expr.Coerce(exp, typeof<obj>)
                <@ (%%obj : obj).ToString() @>

            let rec corceQueryString name expr =
                let obj = Expr.Coerce(expr, typeof<obj>)
                <@ let o = (%%obj : obj)
                   SwaggerProvider.Internal.RuntimeHelpers.toQueryParams name o @>

            let replacePathTemplate path name (exp : Expr) =
                let template = "{" + name + "}"
                <@@ Regex.Replace(%%path, template, string (%%exp : string)) @@>

            let addPayload load (param : ParameterObject) (exp : Expr) =
                let name = param.Name
                let var = coerceString param.Type param.CollectionFormat exp
                match load with
                | Some (FormData, b) -> Some (FormData, <@@ Seq.append %%b [name, (%var : string)] @@>)
                | None               -> match param.In with
                                        | Body -> Some (Body, Expr.Coerce (exp, typeof<obj>))
                                        | _    -> Some (FormData, <@@ (seq [name, (%var : string)]) @@>)
                | _                  -> failwith ("Can only contain one payload")

            let addQuery quer name (exp : Expr) =
                let listValues = corceQueryString name exp
                <@@ List.append
                        (%%quer : (string*string) list)
                        (%listValues : (string*string) list) @@>

            let addHeader head name (exp : Expr) =
                <@@ Array.append (%%head : (string*string) []) ([|name, (%%exp : string)|]) @@>

            // Partitions arguments based on their locations
            let (path, payload, queries, heads) =
                let mPath = op.Path
                parameters
                |> List.fold (
                    fun (path, load, quer, head) (param : ParameterObject, exp) ->
                        let name = param.Name
                        let value = coerceString param.Type param.CollectionFormat exp
                        match param.In with
                        | Path   -> (replacePathTemplate path name value, load, quer, head)
                        | FormData
                        | Body   -> (path, addPayload load param exp, quer, head)
                        | Query  -> (path, load, addQuery quer name exp, head)
                        | Header -> (path, load, quer, addHeader head name value)
                    )
                    (<@@ mPath @@>, None, <@@ ([] : (string*string) list)  @@>, headers)


            let address = <@@ SwaggerProvider.Internal.RuntimeHelpers.combineUrl %basePath (%%path :string ) @@>
            let restCall = op.Type.ToString()

            let customizeHttpRequest =
                <@@ let customizeCall = (%%customizeHttpRequest : Net.HttpWebRequest -> Net.HttpWebRequest)
                    fun (request:Net.HttpWebRequest) ->
                        if restCall = "Post"
                            then request.ContentLength <- 0L
                        customizeCall request @@>

            // Make HTTP call
            let asyncResult =
                match payload with
                | None ->
                    <@ Http.AsyncRequestString(%%address,
                            httpMethod = restCall,
                            headers = (%%heads : array<string*string>),
                            query = (%%queries : (string * string) list),
                            customizeHttpRequest = (%%customizeHttpRequest : Net.HttpWebRequest -> Net.HttpWebRequest)) @>
                | Some (FormData, b) ->
                    <@ Http.AsyncRequestString(%%address,
                            httpMethod = restCall,
                            headers = (%%heads : array<string*string>),
                            body = HttpRequestBody.FormValues (%%b : seq<string * string>),
                            query = (%%queries : (string * string) list),
                            customizeHttpRequest = (%%customizeHttpRequest : Net.HttpWebRequest -> Net.HttpWebRequest)) @>
                | Some (Body, b)     ->
                    <@ 
                        let body = SwaggerProvider.Internal.RuntimeHelpers.serialize (%%b : obj)
                        Http.AsyncRequestString(%%address,
                            httpMethod = restCall,
                            headers = (%%heads : array<string*string>),
                            body = HttpRequestBody.TextRequest body,
                            query = (%%queries : (string * string) list),
                            customizeHttpRequest = (%%customizeHttpRequest : Net.HttpWebRequest -> Net.HttpWebRequest)) @>
                | Some (x, _) -> failwith ("Payload should not be able to have type: " + string x)

            let result =
                match compileType with
                | Synchronous -> 
                    <@@ 
                        let result = %asyncResult |> Async.RunSynchronously
                        SwaggerProvider.Internal.RuntimeHelpers.deserialize result retTy 
                    @@>
                | Asynchronous -> 
                    <@@
                        async {
                            let! result = %asyncResult
                            return SwaggerProvider.Internal.RuntimeHelpers.deserialize result retTy
                        }
                    @@>
                | Task -> 
                    <@@
                        async {
                            let! result = %asyncResult
                            return SwaggerProvider.Internal.RuntimeHelpers.deserialize result retTy
                        } |> Async.StartAsTask
                    @@>

            Expr.Coerce(result, retTy)

        m

    /// Compiles the operation.
    member __.CompilePaths(ignoreOperationId, compileType) =
        let methodNameScope = UniqueNameGenerator()
        let pathToName opType (opPath:String) =
            String.Join("_",
                [|
                    yield opType.ToString()
                    yield!
                        opPath.Split('/')
                        |> Array.filter (fun x ->
                            not <| (String.IsNullOrEmpty(x) || x.StartsWith("{")))
                |])
        let getMethodNameCandidate (op:OperationObject) =
            if ignoreOperationId || String.IsNullOrWhiteSpace(op.OperationId)
            then pathToName op.Type op.Path
            else op.OperationId

        List.ofArray schema.Paths
        |> List.map (fun op ->
            let uniqueMethodName = getMethodNameCandidate op |> nicePascalName |> methodNameScope.MakeUnique
            compileOperation uniqueMethodName op compileType)
