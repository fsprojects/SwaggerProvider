namespace SwaggerProvider.Internal

module SchemaReader =
  open System
  open System.Net.Http

  let readSchemaPath (headersStr:string) (schemaPathRaw:string) =
    async {
        match schemaPathRaw.StartsWith("http", true, null) with
        | true  ->
            let headers =
                headersStr.Split('|')
                |> Seq.choose (fun x ->
                    let pair = x.Split('=')
                    if (pair.Length = 2)
                    then Some (pair.[0],pair.[1])
                    else None
                )
            let request = new HttpRequestMessage(HttpMethod.Get, schemaPathRaw)
            for (name, value) in headers do
                request.Headers.TryAddWithoutValidation(name, value) |> ignore
            // using a custom handler means that we can set the default credentials.
            use handler = new HttpClientHandler(UseDefaultCredentials = true)
            use client = new HttpClient(handler)
            let! response = client.SendAsync(request) |> Async.AwaitTask
            return! response.Content.ReadAsStringAsync() |> Async.AwaitTask
        | false ->
            return schemaPathRaw |> IO.File.ReadAllText
    }

type UniqueNameGenerator() =
    let hash = System.Collections.Generic.HashSet<_>()

    let rec findUniq prefix i =
        let newName = sprintf "%s%s" prefix (if i=0 then "" else i.ToString())
        let key = newName.ToLowerInvariant()
        match hash.Contains key with
        | false ->
            hash.Add key |> ignore
            newName
        | true ->
            findUniq prefix (i+1)

    member __.MakeUnique methodName =
        findUniq methodName 0
