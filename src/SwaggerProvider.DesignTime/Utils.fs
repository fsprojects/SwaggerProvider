namespace SwaggerProvider.Internal

module SchemaReader =
    open System
    open System.IO
    open System.Net
    open System.Net.Http
    open System.Runtime.InteropServices

    /// Determines if a path is truly absolute (not just rooted)
    /// On Windows: C:\path is absolute, \path is rooted (combine with drive), but \..\path is relative
    /// On Unix: /path is absolute, but /../path or /./path are relative
    let private isTrulyAbsolute (path: string) =
        if not (Path.IsPathRooted path) then
            false
        else
            let root = Path.GetPathRoot path
            if String.IsNullOrEmpty root then
                false
            else
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                    // On Windows, a truly absolute path has a volume (C:\, D:\, etc.)
                    // Paths like \path or /path are rooted but may be relative if they start with .. or .
                    if root.Contains(':') then
                        // Has drive letter, truly absolute
                        true
                    else
                        // Rooted but no drive - check if it starts with relative markers
                        // \..\ or /../ or /..\ etc. are relative, not absolute
                        // \.\ or /./ or /\ etc. are also relative
                        let normalized = path.Replace('\\', '/')
                        not (normalized.StartsWith("/../") || normalized.StartsWith("/./") || normalized.StartsWith("/..\\") || normalized.StartsWith("/.\\"))
                else
                    // On Unix, a rooted path is absolute if it starts with /
                    // BUT: if the path starts with /../ or /./, it's relative
                    root = "/" && not (path.StartsWith("/../") || path.StartsWith("/./"))

    let getAbsolutePath (resolutionFolder: string) (schemaPathRaw: string) =
        if String.IsNullOrWhiteSpace(schemaPathRaw) then
            invalidArg "schemaPathRaw" "The schema path cannot be null or empty."

        let uri = Uri(schemaPathRaw, UriKind.RelativeOrAbsolute)

        if uri.IsAbsoluteUri then
            schemaPathRaw
        elif isTrulyAbsolute schemaPathRaw then
            // Truly absolute path (e.g., C:\path on Windows, /path on Unix)
            // On Windows, if path is like \path without drive, combine with drive from resolutionFolder
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && not (Path.GetPathRoot(schemaPathRaw).Contains(':')) then
                Path.Combine(Path.GetPathRoot resolutionFolder, schemaPathRaw.Substring 1)
            else
                schemaPathRaw
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
                // Check address family first to apply family-specific rules
                match ipAddr.AddressFamily with
                | Sockets.AddressFamily.InterNetwork ->
                    // IPv4 validation
                    let bytes = ipAddr.GetAddressBytes()

                    // Check for IPv4 loopback or unspecified address
                    if IPAddress.IsLoopback ipAddr || ipAddr.ToString() = "0.0.0.0" then
                        failwithf "Cannot fetch schemas from localhost/loopback addresses: %s (set SsrfProtection=false for development)" host

                    // Check for IPv4 private ranges
                    let isPrivateIPv4 =
                        match bytes with
                        // 10.0.0.0/8
                        | [| 10uy; _; _; _ |] -> true
                        // 172.16.0.0/12
                        | [| 172uy; secondByte; _; _ |] when secondByte >= 16uy && secondByte <= 31uy -> true
                        // 192.168.0.0/16
                        | [| 192uy; 168uy; _; _ |] -> true
                        // Link-local 169.254.0.0/16
                        | [| 169uy; 254uy; _; _ |] -> true
                        | _ -> false

                    if isPrivateIPv4 then
                        failwithf "Cannot fetch schemas from private or link-local IP addresses: %s (set SsrfProtection=false for development)" host

                | Sockets.AddressFamily.InterNetworkV6 ->
                    // IPv6 validation
                    let bytes = ipAddr.GetAddressBytes()

                    // Check for IPv6 private or reserved ranges
                    let isPrivateIPv6 =
                        match bytes with
                        // Loopback (::1)
                        | [| 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 1uy |] -> true
                        // Unspecified address (::)
                        | [| 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy; 0uy |] -> true
                        // Link-local (fe80::/10) - first byte 0xFE, second byte 0x80-0xBF
                        | [| 0xFEuy; secondByte; _; _; _; _; _; _; _; _; _; _; _; _; _; _ |] when secondByte >= 0x80uy && secondByte <= 0xBFuy -> true
                        // Unique Local Unicast (fc00::/7) - first byte 0xFC or 0xFD
                        | [| 0xFCuy; _; _; _; _; _; _; _; _; _; _; _; _; _; _; _ |] -> true
                        | [| 0xFDuy; _; _; _; _; _; _; _; _; _; _; _; _; _; _; _ |] -> true
                        // Multicast (ff00::/8) - first byte 0xFF
                        | [| 0xFFuy; _; _; _; _; _; _; _; _; _; _; _; _; _; _; _ |] -> true
                        | _ -> false

                    if isPrivateIPv6 then
                        failwithf "Cannot fetch schemas from private or loopback IPv6 addresses: %s (set SsrfProtection=false for development)" host

                | _ ->
                    // Unsupported address family
                    failwithf "Cannot fetch schemas from unsupported IP address type: %s (set SsrfProtection=false for development)" host
            // Block localhost by hostname
            else if host = "localhost" then
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

    let readSchemaPath (ignoreSsrfProtection: bool) (headersStr: string) (resolutionFolder: string) (schemaPathRaw: string) =
        async {
            // Resolve the schema path to absolute path first
            let resolvedPath = getAbsolutePath resolutionFolder schemaPathRaw

            // Check if this is a local file path (not a remote URL)
            // First try to treat it as a local file path (absolute or relative)
            let possibleFilePath =
                try
                    if Path.IsPathRooted resolvedPath then
                        // Already an absolute path
                        if File.Exists resolvedPath then Some resolvedPath else None
                    else
                        // Try to resolve relative paths (e.g., paths with ../ or from __SOURCE_DIRECTORY__)
                        let resolved = Path.GetFullPath resolvedPath
                        if File.Exists resolved then Some resolved else None
                with _ ->
                    None

            match possibleFilePath with
            | Some filePath ->
                // Handle local file - read from disk
                try
                    return File.ReadAllText filePath
                with
                | :? FileNotFoundException -> return failwithf "Schema file not found: %s" filePath
                | ex -> return failwithf "Error reading schema file '%s': %s" filePath ex.Message
            | None ->
                // Handle as remote URL (HTTP/HTTPS)
                let checkUri = Uri(resolvedPath, UriKind.RelativeOrAbsolute)
                // Only treat truly local paths as local files (no scheme or relative paths)
                // Reject file:// scheme as unsupported to prevent SSRF attacks
                let isLocalFile = not checkUri.IsAbsoluteUri

                if isLocalFile then
                    // If we reach here with a local file that wasn't found, report the error
                    return failwithf "Schema file not found: %s" resolvedPath
                else
                    // Handle remote URL (HTTP/HTTPS)
                    let uri = Uri resolvedPath

                    match uri.Scheme with
                    | "https" ->
                        // Validate URL to prevent SSRF (unless explicitly disabled)
                        validateSchemaUrl ignoreSsrfProtection uri

                        let headers =
                            headersStr.Split '|'
                            |> Seq.choose(fun x ->
                                let pair = x.Split '='
                                if (pair.Length = 2) then Some(pair[0], pair[1]) else None)

                        let request = new HttpRequestMessage(HttpMethod.Get, resolvedPath)

                        for name, value in headers do
                            request.Headers.TryAddWithoutValidation(name, value) |> ignore

                        // SECURITY: Remove UseDefaultCredentials to prevent credential leakage (always enforced)
                        use handler = new HttpClientHandler(UseDefaultCredentials = false)
                        use client = new HttpClient(handler, Timeout = TimeSpan.FromSeconds 60.0)

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
                                    resolvedPath
                        else
                            // Development mode: allow HTTP
                            validateSchemaUrl ignoreSsrfProtection uri

                            let headers =
                                headersStr.Split '|'
                                |> Seq.choose(fun x ->
                                    let pair = x.Split '='
                                    if (pair.Length = 2) then Some(pair[0], pair[1]) else None)

                            let request = new HttpRequestMessage(HttpMethod.Get, resolvedPath)

                            for name, value in headers do
                                request.Headers.TryAddWithoutValidation(name, value) |> ignore

                            use handler = new HttpClientHandler(UseDefaultCredentials = false)
                            use client = new HttpClient(handler, Timeout = TimeSpan.FromSeconds 60.0)

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
                        // SECURITY: Reject unknown URL schemes to prevent SSRF attacks via file://, ftp://, etc.
                        return
                            failwithf
                                "Unsupported URL scheme in schema path: '%s'. Only HTTPS is supported for remote schemas (HTTP requires SsrfProtection=false). For local files, ensure the path is absolute or relative to the resolution folder."
                                resolvedPath
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
