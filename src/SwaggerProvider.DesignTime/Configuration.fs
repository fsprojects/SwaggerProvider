/// [omit]
module SwaggerProvider.Internal.Configuration

open System.IO

type Logging() =
  static member logf fmt =
    Printf.kprintf (fun s ->  File.AppendAllLines("/Users/chethusk/oss/swaggerprovider/swaggerlog", [|s|])) fmt