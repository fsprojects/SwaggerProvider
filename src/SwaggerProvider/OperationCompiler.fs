namespace SwaggerProvider.Internal.Compilers

open ProviderImplementation.ProvidedTypes
open FSharp.Data.Runtime.NameUtils
open SwaggerProvider.Internal.Schema
open SwaggerProvider.OptionConverter

open System
open FSharp.Data
open Newtonsoft.Json

open Microsoft.FSharp.Quotations.ExprShape
open Microsoft.FSharp.Quotations
open System.Text.RegularExpressions

/// Object for compiling operations.
type OperationCompiler (schema:SwaggerSchema, defCompiler:DefinitionCompiler, headers : seq<string*string>) =

    let operationGroups =
        schema.Operations
        |> Seq.groupBy (fun op->
            if op.Tags.Length > 0
            then op.Tags.[0] else "Root")
        |> Seq.toList

    let compileOperation (op:OperationObject) =
        let parameters =
            [let required, optional = op.Parameters |> Array.partition (fun x->x.Required)
             for x in Array.append required optional ->
                ProvidedParameter(x.Name, defCompiler.CompileTy x.Type x.Required)]
        let retTy =
            let okResponse =
                op.Responses |> Array.tryFind (fun x->
                    (x.StatusCode.IsSome && x.StatusCode.Value = 200) || x.StatusCode.IsNone)
            match okResponse with
            | Some resp ->
                match resp.Schema with
                | None -> typeof<unit>
                | Some ty -> defCompiler.CompileTy ty true
            | None -> typeof<unit>
        let m = ProvidedMethod(nicePascalName op.OperationId, parameters, retTy, IsStaticMethod = true)
        m.AddXmlDoc(op.Summary)
        m.InvokeCode <- fun args ->
            let scheme =
                match schema.Schemes with 
                | [||]  -> "http" // Should use the scheme used to access the Swagger definition itself. 
                | array -> array.[0]
            let basePath = scheme + "://" + schema.Host + schema.BasePath

            // Fit headers into quotation
            let h1 = List.map 
                        (fun (h1,h2) -> Quotations.Expr.NewTuple [(<@@ h1 @@>); (<@@ h2 @@>)]) 
                        (Seq.toList headers)
            let h2 = Quotations.Expr.NewArray (typeof<Tuple<string,string>>, h1)

            // Locates parameters matching the arguments
            let parameters = 
                List.map (
                    function
                        | ShapeVar v as ex -> Array.find (fun (parameter : OperationParameter) -> parameter.Name = v.Name) op.Parameters, 
                                              ex
                        | _                -> failwith ("Function '" + m.Name + "' does not support functions as arguments.")
                    )
                    args

            // Makes argument a string // TODO: Make body an exception
            let coerceString defType (format : CollectionFormat) exp =
                match defType with
                | Array String
                | Array (Enum _) ->
                    <@@ Array.fold 
                            (fun state str -> state + str + ",") //format.ToString())
                            ""
                            (%%exp : string[])
                    @@>
//                    <@@ let mutable str = ""
//                        for a in (%%exp : string[]) do
//                            str <- str + a + ","
//                        str.Substring(0, str.Length - 1)
//                    @@>
                | _ -> Expr.Coerce (exp, typeof<string>)
            
            let replacePathTemplate path name (exp : Expr) =
                let template = "{" + name + "}"
                <@@ Regex.Replace(%%path, template, string (%%exp : string)) @@>

            let addPayload load (param : OperationParameter) (exp : Expr) =
                let name = param.Name
                let var = coerceString param.Type param.CollectionFormat exp
                match load with
                | Some (FormData, b) -> Some (FormData, <@@ Seq.append %%b [name, (%%var : string)] @@>)
                | None               -> match param.In with
                                        | Body -> Some (Body, Expr.Coerce (exp, typeof<obj>))
                                        | _    -> Some (FormData, <@@ (seq [name, (%%var : string)]) @@>)
                | _                  -> failwith ("Can only contain one payload")

            let addQuery quer name (exp : Expr) =
                <@@ List.append (%%quer : (string*string) list) [name, (%%exp : string)] @@>

            let addHeader head name (exp : Expr) =
                <@@ Array.append (%%head : (string*string) []) ([|name, (%%exp : string)|]) @@>

            // Partitions arguments based on their locations
            let (path, payload, queries, heads) = 
                let mPath = op.Path
                List.fold (
                    fun (path, load, quer, head) (param : OperationParameter, exp) -> 
                        let name = param.Name
                        let value = coerceString param.Type param.CollectionFormat exp
                        match param.In with
                        | Path   -> (replacePathTemplate path name value, load, quer, head)
                        | FormData
                        | Body   -> (path, addPayload load param exp, quer, head)
                        | Query  -> (path, load, addQuery quer name value, head)
                        | Header -> (path, load, quer, addHeader head name value)
                    )
                    (<@@ mPath @@>, None, <@@ ([("","")] : (string*string) list)  @@>, h2)
                    parameters
                    
            let address = <@@ basePath + %%path @@>
            let restCall = op.Type.ToString()
            
            // Make HTTP call
            let result = 
                match payload with
                | None               -> <@@ Http.RequestString(%%address, httpMethod = restCall, headers = (%%heads : array<string*string>), query = (%%queries : (string * string) list)) @@>
                | Some (FormData, b) -> <@@ Http.RequestString(%%address, httpMethod = restCall, headers = (%%heads : array<string*string>), body = HttpRequestBody.FormValues (%%b : seq<string * string>), query = (%%queries : (string * string) list)) @@>
                | Some (Body, b)     ->
                    <@@ let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
                        settings.Converters.Add(new OptionConverter () :> JsonConverter) 
                        let data = (%%b : obj)
                        let body = (JsonConvert.SerializeObject(data, settings)).ToLower()
                        Http.RequestString(%%address, httpMethod = restCall, headers = (%%heads : array<string*string>), body = HttpRequestBody.TextRequest body, query = (%%queries : (string * string) list)) 
                    @@>
                | Some (x, _) -> failwith ("Payload should not be able to have type: " + string x)
            
            // Return deserialized object
            <@@ 
                let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented)
                settings.Converters.Add(new OptionConverter () :> JsonConverter) 
                JsonConvert.DeserializeObject((%%result : string), retTy, settings) 
            @@>

        m

    /// Compiles the operation.
    member __.Compile() =
        operationGroups
        |> List.map (fun (tag, operations) ->
            let ty = ProvidedTypeDefinition(nicePascalName tag, Some typeof<obj>, IsErased = false)
            operations
            |> Seq.map compileOperation
            |> Seq.toList
            |> ty.AddMembers
            ty
        )