namespace SwaggerProvider.Internal

module SchemaReader =
    open System
    open System.IO
    open System.Net
    open System.Net.Http
    open System.Runtime.InteropServices

    /// Checks if a path starts with relative markers like ../ or ./
    let private startsWithRelativeMarker(path: string) =
        let normalized = path.Replace('\\', '/')
        normalized.StartsWith("/../") || normalized.StartsWith("/./")

    /// Determines if a path is truly absolute (not just rooted)
    /// On Windows: C:\path is absolute, \path is rooted (combine with drive), but \..\path is relative
    /// On Unix: /path is absolute, but /../path or /./path are relative
    let private isTrulyAbsolute(path: string) =
        if not(Path.IsPathRooted path) then
            false
        else
            let root = Path.GetPathRoot path

            if String.IsNullOrEmpty root then
                false
            // On Windows, a truly absolute path has a volume (C:\, D:\, etc.)
            // Paths like \path or /path are rooted but may be relative if they start with .. or .
            else if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                if root.Contains(":") then
                    // Has drive letter, truly absolute
                    true
                else
                    // Rooted but no drive - check if it starts with relative markers
                    // \..\ or /../ are relative, not absolute
                    not(startsWithRelativeMarker path)
            else
                // On Unix, a rooted path is absolute if it starts with /
                // BUT: if the path starts with /../ or /./, it's relative
                root = "/" && not(startsWithRelativeMarker path)

    let getAbsolutePath (resolutionFolder: string) (schemaPathRaw: string) =
        if String.IsNullOrWhiteSpace(schemaPathRaw) then
            invalidArg "schemaPathRaw" "The schema path cannot be null or empty."

        let uri = Uri(schemaPathRaw, UriKind.RelativeOrAbsolute)

        if uri.IsAbsoluteUri then
            schemaPathRaw
        elif isTrulyAbsolute schemaPathRaw then
            // Truly absolute path (e.g., C:\path on Windows, /path on Unix)
            // On Windows, if path is like \path without drive, combine with drive from resolutionFolder
            if
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && not(Path.GetPathRoot(schemaPathRaw).Contains(":"))
            then
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

            // Strip any parameters (e.g. "; charset=utf-8") to get the bare media type for comparison.
            let baseMediaType =
                let idx = mediaType.IndexOf(';')

                if idx >= 0 then
                    mediaType.Substring(0, idx).TrimEnd()
                else
                    mediaType

            // Allow only Content-Types that are valid for OpenAPI/Swagger schema files
            // This prevents SSRF attacks where an attacker tries to make the provider
            // fetch and process non-schema files (HTML, images, binaries, etc.)
            let isValidSchemaContentType =
                baseMediaType = "application/json"
                || baseMediaType = "application/yaml"
                || baseMediaType = "application/x-yaml"
                || baseMediaType = "text/yaml"
                || baseMediaType = "text/x-yaml"
                || baseMediaType = "text/plain"
                || baseMediaType = "application/octet-stream"

            if not isValidSchemaContentType then
                failwithf
                    "Invalid Content-Type for schema: %s. Expected JSON or YAML content types only. This protects against SSRF attacks. Set SsrfProtection=false to disable this validation."
                    mediaType

    /// Sends a GET request to the given URL with optional custom headers and returns the response body.
    /// Validates the Content-Type to prevent processing non-schema responses (unless SSRF protection is off).
    let private fetchUrlContent (ignoreSsrfProtection: bool) (headersStr: string) (resolvedPath: string) =
        async {
            let headers =
                headersStr.Split '|'
                |> Seq.choose(fun x ->
                    let pair = x.Split([| '=' |], 2)

                    if (pair.Length = 2) then
                        Some(pair[0].Trim(), pair[1].Trim())
                    else
                        None)

            use request = new HttpRequestMessage(HttpMethod.Get, resolvedPath)

            for name, value in headers do
                request.Headers.TryAddWithoutValidation(name, value) |> ignore

            // SECURITY: Disable default credentials to prevent credential leakage (always enforced)
            // SECURITY: Prevent redirect-based SSRF bypasses when SSRF protection is enabled.
            use handler =
                new HttpClientHandler(UseDefaultCredentials = false, AllowAutoRedirect = ignoreSsrfProtection)

            use client = new HttpClient(handler, Timeout = TimeSpan.FromSeconds 60.0)

            let! res =
                async {
                    use! response = client.SendAsync request |> Async.AwaitTask

                    // Validate Content-Type to ensure we're parsing the correct format
                    validateContentType ignoreSsrfProtection response.Content.Headers.ContentType

                    return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
                }
                |> Async.Catch

            match res with
            | Choice1Of2 x -> return x
            | Choice2Of2(:? Swagger.OpenApiException as ex) when not <| isNull ex.Content ->
                let! content = ex.Content.ReadAsStringAsync() |> Async.AwaitTask

                return
                    if String.IsNullOrEmpty content then
                        ex.Reraise()
                    else
                        content
            | Choice2Of2(:? WebException as wex) when not <| isNull wex.Response ->
                use stream = wex.Response.GetResponseStream()
                use reader = new StreamReader(stream)
                let err = reader.ReadToEnd()

                return
                    if String.IsNullOrEmpty err then
                        wex.Reraise()
                    else
                        err.ToString()
            | Choice2Of2 e -> return e.Reraise()
        }

    let readSchemaPath (ignoreSsrfProtection: bool) (headersStr: string) (resolutionFolder: string) (schemaPathRaw: string) =
        async {
            // Resolve the schema path to absolute path first
            let resolvedPath = getAbsolutePath resolutionFolder schemaPathRaw

            // Check if this is a local file path (not a remote URL)
            // First try to treat it as a local file path (absolute or relative)
            let possibleFilePath =
                try
                    if Path.IsPathRooted resolvedPath then
                        // Already a rooted path - normalize it to handle .. and . components
                        // This is important on Windows where paths like D:\foo\..\bar need normalization
                        let normalized = Path.GetFullPath resolvedPath
                        if File.Exists normalized then Some normalized else None
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
                // First check if this looks like a local file path (Windows or Unix)
                // On Windows, paths like D:\path are parsed as URIs with scheme "D", so we need special handling
                let looksLikeWindowsPath =
                    resolvedPath.Length >= 2
                    && Char.IsLetter(resolvedPath.[0])
                    && resolvedPath.[1] = ':'

                let looksLikeUnixAbsolutePath = resolvedPath.StartsWith("/")

                // If it looks like a local file path, treat it as such (file not found)
                if
                    looksLikeWindowsPath
                    || looksLikeUnixAbsolutePath
                    || not(resolvedPath.Contains("://"))
                then
                    // If we reach here with a local file that wasn't found, report the error
                    return failwithf "Schema file not found: %s" resolvedPath
                else
                    // Handle remote URL (HTTP/HTTPS)
                    let uri = Uri resolvedPath

                    match uri.Scheme with
                    | "https" ->
                        // Validate URL to prevent SSRF (unless explicitly disabled)
                        validateSchemaUrl ignoreSsrfProtection uri
                        return! fetchUrlContent ignoreSsrfProtection headersStr resolvedPath

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
                            return! fetchUrlContent ignoreSsrfProtection headersStr resolvedPath

                    | _ ->
                        // SECURITY: Reject unknown URL schemes to prevent SSRF attacks via file://, ftp://, etc.
                        return
                            failwithf
                                "Unsupported URL scheme in schema path: '%s'. Only HTTPS is supported for remote schemas (HTTP requires SsrfProtection=false). For local files, ensure the path is absolute or relative to the resolution folder."
                                resolvedPath
        }

