namespace SwaggerProvider.Internal

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
