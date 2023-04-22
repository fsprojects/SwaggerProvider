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

    let readSchemaPath (headersStr: string) (schemaPathRaw: string) =
        async {
            match Uri(schemaPathRaw).Scheme with
            | "https"
            | "http" ->
                let headers =
                    headersStr.Split('|')
                    |> Seq.choose(fun x ->
                        let pair = x.Split('=')

                        if (pair.Length = 2) then Some(pair[0], pair[1]) else None)

                let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)

                for name, value in headers do
                    request.Headers.TryAddWithoutValidation(name, value) |> ignore
                // using a custom handler means that we can set the default credentials.
                use handler = new HttpClientHandler(UseDefaultCredentials = true)
                use client = new HttpClient(handler)

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
                            raise wex
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
