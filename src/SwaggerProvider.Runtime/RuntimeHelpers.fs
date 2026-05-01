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
    let private timeOnlyTypeName = "System.TimeOnly"

    let private isDateOnlyType(t: Type) =
        not(isNull t) && t.FullName = dateOnlyTypeName

    let private isTimeOnlyType(t: Type) =
        not(isNull t) && t.FullName = timeOnlyTypeName

    let private isOptionOfDateOnlyType(t: Type) =
        t.IsGenericType
        && t.GetGenericTypeDefinition() = typedefof<option<_>>
        && isDateOnlyType(t.GetGenericArguments().[0])

    let private isOptionOfTimeOnlyType(t: Type) =
        t.IsGenericType
        && t.GetGenericTypeDefinition() = typedefof<option<_>>
        && isTimeOnlyType(t.GetGenericArguments().[0])

    let private isDateOnlyLikeType(t: Type) =
        isDateOnlyType t || isOptionOfDateOnlyType t

    let private isTimeOnlyLikeType(t: Type) =
        isTimeOnlyType t || isOptionOfTimeOnlyType t

    // Formats a DateOnly or TimeOnly value using the given format string.
    // The caller has already verified ty.FullName matches the expected type name.
    // DateOnly and TimeOnly implement IFormattable on .NET 6+; the GetMethod
    // fallback is a defensive path for forward-compatibility only.
    let private formatDateOrTimeValue (format: string) (ty: Type) (value: obj) : string =
        match value with
        | :? IFormattable as f -> f.ToString(format, Globalization.CultureInfo.InvariantCulture)
        | _ ->
            ty.GetMethod("ToString", [| typeof<string>; typeof<IFormatProvider> |])
            |> Option.ofObj
            |> Option.map(fun mi -> mi.Invoke(value, [| box format; box Globalization.CultureInfo.InvariantCulture |]) :?> string)
            |> Option.defaultWith(fun () -> value.ToString())

    // Cache of precomputed union tag readers for F# option types. Avoids the overhead of
    // FSharpValue.GetUnionFields (which allocates UnionCaseInfo + obj[]) on each call.
    // Stores as (obj -> int) with an explicit wrapper to satisfy nullable annotations.
    let private optionTagReaderCache =
        Collections.Concurrent.ConcurrentDictionary<Type, obj -> int>()

    let private makeOptionTagReader(t: Type) : obj -> int =
        let reader = Microsoft.FSharp.Reflection.FSharpValue.PreComputeUnionTagReader t
        fun (o: obj) -> reader o

    // Hoisted factory delegate to avoid allocating a new Func on every GetOrAdd call.
    let private optionTagReaderFactory =
        System.Func<Type, obj -> int>(makeOptionTagReader)

    // Cache of the 'Value' PropertyInfo per F# option type, shared with unwrapFSharpOption below.
    let private optionValueCache =
        Collections.Concurrent.ConcurrentDictionary<Type, Reflection.PropertyInfo>()

    // Hoisted factory delegate to avoid allocating a new lambda on every GetOrAdd call.
    let private optionValueFactory =
        System.Func<Type, Reflection.PropertyInfo>(fun t -> t.GetProperty("Value"))

    // Reflective lookup for JsonStringEnumMemberNameAttribute (available in System.Text.Json 9.0+).
    // On older runtimes (e.g. netstandard2.0 with STJ 8.x) this will be null and
    // enum members are serialised using their .NET identifier name by default.
    let private jsonStringEnumMemberNameType =
        Type.GetType("System.Text.Json.Serialization.JsonStringEnumMemberNameAttribute, System.Text.Json")

    let private jsonStringEnumMemberNameCtor =
        if isNull jsonStringEnumMemberNameType then
            null
        else
            jsonStringEnumMemberNameType.GetConstructor([| typeof<string> |])

    let private jsonStringEnumMemberNameProp =
        if isNull jsonStringEnumMemberNameType then
            null
        else
            jsonStringEnumMemberNameType.GetProperty("Name")

    /// Returns the OpenAPI wire value for a string enum field.
    /// Prefers [JsonStringEnumMemberName] (STJ 9+), then [JsonPropertyName] (legacy fallback),
    /// and finally the .NET field name when neither attribute is present.
    let private getStringEnumMemberWireName(f: Reflection.FieldInfo) : string =
        // Prefer JsonStringEnumMemberNameAttribute (the correct STJ mechanism for enum member naming).
        if not(isNull jsonStringEnumMemberNameType) then
            let attr = Attribute.GetCustomAttribute(f, jsonStringEnumMemberNameType)

            if not(isNull attr) then
                jsonStringEnumMemberNameProp.GetValue(attr) :?> string
            else
                // Legacy fallback: attributes from type providers built before the switch to
                // JsonStringEnumMemberName still carry [JsonPropertyName].
                let propAttr =
                    Attribute.GetCustomAttribute(f, typeof<JsonPropertyNameAttribute>) :?> JsonPropertyNameAttribute

                if isNull propAttr then f.Name else propAttr.Name
        else
            let propAttr =
                Attribute.GetCustomAttribute(f, typeof<JsonPropertyNameAttribute>) :?> JsonPropertyNameAttribute

            if isNull propAttr then f.Name else propAttr.Name

    /// Builds an (obj -> string) serializer for CLI enum types.
    /// For string enums (annotated with JsonStringEnumConverter): returns the
    /// JsonStringEnumMemberName / JsonPropertyName wire value for each member,
    /// falling back to the field name.
    /// For integer enums: returns the underlying integer value as a string.
    let private buildEnumSerializer(ty: Type) : obj -> string =
        let jsonConverterAttr =
            Attribute.GetCustomAttribute(ty, typeof<JsonConverterAttribute>) :?> JsonConverterAttribute

        let isStringEnum =
            not(isNull jsonConverterAttr)
            && typeof<JsonStringEnumConverter>.IsAssignableFrom(jsonConverterAttr.ConverterType)

        if isStringEnum then
            // Use Dictionary with ContainsKey + Add instead of |> dict to safely handle alias values
            // (two enum members with the same underlying integer), which would throw in dict.
            let lookup = Collections.Generic.Dictionary<int, string>()

            ty.GetFields(Reflection.BindingFlags.Public ||| Reflection.BindingFlags.Static)
            |> Array.iter(fun f ->
                if f.IsLiteral then
                    let v = Convert.ToInt32(f.GetRawConstantValue())
                    let name = getStringEnumMemberWireName f
                    // Skip alias values (same integer, different name) to prevent key collisions.
                    if not(lookup.ContainsKey v) then
                        lookup.Add(v, name))

            fun (o: obj) ->
                let intValue = Convert.ToInt32 o

                match lookup.TryGetValue intValue with
                | true, s -> s
                | false, _ -> o.ToString()
        else
            let underlyingType = Enum.GetUnderlyingType ty
            fun (o: obj) -> Convert.ChangeType(o, underlyingType).ToString()

    // Cache of enum type -> (obj -> string) serializer, built lazily per type.
    let private enumSerializerCache =
        Collections.Concurrent.ConcurrentDictionary<Type, obj -> string>()

    let private enumSerializerFactory =
        System.Func<Type, obj -> string>(buildEnumSerializer)

    let rec toParam(obj: obj) =
        match obj with
        | :? DateTime as dt -> dt.ToString("O")
        | :? DateTimeOffset as dto -> dto.ToString("O")
        | null -> null
        | _ ->
            // Hoist GetType() once; previously tryFormatDateOnly and tryFormatTimeOnly
            // each called GetType() internally, resulting in up to 3 GetType() calls for
            // common scalar types such as string, int, Guid, or bool.
            let ty = obj.GetType()

            if ty.FullName = dateOnlyTypeName then
                formatDateOrTimeValue "yyyy-MM-dd" ty obj
            elif ty.FullName = timeOnlyTypeName then
                formatDateOrTimeValue "HH:mm:ss.FFFFFFF" ty obj
            // Unwrap F# Option<T>: Some(x) -> toParam(x), None -> null.
            // Uses a precomputed tag reader (cached) to check Some/None without
            // allocating a UnionCaseInfo or obj[] on every call.
            elif
                ty.IsGenericType
                && ty.GetGenericTypeDefinition() = typedefof<option<_>>
            then
                let tagReader = optionTagReaderCache.GetOrAdd(ty, optionTagReaderFactory)

                if tagReader obj = 1 then // 1 = Some
                    let valueProp = optionValueCache.GetOrAdd(ty, optionValueFactory)

                    toParam(valueProp.GetValue(obj))
                else
                    null
            elif ty.IsEnum then
                // CLI enum type: use the cached serializer so string enums produce their
                // original OpenAPI string value and integer enums produce the integer.
                let serializer = enumSerializerCache.GetOrAdd(ty, enumSerializerFactory)
                serializer obj
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
                |> Option.exists(fun t -> isDateOnlyLikeType t || isTimeOnlyLikeType t || t.IsEnum)
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

    // Cached constructor for JsonConverterAttribute (used to apply JsonStringEnumConverter to generated enum types).
    let private jsonConverterCtor =
        typeof<JsonConverterAttribute>.GetConstructor [| typeof<Type> |]

    /// Builds a CustomAttributeData representing [JsonConverter(typeof<JsonStringEnumConverter>)].
    /// Apply this to generated CLI enum types so System.Text.Json serialises them as strings.
    let getJsonStringEnumConverterAttribute() =
        { new Reflection.CustomAttributeData() with
            member _.Constructor = jsonConverterCtor

            member _.ConstructorArguments =
                [| Reflection.CustomAttributeTypedArgument(typeof<Type>, typeof<JsonStringEnumConverter>) |] :> Collections.Generic.IList<_>

            member _.NamedArguments = [||] :> Collections.Generic.IList<_> }

    /// Builds a CustomAttributeData representing [JsonStringEnumMemberName(name)].
    /// Apply this to individual string-enum members so System.Text.Json (9.0+) honours
    /// the exact OpenAPI wire value regardless of the sanitised .NET member name.
    /// Returns None on runtimes where JsonStringEnumMemberNameAttribute is not available
    /// (System.Text.Json < 9.0 / netstandard2.0 with STJ 8.x).
    let getEnumMemberNameAttribute(name: string) =
        if isNull jsonStringEnumMemberNameCtor then
            None
        else
            Some(
                { new Reflection.CustomAttributeData() with
                    member _.Constructor = jsonStringEnumMemberNameCtor

                    member _.ConstructorArguments =
                        [| Reflection.CustomAttributeTypedArgument(typeof<string>, name) |] :> Collections.Generic.IList<_>

                    member _.NamedArguments = [||] :> Collections.Generic.IList<_> }
            )

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
    // Reuses optionValueCache defined alongside toParam above.
    let private unwrapFSharpOption(value: obj) : obj =
        if isNull value then
            null
        else
            let ty = value.GetType()

            if
                ty.IsGenericType
                && ty.GetGenericTypeDefinition() = typedefof<option<_>>
            then
                let prop = optionValueCache.GetOrAdd(ty, optionValueFactory)
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
                let strValue = toParam x

                if not(isNull strValue) then
                    cnt.Add(toStringContent strValue, name)

        cnt

    let toFormUrlEncodedContent(keyValues: seq<string * obj>) =
        let keyValues =
            keyValues
            |> Seq.filter(snd >> isNull >> not)
            |> Seq.choose(fun (k, v) ->
                let param = toParam v

                if isNull param then
                    None
                else
                    Some(Collections.Generic.KeyValuePair(k, param)))

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

    // Pre-built map of standard HTTP method names to their corresponding static HttpMethod
    // instances. Uses an ordinal case-insensitive comparer so callers passing different
    // casing (for example, "get") still resolve to the cached standard HttpMethod without
    // allocating a normalized string for lookup.
    let private standardHttpMethods =
        let methods =
            [| HttpMethod.Get
               HttpMethod.Post
               HttpMethod.Put
               HttpMethod.Delete
               HttpMethod("PATCH")
               HttpMethod.Head
               HttpMethod.Options
               HttpMethod.Trace |]

        let dictionary =
            System.Collections.Generic.Dictionary<string, HttpMethod>(StringComparer.OrdinalIgnoreCase)

        methods |> Array.iter(fun m -> dictionary.Add(m.Method, m))
        System.Collections.ObjectModel.ReadOnlyDictionary<string, HttpMethod>(dictionary)

    let private resolveHttpMethod(method: string) : HttpMethod =
        match standardHttpMethods.TryGetValue method with
        | true, m -> m
        | false, _ -> HttpMethod(method.ToUpperInvariant())

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

        let method = resolveHttpMethod httpMethod
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
