namespace SwaggerProvider.Internal

module SchemaReader =
    open System
    open System.IO
    open System.Net
    open System.Net.Http

    let getAbsolutePath (resolutionFolder: string) (schemaPathRaw: string) =
        let uri = Uri(schemaPathRaw, UriKind.RelativeOrAbsolute)

        if uri.IsAbsoluteUri then
            schemaPathRaw
        elif Path.IsPathRooted schemaPathRaw then
            Path.Combine(Path.GetPathRoot(resolutionFolder), schemaPathRaw.Substring(1))
        else
            Path.Combine(resolutionFolder, schemaPathRaw)

    /// Validates URL to prevent SSRF attacks
    /// Pass ignoreSsrfProtection=true to disable validation (for development/testing only)
    let validateSchemaUrl (ignoreSsrfProtection: bool) (url: Uri) =
        if ignoreSsrfProtection then
            () // Skip validation when explicitly disabled
        else
            // Only allow HTTPS for security (prevent MITM)
            if url.Scheme <> "https" then
                failwithf "Only HTTPS URLs are allowed for remote schemas. Got: %s (set SsrfProtection=false for development)" url.Scheme

            // Prevent access to private IP ranges (SSRF protection)
            let host = url.Host.ToLowerInvariant()

            // Block localhost and loopback
            if
                host = "localhost"
                || host.StartsWith "127."
                || host = "::1"
                || host = "0.0.0.0"
            then
                failwithf "Cannot fetch schemas from localhost/loopback addresses: %s (set SsrfProtection=false for development)" host

            // Block private IP ranges (RFC 1918)
            if
                host.StartsWith "10."
                || host.StartsWith "192.168."
                || host.StartsWith "172.16."
                || host.StartsWith "172.17."
                || host.StartsWith "172.18."
                || host.StartsWith "172.19."
                || host.StartsWith "172.20."
                || host.StartsWith "172.21."
                || host.StartsWith "172.22."
                || host.StartsWith "172.23."
                || host.StartsWith "172.24."
                || host.StartsWith "172.25."
                || host.StartsWith "172.26."
                || host.StartsWith "172.27."
                || host.StartsWith "172.28."
                || host.StartsWith "172.29."
                || host.StartsWith "172.30."
                || host.StartsWith "172.31."
            then
                failwithf "Cannot fetch schemas from private IP addresses: %s (set SsrfProtection=false for development)" host

            // Block link-local addresses
            if host.StartsWith "169.254." then
                failwithf "Cannot fetch schemas from link-local addresses: %s (set SsrfProtection=false for development)" host

    let readSchemaPath (ignoreSsrfProtection: bool) (headersStr: string) (schemaPathRaw: string) =
        async {
            let uri = Uri schemaPathRaw

            match uri.Scheme with
            | "https" ->
                // Validate URL to prevent SSRF (unless explicitly disabled)
                validateSchemaUrl ignoreSsrfProtection uri

                let headers =
                    headersStr.Split '|'
                    |> Seq.choose(fun x ->
                        let pair = x.Split '='

                        if (pair.Length = 2) then Some(pair[0], pair[1]) else None)

                let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)

                for name, value in headers do
                    request.Headers.TryAddWithoutValidation(name, value) |> ignore

                // SECURITY: Remove UseDefaultCredentials to prevent credential leakage (always enforced)
                use handler = new HttpClientHandler(UseDefaultCredentials = false)
                use client = new HttpClient(handler, Timeout = System.TimeSpan.FromSeconds 60.0)

                let! res =
                    async {
                        let! response = client.SendAsync request |> Async.AwaitTask

                        // Validate Content-Type to ensure we're parsing the correct format
                        let contentType = response.Content.Headers.ContentType

                        if not(isNull contentType) then
                            let mediaType = contentType.MediaType.ToLowerInvariant()

                            if
                                not(
                                    mediaType.Contains "json"
                                    || mediaType.Contains "yaml"
                                    || mediaType.Contains "text"
                                    || mediaType.Contains "application/octet-stream"
                                )
                            then
                                failwithf "Invalid Content-Type for schema: %s. Expected JSON or YAML." mediaType

                        return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
                    }
                    |> Async.Catch

                match res with
                | Choice1Of2 x -> return x
                | Choice2Of2(:? Swagger.OpenApiException as ex) when not <| isNull ex.Content ->
                    let content =
                        ex.Content.ReadAsStringAsync()
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    if String.IsNullOrEmpty content then
                        return ex.Reraise()
                    else
                        return content
                | Choice2Of2(:? WebException as wex) when not <| isNull wex.Response ->
                    use stream = wex.Response.GetResponseStream()
                    use reader = new StreamReader(stream)
                    let err = reader.ReadToEnd()

                    return
                        if String.IsNullOrEmpty err then
                            wex.Reraise()
                        else
                            err.ToString()
                | Choice2Of2 e -> return failwith(e.ToString())
            | "http" ->
                // HTTP is allowed only when SSRF protection is explicitly disabled (development/testing mode)
                if not ignoreSsrfProtection then
                    return
                        failwithf
                            "HTTP URLs are not supported for security reasons. Use HTTPS or set SsrfProtection=false for development: %s"
                            schemaPathRaw
                else
                    // Development mode: allow HTTP
                    validateSchemaUrl ignoreSsrfProtection uri // Still validate private IPs even in dev mode

                    let headers =
                        headersStr.Split '|'
                        |> Seq.choose(fun x ->
                            let pair = x.Split '='
                            if (pair.Length = 2) then Some(pair[0], pair[1]) else None)

                    let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)

                    for name, value in headers do
                        request.Headers.TryAddWithoutValidation(name, value) |> ignore

                    use handler = new HttpClientHandler(UseDefaultCredentials = false)
                    use client = new HttpClient(handler, Timeout = System.TimeSpan.FromSeconds 60.0)

                    let! res =
                        async {
                            let! response = client.SendAsync(request) |> Async.AwaitTask
                            return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
                        }
                        |> Async.Catch

                    match res with
                    | Choice1Of2 x -> return x
                    | Choice2Of2(:? WebException as wex) when not <| isNull wex.Response ->
                        use stream = wex.Response.GetResponseStream()
                        use reader = new StreamReader(stream)
                        let err = reader.ReadToEnd()

                        return
                            if String.IsNullOrEmpty err then
                                wex.Reraise()
                            else
                                err.ToString()
                    | Choice2Of2 e -> return failwith(e.ToString())
            | _ ->
                let request = WebRequest.Create(schemaPathRaw)
                use! response = request.GetResponseAsync() |> Async.AwaitTask
                use sr = new StreamReader(response.GetResponseStream())
                return! sr.ReadToEndAsync() |> Async.AwaitTask
        }

type UniqueNameGenerator() =
    let hash = System.Collections.Generic.HashSet<_>()

    let rec findUniq prefix i =
        let newName = sprintf "%s%s" prefix (if i = 0 then "" else i.ToString())
        let key = newName.ToLowerInvariant()

        match hash.Contains key with
        | false ->
            hash.Add key |> ignore
            newName
        | true -> findUniq prefix (i + 1)

    member _.MakeUnique methodName =
        findUniq methodName 0
