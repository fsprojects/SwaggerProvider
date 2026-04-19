namespace Swagger.Internal

open System
open System.Net.Http
open System.Net.Http.Headers
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

    let private dateOnlyTypeName = "System.DateOnly"

    let private isDateOnlyType(t: Type) =
        not(isNull t) && t.FullName = dateOnlyTypeName

    let private isOptionOfDateOnlyType(t: Type) =
        t.IsGenericType
        && t.GetGenericTypeDefinition() = typedefof<option<_>>
        && isDateOnlyType(t.GetGenericArguments().[0])

    let private isDateOnlyLikeType(t: Type) =
        isDateOnlyType t || isOptionOfDateOnlyType t

    let private tryFormatDateOnly(value: obj) =
        if isNull value then
            None
        else
            let ty = value.GetType()

            if isDateOnlyType ty then
                match ty.GetMethod("ToString", [| typeof<string> |]) |> Option.ofObj with
                | Some methodInfo -> Some(methodInfo.Invoke(value, [| box "O" |]) :?> string)
                | None -> Some(value.ToString())
            else
                None

    let rec toParam(obj: obj) =
        match obj with
        | :? DateTime as dt -> dt.ToString("O")
        | :? DateTimeOffset as dto -> dto.ToString("O")
        | null -> null
        | _ ->
            match tryFormatDateOnly obj with
            | Some formatted -> formatted
            | None ->
                let ty = obj.GetType()

                // Unwrap F# Option<T>: Some(x) -> toParam(x), None -> null
                if
                    ty.IsGenericType
                    && ty.GetGenericTypeDefinition() = typedefof<option<_>>
                then
                    let (case, values) = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(obj, ty)

                    if case.Name = "Some" && values.Length > 0 then
                        toParam values.[0]
                    else
                        null
                else
                    obj.ToString()

    let toQueryParams (name: string) (obj: obj) (client: Swagger.ProvidedApiClientBase) =
        if isNull obj then
            []
        else

            match obj with
            | :? array<byte> as xs -> [ name, (client.Serialize xs).Trim('\"') ] // TODO: Need to verify how servers parse byte[] from query string
            | :? Option<array<byte>> as x ->
                match x with
                | Some xs -> [ name, (client.Serialize xs).Trim('\"') ]
                | None -> []
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
            | :? array<Option<Guid>> as xs -> xs |> toStrArrayOpt name
            | :? Array as xs when
                xs.GetType().GetElementType()
                |> Option.ofObj
                |> Option.exists isDateOnlyLikeType
                ->
                xs
                |> Seq.cast<obj>
                |> Seq.choose(fun value ->
                    let param = toParam value

                    if isNull param then None else Some(name, param))
                |> Seq.toList
            | _ ->
                let param = toParam obj
                if isNull param then [] else [ name, param ]

    /// Cache of sorted declared public instance properties per type, to avoid repeated
    /// reflection and sorting overhead when formatObject is called frequently.
    let private propCache =
        Collections.Concurrent.ConcurrentDictionary<Type, Reflection.PropertyInfo[]>()

    let private getProperties(t: Type) =
        propCache.GetOrAdd(
            t,
            fun ty ->
                ty.GetProperties(
                    Reflection.BindingFlags.Public
                    ||| Reflection.BindingFlags.Instance
                    ||| Reflection.BindingFlags.DeclaredOnly
                )
                |> Array.sortBy(fun p -> p.Name)
        )

    /// Cache of (serialized-name, PropertyInfo) pairs per type for getPropertyValues.
    /// Avoids repeated GetProperties + GetCustomAttributes calls on hot form-encoding paths.
    let private propNameCache =
        Collections.Concurrent.ConcurrentDictionary<Type, (string * Reflection.PropertyInfo)[]>()

    let private getPropertyNamesAndInfos(t: Type) =
        propNameCache.GetOrAdd(
            t,
            fun ty ->
                ty.GetProperties(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Instance)
                |> Array.map(fun prop ->
                    let name =
                        match prop.GetCustomAttributes(typeof<JsonPropertyNameAttribute>, false) with
                        | [| x |] -> (x :?> JsonPropertyNameAttribute).Name
                        | _ -> prop.Name

                    (name, prop))
        )

    /// Formats a generated API object as a string in the form `{Prop1=value1; Prop2=value2}`.
    /// Only declared public instance properties are included, sorted alphabetically by name.
    /// Used by the emitted ToString() override to keep the generated method body O(1) in size.
    let formatObject(obj: obj) : string =
        let props = getProperties(obj.GetType())

        let strs =
            props
            |> Array.map(fun p ->
                let v = p.GetValue(obj)

                let s =
                    if isNull v then
                        "null"
                    else
                        let vTy = v.GetType()

                        if vTy = typeof<string> then
                            String.Format("\"{0}\"", v)
                        elif vTy.IsArray then
                            let elements =
                                (v :?> Array)
                                |> Seq.cast<obj>
                                |> Seq.map(fun x -> if isNull x then "null" else x.ToString())
                                |> Array.ofSeq

                            String.Format("[{0}]", String.Join("; ", elements))
                        else
                            v.ToString()

                String.Format("{0}={1}", p.Name, s))

        String.Format("{{{0}}}", String.Join("; ", strs))

    // Cached constructor for JsonPropertyNameAttribute to avoid repeated reflection lookups
    // when compiling large schemas with many properties.
    let private jsonPropertyNameCtor =
        typeof<JsonPropertyNameAttribute>.GetConstructor [| typeof<string> |]

    let getPropertyNameAttribute name =
        { new Reflection.CustomAttributeData() with
            member _.Constructor = jsonPropertyNameCtor

            member _.ConstructorArguments =
                [| Reflection.CustomAttributeTypedArgument(typeof<string>, name) |] :> Collections.Generic.IList<_>

            member _.NamedArguments = [||] :> Collections.Generic.IList<_> }

    let toStringContent(valueStr: string) =
        new StringContent(valueStr, Text.Encoding.UTF8, "application/json")

    let toTextContent(valueStr: string) =
        new StringContent(valueStr, Text.Encoding.UTF8, "text/plain")

    let toStreamContent(boxedStream: obj, contentType: string) =
        match boxedStream with
        | :? IO.Stream as stream ->
            let content = new StreamContent(stream)

            if (not <| String.IsNullOrEmpty(contentType)) then
                content.Headers.ContentType <- MediaTypeHeaderValue(contentType)

            content
        | _ -> failwith $"Unexpected parameter type {boxedStream.GetType().Name} instead of IO.Stream"

    // Unwraps F# option values: returns the inner value for Some, null for None.
    // This prevents `Some(value)` from being sent as-is in form data.
    // The `Value` PropertyInfo is cached per concrete option type to avoid repeated reflection lookups.
    let private optionValuePropCache =
        Collections.Concurrent.ConcurrentDictionary<Type, Reflection.PropertyInfo>()

    let private unwrapFSharpOption(value: obj) : obj =
        if isNull value then
            null
        else
            let ty = value.GetType()

            if
                ty.IsGenericType
                && ty.GetGenericTypeDefinition() = typedefof<option<_>>
            then
                let prop = optionValuePropCache.GetOrAdd(ty, fun t -> t.GetProperty("Value"))
                prop.GetValue(value)
            else
                value

    let getPropertyValues(object: obj) =
        if isNull object then
            Seq.empty
        else
            let namesAndProps = getPropertyNamesAndInfos(object.GetType())

            namesAndProps
            |> Seq.choose(fun (name, prop) ->
                prop.GetValue(object)
                |> unwrapFSharpOption
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

    /// Resolves a public static generic method definition with one type parameter and one
    /// value parameter by name from the given type. Raises a descriptive exception if the
    /// method cannot be uniquely identified, avoiding AmbiguousMatchException from a
    /// name-only GetMethod lookup.
    let private resolveCastMethod(ownerType: Type) =
        ownerType.GetMethods(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Static)
        |> Array.tryFind(fun m ->
            m.Name = "cast"
            && m.IsGenericMethodDefinition
            && m.GetGenericArguments().Length = 1
            && m.GetParameters().Length = 1)
        |> Option.defaultWith(fun () -> failwithf "Could not resolve %s.cast<'t> generic method definition" ownerType.FullName)

    /// Pre-resolved MethodInfo for AsyncExtensions.cast and TaskExtensions.cast.
    /// Both are constant across the lifetime of the process; resolve once at module init.
    let private asyncCastMethod = resolveCastMethod typeof<AsyncExtensions>

    let private taskCastMethod = resolveCastMethod typeof<TaskExtensions>

    /// Per-type cache of the concrete generic MethodInfo produced by MakeGenericMethod.
    /// Avoids repeated generic-method instantiation for the same return type.
    let private asyncCastCache =
        Collections.Concurrent.ConcurrentDictionary<Type, Reflection.MethodInfo>()

    let private taskCastCache =
        Collections.Concurrent.ConcurrentDictionary<Type, Reflection.MethodInfo>()

    let asyncCast runtimeTy (asyncOp: Async<obj>) =
        let m =
            asyncCastCache.GetOrAdd(runtimeTy, fun t -> asyncCastMethod.MakeGenericMethod([| t |]))

        m.Invoke(null, [| asyncOp |])

    let readContentAsString (content: HttpContent) (ct: System.Threading.CancellationToken) : Task<string> =
#if NET5_0_OR_GREATER
        content.ReadAsStringAsync(ct)
#else
        content.ReadAsStringAsync()
#endif

    let readContentAsStream (content: HttpContent) (ct: System.Threading.CancellationToken) : Task<IO.Stream> =
#if NET5_0_OR_GREATER
        content.ReadAsStreamAsync(ct)
#else
        content.ReadAsStreamAsync()
#endif

    let taskCast runtimeTy (task: Task<obj>) =
        let m =
            taskCastCache.GetOrAdd(runtimeTy, fun t -> taskCastMethod.MakeGenericMethod([| t |]))

        m.Invoke(null, [| task |])
