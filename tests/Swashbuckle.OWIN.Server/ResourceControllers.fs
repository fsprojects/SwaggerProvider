module ResourceControllers

open System
open System.Web.Http

type ResourceController<'a,'b when 'a: equality> (dict:System.Collections.Generic.Dictionary<'a,'b>)=
    inherit ApiController()
    member __.Get    (key)                     = dict.[key]
    member __.Delete (key)                     = dict.Remove(key) |> ignore
    member __.Put    (key) ([<FromBody>]value) = dict.Add(key, value)
    member __.Post   (key) ([<FromBody>]value) = dict.[key] <- value


let dictStringString = System.Collections.Generic.Dictionary<string, string> ()
[<Route("api/ResourceStringString/{key}")>]
type ResourceStringStringController () =
    inherit ResourceController<string,string>(dictStringString)