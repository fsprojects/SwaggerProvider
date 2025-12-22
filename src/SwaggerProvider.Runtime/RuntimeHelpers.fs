namespace Swagger.Internal

open System
open System.Net.Http
open System.Text.Json.Serialization
open System.Threading.Tasks

module MediaTypes =
    [<Literal>]
    let ApplicationJson = "application/json"

    [<Literal>]
    let ApplicationOctetStream = "application/octet-stream"

    [<Literal>]
    let ApplicationFormUrlEncoded = "application/x-www-form-urlencoded"

    [<Literal>]
    let MultipartFormData = "multipart/form-data"

    [<Literal>]
    let TextPlain = "text/plain"

type AsyncExtensions() =
    static member cast<'t> asyncOp =
        async {
            let! ret = asyncOp
            return (box ret) :?> 't
        }

type TaskExtensions() =
    static member cast<'t> taskOp =
        task {
            let! ret = taskOp
            return (box ret) :?> 't
        }

module RuntimeHelpers =
    let inline private toStrArray name values =
        values
        |> Array.map(fun value -> name, value.ToString())
        |> Array.toList

    let inline private toStrArrayDateTime name (values: DateTime array) =
        values
        |> Array.map(fun value -> name, value.ToString("O"))
        |> Array.toList

    let inline private toStrArrayDateTimeOffset name (values: DateTimeOffset array) =
        values
        |> Array.map(fun value -> name, value.ToString("O"))
        |> Array.toList

    let inline private toStrArrayOpt name values =
        values |> Array.choose(id) |> toStrArray name

    let inline private toStrArrayDateTimeOpt name values =
        values |> Array.choose(id) |> toStrArrayDateTime name

    let inline private toStrArrayDateTimeOffsetOpt name values =
        values |> Array.choose(id) |> toStrArrayDateTimeOffset name


    let inline private toStrOpt name value =
        match value with
        | Some(x) -> [ name, x.ToString() ]
        | None -> []

    let inline private toStrDateTimeOpt name (value: DateTime option) =
        match value with
        | Some(x) -> [ name, x.ToString("O") ]
        | None -> []

    let inline private toStrDateTimeOffsetOpt name (value: DateTimeOffset option) =
        match value with
        | Some(x) -> [ name, x.ToString("O") ]
        | None -> []

    let toParam(obj: obj) =
        match obj with
        | :? DateTime as dt -> dt.ToString("O")
        | :? DateTimeOffset as dto -> dto.ToString("O")
        | null -> null
        | _ -> obj.ToString()

    let toQueryParams (name: string) (obj: obj) (client: Swagger.ProvidedApiClientBase) =
        match obj with
        | :? array<byte> as xs -> [ name, (client.Serialize xs).Trim('\"') ] // TODO: Need to verify how servers parse byte[] from query string
        | :? array<bool> as xs -> xs |> toStrArray name
        | :? array<int32> as xs -> xs |> toStrArray name
        | :? array<int64> as xs -> xs |> toStrArray name
        | :? array<float32> as xs -> xs |> toStrArray name
        | :? array<double> as xs -> xs |> toStrArray name
        | :? array<string> as xs -> xs |> toStrArray name
        | :? array<DateTime> as xs -> xs |> toStrArrayDateTime name
        | :? array<DateTimeOffset> as xs -> xs |> toStrArrayDateTimeOffset name
        | :? array<Guid> as xs -> xs |> toStrArray name
        | :? array<Option<bool>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<int64>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<float32>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<double>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<string>> as xs -> xs |> toStrArrayOpt name
        | :? array<Option<DateTime>> as xs -> xs |> toStrArrayDateTimeOpt name
        | :? array<Option<DateTimeOffset>> as xs -> xs |> toStrArrayDateTimeOffsetOpt name
        | :? array<Option<Guid>> as xs -> xs |> toStrArray name
        | :? Option<bool> as x -> x |> toStrOpt name
        | :? Option<int32> as x -> x |> toStrOpt name
        | :? Option<int64> as x -> x |> toStrOpt name
        | :? Option<float32> as x -> x |> toStrOpt name
        | :? Option<double> as x -> x |> toStrOpt name
        | :? Option<string> as x -> x |> toStrOpt name
        | :? Option<DateTime> as x -> x |> toStrDateTimeOpt name
        | :? Option<DateTimeOffset> as x -> x |> toStrDateTimeOffsetOpt name
        | :? DateTime as x -> [ name, x.ToString("O") ]
        | :? DateTimeOffset as x -> [ name, x.ToString("O") ]
        | :? Option<Guid> as x -> x |> toStrOpt name
        | _ -> [ name, (if isNull obj then null else obj.ToString()) ]

    let getPropertyNameAttribute name =
        { new Reflection.CustomAttributeData() with
            member _.Constructor =
                typeof<JsonPropertyNameAttribute>.GetConstructor [| typeof<string> |]

            member _.ConstructorArguments =
                [| Reflection.CustomAttributeTypedArgument(typeof<string>, name) |] :> Collections.Generic.IList<_>

            member _.NamedArguments = [||] :> Collections.Generic.IList<_> }

    let toStringContent(valueStr: string) =
        new StringContent(valueStr, Text.Encoding.UTF8, "application/json")

    let toTextContent(valueStr: string) =
        new StringContent(valueStr, Text.Encoding.UTF8, "text/plain")

    let toStreamContent(boxedStream: obj) =
        match boxedStream with
        | :? IO.Stream as stream -> new StreamContent(stream)
        | _ -> failwith $"Unexpected parameter type {boxedStream.GetType().Name} instead of IO.Stream"

    let getPropertyValues(object: obj) =
        if isNull object then
            Seq.empty
        else
            object
                .GetType()
                .GetProperties(
                    System.Reflection.BindingFlags.Public
                    ||| System.Reflection.BindingFlags.Instance
                )
            |> Seq.choose(fun prop ->
                let name =
                    match prop.GetCustomAttributes(typeof<JsonPropertyNameAttribute>, false) with
                    | [| x |] -> (x :?> JsonPropertyNameAttribute).Name
                    | _ -> prop.Name

                prop.GetValue(object)
                |> Option.ofObj
                |> Option.map(fun value -> (name, value)))

    let toMultipartFormDataContent(keyValues: seq<string * obj>) =
        let cnt = new MultipartFormDataContent()

        let addFileStream name (stream: IO.Stream) =
            let filename = Guid.NewGuid().ToString() // asp.net core cannot deserialize IFormFile otherwise
            cnt.Add(new StreamContent(stream), name, filename)

        for name, value in keyValues do
            match value with
            | null -> ()
            | :? IO.Stream as stream -> addFileStream name stream
            | :? (IO.Stream[]) as streams -> streams |> Seq.iter(addFileStream name)
            | x ->
                let strValue = x.ToString() // TODO: serialize? does not work with arrays probably
                cnt.Add(toStringContent strValue, name)

        cnt

    let toFormUrlEncodedContent(keyValues: seq<string * obj>) =
        let keyValues =
            keyValues
            |> Seq.filter(snd >> isNull >> not)
            |> Seq.map(fun (k, v) -> Collections.Generic.KeyValuePair(k, v.ToString()))

        new FormUrlEncodedContent(keyValues)

    let getDefaultHttpClient(host: string) =
        // Using default handler with UseCookies=true, HttpClient will not be able to set Cookie-based parameters
        let handler = new HttpClientHandler(UseCookies = false)

        if isNull host then
            new HttpClient(handler, true)
        else
            let host = if host.EndsWith("/") then host else host + "/"

            new HttpClient(handler, true, BaseAddress = Uri(host))

    let combineUrl (urlA: string) (urlB: string) =
        sprintf "%s/%s" (urlA.TrimEnd('/')) (urlB.TrimStart('/'))

    let createHttpRequest (httpMethod: string) address queryParams =
        let requestUrl =
            let fakeHost = "http://fake-host/"
            let builder = UriBuilder(combineUrl fakeHost address)
            let query = System.Web.HttpUtility.ParseQueryString(builder.Query)

            for name, value in queryParams do
                if not <| isNull value then
                    query.Add(name, value)

            builder.Query <- query.ToString()
            builder.Uri.PathAndQuery.TrimStart('/')

        let method = HttpMethod(httpMethod.ToUpper())
        new HttpRequestMessage(method, Uri(requestUrl, UriKind.Relative))

    let fillHeaders (msg: HttpRequestMessage) (headers: (string * string) seq) =
        headers
        |> Seq.filter(snd >> isNull >> not)
        |> Seq.iter(fun (name, value) ->
            if not <| msg.Headers.TryAddWithoutValidation(name, value) then
                let errMsg =
                    String.Format("Cannot add header '{0}'='{1}' to HttpRequestMessage", name, value)

                if (name <> "Content-Type") then
                    raise <| Exception(errMsg))

    let asyncCast runtimeTy (asyncOp: Async<obj>) =
        let castFn = typeof<AsyncExtensions>.GetMethod "cast"

        castFn.MakeGenericMethod([| runtimeTy |]).Invoke(null, [| asyncOp |])

    let taskCast runtimeTy (task: Task<obj>) =
        let castFn = typeof<TaskExtensions>.GetMethod "cast"

        castFn.MakeGenericMethod([| runtimeTy |]).Invoke(null, [| task |])
