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
            Path.Combine(Path.GetPathRoot resolutionFolder, schemaPathRaw.Substring 1)
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

            // Block localhost and loopback, and private IP ranges using proper IP address parsing
            let isIp, ipAddr = IPAddress.TryParse host

            if isIp then
                // Loopback
                if IPAddress.IsLoopback ipAddr || ipAddr.ToString() = "0.0.0.0" then
                    failwithf "Cannot fetch schemas from localhost/loopback addresses: %s (set SsrfProtection=false for development)" host
                // Private IPv4 ranges
                let bytes = ipAddr.GetAddressBytes()

                let isPrivate =
                    ipAddr.AddressFamily = Sockets.AddressFamily.InterNetwork
                    && match bytes with
                       | [| 10uy; _; _; _ |] -> true // 10.0.0.0/8
                       | [| 172uy; b1; _; _ |] when b1 >= 16uy && b1 <= 31uy -> true // 172.16.0.0/12
                       | [| 192uy; 168uy; _; _ |] -> true // 192.168.0.0/16
                       | [| 169uy; 254uy; _; _ |] -> true // Link-local 169.254.0.0/16
                       | _ -> false

                if isPrivate then
                    failwithf "Cannot fetch schemas from private or link-local IP addresses: %s (set SsrfProtection=false for development)" host
            else if
                // Block localhost by name
                host = "localhost"
            then
                failwithf "Cannot fetch schemas from localhost/loopback addresses: %s (set SsrfProtection=false for development)" host

    let validateContentType (ignoreSsrfProtection: bool) (contentType: Headers.MediaTypeHeaderValue) =
        // Skip validation if SSRF protection is disabled
        if ignoreSsrfProtection || isNull contentType then
            ()
        else
            let mediaType = contentType.MediaType.ToLowerInvariant()

            // Allow only Content-Types that are valid for OpenAPI/Swagger schema files
            // This prevents SSRF attacks where an attacker tries to make the provider
            // fetch and process non-schema files (HTML, images, binaries, etc.)
            let isValidSchemaContentType =
                // JSON formats
                mediaType = "application/json"
                || mediaType.StartsWith "application/json;"
                // YAML formats
                || mediaType = "application/yaml"
                || mediaType = "application/x-yaml"
                || mediaType = "text/yaml"
                || mediaType = "text/x-yaml"
                || mediaType.StartsWith "application/yaml;"
                || mediaType.StartsWith "application/x-yaml;"
                || mediaType.StartsWith "text/yaml;"
                || mediaType.StartsWith "text/x-yaml;"
                // Plain text (sometimes used for YAML)
                || mediaType = "text/plain"
                || mediaType.StartsWith "text/plain;"
                // Generic binary (fallback for misconfigured servers)
                || mediaType = "application/octet-stream"
                || mediaType.StartsWith "application/octet-stream;"

            if not isValidSchemaContentType then
                failwithf
                    "Invalid Content-Type for schema: %s. Expected JSON or YAML content types only. This protects against SSRF attacks. Set SsrfProtection=false to disable this validation."
                    mediaType

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
                        validateContentType ignoreSsrfProtection response.Content.Headers.ContentType

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
                    validateSchemaUrl ignoreSsrfProtection uri

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

                            // Validate Content-Type to ensure we're parsing the correct format
                            validateContentType ignoreSsrfProtection response.Content.Headers.ContentType

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
