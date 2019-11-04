# Customizations

## Provided Client Base

TODO:

SwaggerProvider generates `.ctor` that have to be used to create instance of
generated type for communication with a server. The following parameter may be specified

| Parameter | Description |
|-----------|-------------|
| `host` | Server Url, if you want call server that differs from one specified in the schema |
| `CustomizeHttpRequest` | Function that is called for each `System.Net.HttpWebRequest` created by Type Provider. Here you can apply any transformation to the request (add credentials, headers, cookies and etc.) |

[Read more about available configuration options.](http://stackoverflow.com/questions/37566751/what-should-i-do-to-prevent-a-401-unauthorised-when-using-the-swagger-type-provi/37628857#37628857)

## Request interception

TODO:

## Authentication

TODO:

## Serializalization

TODO: