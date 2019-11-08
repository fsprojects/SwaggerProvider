namespace Swashbuckle.WebApi.Server.Controllers

module Application =
    [<Literal>]
    let OctetStream = "application/octet-stream"
    [<Literal>]
    let Json = "application/json"


module Types =
    open System.Runtime.Serialization

    [<DataContract>]
    type PointClass(x:int, y:int) =
        new () = PointClass(0,0)
        [<DataMember>]
        member val X = x with get, set
        [<DataMember>]
        member val Y = y with get, set

    [<DataContract>]
    type FileDescription(name:string, bytes:byte[]) =
        new () = FileDescription("",[||])
        [<DataMember>]
        member val Name = name with get, set
        [<DataMember>]
        member val Bytes = bytes with get, set