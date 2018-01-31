namespace Swagger.Internal

open System
open Newtonsoft.Json
open Swagger.Serialization
open System.Threading.Tasks
open System.Net.Http

type ProvidedSwaggerBaseType (host:string) =
    member val Host = host with get, set
    member val Headers = Array.empty<string*string> with get, set
    member val CustomizeHttpRequest = (id: HttpRequestMessage -> HttpRequestMessage) with get, set

type AsyncExtensions () =
    static member cast<'t> asyncOp = async {
        let! ret = asyncOp
        let cast = box ret
        return cast :?> 't
    }

type TaskExtensions () =
    static member cast<'t> (task: Task<obj>): Task<'t> = task.ContinueWith(fun (t: Task<obj>) -> t.Result :?> 't)

module RuntimeHelpers =
    /// initialize a static httpclient because they are stateless as long as we don't mutate the DefaultHttpHeaders property. See https://aspnetmonsters.com/2016/08/2016-08-27-httpclientwrong/ and a billion other articles for why
    ///
    /// NOTE: DNS records for this HttpClient instance will remain stagnant once retrieved. This can be bad. We should probably convert this to some kind of factory function that swaps instances after a bit.
    let httpClient = new HttpClient()

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

    let serialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore)
        settings.Converters.Add(OptionConverter () :> JsonConverter)
        settings.Converters.Add(ByteArrayConverter () :> JsonConverter)
        fun (value:obj) ->
            JsonConvert.SerializeObject(value, settings)

    let deserialize =
        let settings = JsonSerializerSettings(NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented)
        settings.Converters.Add(OptionConverter () :> JsonConverter)
        settings.Converters.Add(ByteArrayConverter () :> JsonConverter)
        fun value (retTy:Type) ->
            JsonConvert.DeserializeObject(value, retTy, settings)

    let combineUrl (urlA:string) (urlB:string) =
        sprintf "%s/%s" (urlA.TrimEnd('/')) (urlB.TrimStart('/'))

    let asyncCast =
        let castFn = typeof<AsyncExtensions>.GetMethod("cast")
        fun runtimeTy (asyncOp: Async<obj>) ->
            castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|asyncOp|])

    let taskCast =
        let castFn = typeof<TaskExtensions>.GetMethod("cast")
        fun runtimeTy (task: Task<obj>) ->
            castFn.MakeGenericMethod([|runtimeTy|]).Invoke(null, [|task|])
