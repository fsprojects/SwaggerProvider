module SwaggerProvider.YamlParser

open System.IO
open YamlDotNet.Serialization
open System.Collections.Generic

type Node =
    | Scalar of string
    | List of Node list
    | Map of (string * Node) list

let Parse : (string -> Node) =
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
        use reader = new StringReader(text)
        deserializer.Deserialize(reader) |> loop
