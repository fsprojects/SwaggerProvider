namespace Swagger.Internal

open System
open Newtonsoft.Json
open System.Threading.Tasks
open System.Net.Http

type AsyncExtensions () =
    static member cast<'t> asyncOp = async {
        let! ret = asyncOp
        let cast = box ret
        return cast :?> 't
    }

type TaskExtensions () =
    static member cast<'t> (task: Task<obj>): Task<'t> = task.ContinueWith(fun (t: Task<obj>) -> t.Result :?> 't)

module RuntimeHelpers =
    let inline private toStrArray name values =
        values
        |> Array.map (fun value-> name, value.ToString())
        |> Array.toList

    let inline private toStrArrayOpt name values =
        values
        |> Array.choose (id)
        |> toStrArray name

    let inline private toStrOpt name value =
        match value with
        | Some(x) -> [name, x.ToString()]
        | None ->[]

    let toQueryParams (name:string) (obj:obj) =
        match obj with
        | :? array<byte> as xs -> xs |> toStrArray name
        | :? array<bool> as xs -> xs |> toStrArray name
        | :? array<int32> as xs -> xs |> toStrArray name
        | :? array<int64> as xs -> xs |> toStrArray name
        | :? array<float32> as xs -> xs |> toStrArray name
        | :? array<double> as xs -> xs |> toStrArray name
        | :? array<string> as xs -> xs |> toStrArray name
        | :? array<DateTime> as xs -> xs |> toStrArray name
        | :? array<Option<bool>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int64>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<float32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<double>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<string>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<DateTime>> as xs -> xs |> toStrArray name
        | :? Option<bool> as x -> x |> toStrOpt name
        | :? Option<int32> as x -> x |> toStrOpt name
        | :? Option<int64> as x -> x |> toStrOpt name
        | :? Option<float32> as x -> x |> toStrOpt name
        | :? Option<double> as x -> x |> toStrOpt name
        | :? Option<string> as x -> x |> toStrOpt name
        | :? Option<DateTime> as x -> x |> toStrOpt name
        | _ -> [name, obj.ToString()]

    let getPropertyNameAttribute name =
        { new Reflection.CustomAttributeData() with
            member __.Constructor =  typeof<JsonPropertyAttribute>.GetConstructor([|typeof<string>|])
            member __.ConstructorArguments = [|Reflection.CustomAttributeTypedArgument(typeof<string>, name)|] :> Collections.Generic.IList<_>
            member __.NamedArguments = [||] :> Collections.Generic.IList<_> }

    let toStringContent (valueStr:string) =
        new StringContent(valueStr, Text.Encoding.UTF8, "application/json")
    let toMultipartFormDataContent (keyValues:seq<string*string>) =
        let cnt = new MultipartFormDataContent()
        for (k,v) in keyValues do
            cnt.Add(toStringContent v, k)
        cnt
    let toFormUrlEncodedContent (keyValues:seq<string*string>) =
        let keyValues = keyValues |> Seq.map Collections.Generic.KeyValuePair
        new FormUrlEncodedContent(keyValues)

    let getDefaultHttpClient host =
        // Using default handler with UseCookies=true, HttpClient will not be able to set Cookie-based parameters
        let handler = new HttpClientHandler (UseCookies = false)
        if isNull host 
        then new HttpClient(handler, true)
        else new HttpClient(handler, true, BaseAddress=Uri(host))

    let combineUrl (urlA:string) (urlB:string) =
        sprintf "%s/%s" (urlA.TrimEnd('/')) (urlB.TrimStart('/'))

    let fillHeaders (msg:HttpRequestMessage) (heads:(string*string) seq) =
        for (name, value) in heads do
            if not <| msg.Headers.TryAddWithoutValidation(name, value) then
                let errMsg = String.Format("Cannot add header '{0}'='{1}' to HttpRequestMessage", name, value)
                if (name <> "Content-Type") then
                    raise <| System.Exception(errMsg)

    let asyncCast runtimeTy (asyncOp: Async<obj>) =
        let castFn = typeof<AsyncExtensions>.GetMethod("cast")
        castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|asyncOp|])

    let taskCast runtimeTy (task: Task<obj>) =
        let castFn = typeof<TaskExtensions>.GetMethod("cast")
        castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|task|])
