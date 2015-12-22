module SwaggerProvider.YamlParser

module private ValueParser =
    open System.Globalization
    open System

    /// Converts a function returning bool,value to a function returning value option.
    /// Useful to process TryXX style functions.
    let inline private tryParseWith func = func >> function
        | true, value -> Some value
        | false, _ -> None

    let (|Bool|_|) = tryParseWith Boolean.TryParse
    let (|Int|_|) = tryParseWith Int32.TryParse
    let (|Float|_|) = tryParseWith (fun x -> Double.TryParse(x, NumberStyles.Any, CultureInfo.InvariantCulture))
    let (|TimeSpan|_|) = tryParseWith (fun x -> TimeSpan.TryParse(x, CultureInfo.InvariantCulture))

    let (|DateTime|_|) =
        tryParseWith (fun x -> DateTime.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal))

    let (|Uri|_|) (text: string) =
        ["http"; "https"; "ftp"; "ftps"; "sftp"; "amqp"; "file"; "ssh"; "tcp"]
        |> List.tryPick (fun x ->
            if text.Trim().StartsWith(x + ":", StringComparison.InvariantCultureIgnoreCase) then
                match System.Uri.TryCreate(text, UriKind.Absolute) with
                | true, uri -> Some uri
                | _ -> None
            else None)

open System.IO
open YamlDotNet.Serialization
open System.Collections.Generic

type Node =
    | Scalar of string
    | List of Node list
    | Map of (string * Node) list

let parse : (string -> Node) =
    let rec loop (n: obj) =
        match n with
        | :? List<obj> as l -> Node.List (l |> Seq.map loop |> Seq.toList)
        | :? Dictionary<obj,obj> as m ->
            Map (m |> Seq.choose (fun p ->
                match p.Key with
                | :? string as key -> Some (key, loop p.Value)
                | _ -> None) |> Seq.toList)
        | scalar ->
            let value = if (scalar = null) then "" else scalar.ToString()
            Scalar (value)

    let deserializer = new Deserializer();
    fun (text:string) ->
        try
            use reader = new StringReader(text)
            deserializer.Deserialize(reader) |> loop
        with
        //| :? SharpYaml.YamlException as e when e.InnerException <> null ->
        //    raise e.InnerException // inner exceptions are much more informative
        | _ -> reraise()
