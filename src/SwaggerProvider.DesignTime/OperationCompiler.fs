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
open System.IO

type BodyType =
| BodyForm of Expr<(string*string) seq>
| BodyObject of Expr<obj>
| BodyMultipart of Expr<(string*string*Stream) seq>

/// Object for compiling operations.
type OperationCompiler (schema:SwaggerObject, defCompiler:DefinitionCompiler) =

    let compileOperation (methodName:string) (op:OperationObject) =
        if String.IsNullOrWhiteSpace methodName
            then failwithf "Operation name could not be empty. See '%s/%A'" op.Path op.Type

        let parameters =
            [
             let required, optional = op.Parameters |> Array.partition (fun x->x.Required)
             let parameters = Array.append required optional
             for x in parameters ->
                let paramName = niceCamelName x.Name
                let paramType = defCompiler.CompileTy methodName paramName x.Type x.Required
                if x.Required then ProvidedParameter(paramName, paramType)
                else
                    let paramDefaultValue = defCompiler.GetDefaultValue x.Type
                    ProvidedParameter(paramName, paramType, false, paramDefaultValue)
            ]
        let retTy, isReturnFile =
            let okResponse = // BUG :  wrong selector
                op.Responses |> Array.tryFind (fun (code, resp) ->
                    (code.IsSome && (code.Value = 200 || code.Value = 201)) || code.IsNone)
            match okResponse with
            | Some (_,resp) ->
                match resp.Schema with
                | None -> typeof<unit>, false
                | Some ty when ty = SchemaObject.File ->
                    typeof<IO.Stream>, true
                | Some ty ->
                    defCompiler.CompileTy methodName "Response" ty true, false
            | None -> typeof<unit>, false

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
                let jsonConsumable = op.Consumes |> Seq.exists (fun mt -> mt="application/json")
                <@
                    let headersArr = (%%headers:(string*string)[])
                    let ctHeaderExist = headersArr |> Array.exists (fun (h,_)->h="Content-Type")
                    if not(ctHeaderExist) && jsonConsumable
                    then Array.append [|"Content-Type","application/json"|] headersArr
                    else headersArr
                @>
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

            let rec coerceQueryString name expr =
                let obj = Expr.Coerce(expr, typeof<obj>)
                <@ let o = (%%obj : obj)
                   SwaggerProvider.Internal.RuntimeHelpers.toQueryParams name o @>

            let replacePathTemplate path name (exp: Expr<string>) =
                let template = sprintf "{%s}" name
                <@ Regex.Replace(%path, template, %exp) @>

            let createStream (stringData: string) =
                stringData |> Text.Encoding.UTF8.GetBytes |> MemoryStream :> Stream

            let addPayload load (param : ParameterObject) (exp : Expr) =
                let name = param.Name
                // delay the string coercion so that if it's a stream we don't tostring it.
                let var () = coerceString param.Type param.CollectionFormat exp
                match load with
                | Some (BodyForm f) ->
                    Some (BodyForm <@ Seq.append %f [name, %var()] @>)
                | Some (BodyForm f) when param.Type = SchemaObject.File ->
                    // have to convert existing form items to streams when we hit a file so that multipart upload can occur
                    let prevs = <@ %f |> Seq.map (fun (k,v) -> k, k, createStream(v)) @>
                    let wrapper = Expr.Coerce (exp, typeof<FileWrapper>) |> Expr.Cast<FileWrapper>
                    Some (BodyMultipart <@ Seq.append %prevs [name, (%wrapper).fileName, (%wrapper).data] @>)
                | Some (BodyMultipart f) ->
                    Some (BodyMultipart <@ Seq.append %f [name, name, createStream(%var())] @>)
                | None when param.Type = SchemaObject.File ->
                    let wrapper = Expr.Coerce (exp, typeof<FileWrapper>) |> Expr.Cast<FileWrapper>
                    Some(BodyMultipart <@ Seq.singleton (name, (%wrapper).fileName, (%wrapper).data) @>)
                | None               ->
                    match param.In with
                    | Body ->
                        Some (BodyObject (Expr.Coerce (exp, typeof<obj>) |> Expr.Cast<obj>))
                    | _    ->
                        Some (BodyForm <@ (seq [name, (%var() : string)]) @>)
                | _                  -> failwith ("Can only contain one payload")

            let addQuery quer name (exp : Expr) =
                let listValues = coerceQueryString name exp
                <@ List.append %quer %listValues @>

            let addHeader head name (exp : Expr<string>) =
                <@ Array.append %head ([| name, %exp |]) @>

            // Partitions arguments based on their locations
            let (path, payload, queries: Expr<(string*string) list>, heads) =
                let mPath = op.Path
                parameters
                |> List.fold (
                    fun (path, load, quer, head) (param : ParameterObject, exp) ->
                        let name = param.Name
                        let value = coerceString param.Type param.CollectionFormat exp
                        match param.In with
                        | Path   -> (replacePathTemplate path name value, load, quer, head)
                        | FormData | Body -> (path, addPayload load param exp, quer, head)
                        | Query  -> (path, load, addQuery quer name exp, head)
                        | Header -> (path, load, quer, addHeader head name value)
                    )
                    (<@ mPath @>, None, <@ ([] : (string*string) list)  @>, headers)


            let address = <@ SwaggerProvider.Internal.RuntimeHelpers.combineUrl %basePath %path @>
            let restCall = op.Type.ToString()

            let customizeHttpRequest =
                <@ let customizeCall = %%customizeHttpRequest
                   fun (request:Net.HttpWebRequest) ->
                        if restCall = "Post"
                            then request.ContentLength <- 0L
                        customizeCall request @>

            // Make HTTP call
            let result =
                match payload with
                | None ->
                    <@ Http.RequestStream(%address,
                            httpMethod = restCall,
                            headers = %heads,
                            query = %queries,
                            customizeHttpRequest = %customizeHttpRequest) @>
                | Some (BodyMultipart parts) ->
                    <@ Http.RequestStream(%address,
                            httpMethod = restCall,
                            body = HttpRequestBody.Multipart(string (Guid.NewGuid()), %parts),
                            headers = %heads,
                            query = %queries,
                            customizeHttpRequest = %customizeHttpRequest) @>
                | Some (BodyForm formData) ->
                    <@ Http.RequestStream(%address,
                            httpMethod = restCall,
                            headers = %heads,
                            body = HttpRequestBody.FormValues %formData,
                            query = %queries,
                            customizeHttpRequest = %customizeHttpRequest) @>
                | Some (BodyObject b) ->
                    <@  let body = SwaggerProvider.Internal.RuntimeHelpers.serialize %b
                        Http.RequestStream(%address,
                            httpMethod = restCall,
                            headers = %heads,
                            body = HttpRequestBody.TextRequest body,
                            query = %queries,
                            customizeHttpRequest = %customizeHttpRequest) @>
            // Return deserialized object

            let value =
                <@@
                    let stream = (%result).ResponseStream
                    if isReturnFile
                    then box stream
                    else
                        let reader = new StreamReader(stream)
                        let body = reader.ReadToEnd()
                        let ret = box <| SwaggerProvider.Internal.RuntimeHelpers.deserialize body retTy
                        reader.Close()
                        stream.Close()
                        ret @@>
            Expr.Coerce(value, retTy)

        m

    /// Compiles the operation.
    member __.CompilePaths(ignoreOperationId) =
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
            let methodNameCandidate = nicePascalName <| getMethodNameCandidate op
            compileOperation (methodNameScope.MakeUnique methodNameCandidate) op)
