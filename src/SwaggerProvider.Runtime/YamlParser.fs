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

open System
open SharpYaml.Serialization
open System.Collections.Generic

type Scalar =
    | Int of int
    | String of string
    | TimeSpan of TimeSpan
    | Bool of bool
    | Uri of Uri
    | Float of double
    static member ParseStr = function
        | ValueParser.TimeSpan x -> TimeSpan x
        | ValueParser.Uri x -> Uri x
        | x -> String x
    static member FromObj : obj -> Scalar = function
        | null -> String ""
        | :? System.Boolean as b -> Bool b
        | :? System.Int32 as i -> Int i
        | :? System.Double as d -> Float d
        | :? System.String as s ->
            Scalar.ParseStr s
        |t -> failwithf "Unknown type %s" (string (t.GetType()))
    member x.UnderlyingType =
        match x with
        | Int x -> x.GetType()
        | String x -> x.GetType()
        | Bool x -> x.GetType()
        | TimeSpan x -> x.GetType()
        | Uri x -> x.GetType()
        | Float x -> x.GetType()
    member x.BoxedValue =
        match x with
        | Int x -> box x
        | String x -> box x
        | TimeSpan x -> box x
        | Bool x -> box x
        | Uri x -> box x
        | Float x -> box x

type Node =
    | Scalar of Scalar
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
            Scalar (Scalar.FromObj scalar)

    let settings = SerializerSettings(EmitDefaultValues=true, EmitTags=false, SortKeyForMapping=false)
    let serializer = Serializer(settings)
    fun text ->
        try serializer.Deserialize(fromText=text) |> loop
        with
        | :? SharpYaml.YamlException as e when e.InnerException <> null ->
            raise e.InnerException // inner exceptions are much more informative
        | _ -> reraise()