module XmlDoc =
    open System
    open System.Collections.Generic
    open System.Text.Json
    open System.Text.Json.Nodes

    // Fast-path: skip all allocations when the string has no XML special characters.
    // In practice the vast majority of OpenAPI descriptions are plain English text,
    // so this check pays for itself immediately.
    let private xmlSpecialChars = [| '&'; '<'; '>' |]

    let private escapeXml(s: string) =
        if s.IndexOfAny(xmlSpecialChars) < 0 then
            s
        else
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

    let private formatEnumValue(v: JsonNode) =
        if isNull v then
            "null"
        else
            match v with
            | :? JsonValue as jv ->
                match jv.GetValueKind() with
                | JsonValueKind.String -> jv.GetValue<string>()
                | JsonValueKind.Null -> "null"
                | _ -> jv.ToString()
            | _ -> v.ToString()

    /// Returns "Allowed values: x, y, z" if the schema has enum values, otherwise None.
    let buildEnumDoc(enumValues: IList<JsonNode>) =
        if isNull enumValues || enumValues.Count = 0 then
            None
        else
            let values = enumValues |> Seq.map formatEnumValue |> String.concat ", "
            Some $"Allowed values: {values}"

    /// Combines a schema description with optional enum-value documentation into a single
    /// XML doc string. Returns null if both inputs are absent.
    /// Callers use this to avoid duplicating the four-case match expression in every property
    /// and parameter doc-building site.
    let combineDescAndEnum (description: string) (enumDoc: string option) =
        match
            description
            |> Option.ofObj
            |> Option.filter(String.IsNullOrWhiteSpace >> not),
            enumDoc
        with
        | None, None -> null
        | Some d, None -> d
        | None, Some ev -> ev
        | Some d, Some ev -> $"{d}\n{ev}"

    /// Builds a structured XML doc string from summary, description, parameter descriptions, and an optional
    /// return description. paramDescriptions is a sequence of (camelCaseName, description) pairs.
    let buildXmlDoc (summary: string) (description: string) (paramDescriptions: (string * string) seq) (returnDoc: string option) =
        let summaryPart =
            if String.IsNullOrEmpty summary then
                ""
            else
                $"<summary>{escapeXml summary}</summary>"

        let remarksPart =
            if String.IsNullOrEmpty description || description = summary then
                ""
            else
                $"<remarks>{escapeXml description}</remarks>"

        let paramParts =
            [ for name, desc in paramDescriptions do
                  if not(String.IsNullOrWhiteSpace desc) then
                      yield $"<param name=\"{name}\">{escapeXml desc}</param>" ]
            |> String.concat ""

        let returnsPart =
            match returnDoc with
            | Some rd when not(String.IsNullOrWhiteSpace rd) -> $"<returns>{escapeXml rd}</returns>"
            | _ -> ""

        String.Concat(summaryPart, remarksPart, paramParts, returnsPart)

type UniqueNameGenerator(?occupiedNames: string seq) =
    let hash = System.Collections.Generic.HashSet<_>()

    do
        for name in (defaultArg occupiedNames Seq.empty) do
            hash.Add(name.ToLowerInvariant()) |> ignore

    // Compute the lowercase prefix once per MakeUnique call; on each collision
    // only the numeric suffix needs to be appended, avoiding repeated ToLowerInvariant
    // calls on the full name string.
    let rec findUniq prefix prefixLower i =
        let newName = if i = 0 then prefix else $"{prefix}{i}"
        let key = if i = 0 then prefixLower else $"{prefixLower}{i}"

        match hash.Contains key with
        | false ->
            hash.Add key |> ignore
            newName
        | true -> findUniq prefix prefixLower (i + 1)

    member _.MakeUnique methodName =
        let methodName = if isNull methodName then "" else methodName
        findUniq methodName (methodName.ToLowerInvariant()) 0
