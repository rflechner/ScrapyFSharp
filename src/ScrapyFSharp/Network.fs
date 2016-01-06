namespace ScrapyFSharp.Network

open System
open System.IO
open System.Net
open System.Text
open System.Text.RegularExpressions
open System.Globalization
open System.Collections.Generic
open System.Collections.Specialized
open System.Net.Cache

type WebResource =
    { Content:MemoryStream
      LastModified:string
      AbsoluteUrl:Uri
      ForceDownload:bool
      ContentType:string }
    
type FakeUserAgent =
    { Name:string
      UserAgent:string}
    static member Chrome = 
        {Name="Chrome"; UserAgent="Mozilla/5.0 (Windows; U; Windows NT 6.1; en-US) AppleWebKit/534.13 (KHTML, like Gecko) Chrome/9.0.597.98 Safari/534.13"}
    static member Chrome24 = 
        {Name="Chrome"; UserAgent="Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/537.17 (KHTML, like Gecko) Chrome/24.0.1312.57 Safari/537.17"}
    static member InternetExplorer8 =
        {Name="Internet Explorer 8"; UserAgent="Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 6.1; WOW64; Trident/4.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; Media Center PC 6.0; CMDTDF; .NET4.0C; .NET4.0E)"}

type HttpVerb =
    | Get
    | Post
    | Put
    | Head
    | Delete
    | Trace
    override x.ToString() = 
        match x with
        | Get -> "GET"
        | Post -> "POST"
        | Put -> "PUT"
        | Head -> "HEAD"
        | Delete -> "DELETE"
        | Trace -> "TRACE"
    static member Parse (text:string) =
        if text |> String.IsNullOrWhiteSpace
        then None
        else
            match text.ToUpperInvariant().Trim() with
            | "GET" -> Some Get
            | "POST" -> Some Post
            | "PUT" -> Some Put
            | "HEAD" -> Some Head
            | "DELETE" -> Some Delete
            | "TRACE" -> Some Trace
            | _ -> None

type Helpers () = 
    static let toHex (c:char) = Convert.ToInt32(c) |> fun i -> i.ToString("X")
    static member UrlEncode s = 
        let parts = 
            s 
            |> Seq.map (
                fun c -> 
                    if c |> Char.IsLetterOrDigit |> not
                    then "%" + (c |> toHex)
                    else c.ToString()
                )
        String.Join("", parts)
    static member UrlDecode (text:string) =
        let (|HexaChar|_|) (s:char list) =
            if s.Length > 0 && s.Head = '%' then
                let chars = s |> Seq.skip 1 |> Seq.take 2 |> Array.ofSeq
                let h = new String(chars)
                let num = Convert.ToInt32(h, 16)
                let tail = s |> Seq.skip (chars.Length+1) |> List.ofSeq
                Some ((Convert.ToChar num), tail)
            else
                None
        let rec decode s acc = 
            match s with
            | HexaChar (c, t) -> decode t (c :: acc)
            | c :: t          -> decode t (c :: acc)
            | []              -> new string(acc |> List.rev |> Array.ofList)
        decode (text |> Seq.toList) []

type HttpRequestData =
    | Text of string
    | Buffer of byte array
    | ReadableData of Stream
    | FormData of (string * string) list
    static member FromString s = Text s
    static member FromBytes b = Buffer b
    static member FromStream s = ReadableData s
    static member FromFormData f = FormData f
    member x.ToRawParams() =
        match x with
        | Text s -> sprintf "%s" s
        | Buffer b -> sprintf "%s" (Encoding.ASCII.GetString b)
        | ReadableData s -> 
            use reader = new StreamReader(s)
            sprintf "%s" (reader.ReadToEnd())
        | FormData forms ->
            let b = StringBuilder()
            for i in 0..forms.Length-1 do
                let (k,v) = forms.Item i
                k |> Helpers.UrlEncode |> b.Append |> ignore
                b.Append "=" |> ignore
                v |> Helpers.UrlEncode |> b.Append |> ignore
                if i < forms.Length-1
                then b.Append "&" |> ignore
            b.ToString()

type RawRequest =
    { Verb:string
      Url:Uri
      HttpVersion:Version
      Headers:(string*string) list
      Body:(byte array) option
      Encoding:Encoding }
    override x.ToString() = 
        let builder = StringBuilder()
        builder.AppendFormat("{0} {1} HTTP/{2}.{3}\r\n", x.Verb, x.Url, x.HttpVersion.Major, x.HttpVersion.Minor) |> ignore
        for (k,v) in x.Headers do
            builder.AppendFormat("{0}: {1}\r\n", k, v) |> ignore
        builder.Append("\r\n") |> ignore
        match x.Body with
        | None
        | Some [||] -> ()
        | Some body ->
            builder.AppendFormat("{0}\r\n", Encoding.ASCII.GetString(body)) |> ignore
        builder.ToString()

type RawResponse =
    { HttpVersion:Version
      StatusCode:int
      StatusDescription:string
      Headers:(string*string) list
      Body:(byte array) option
      Encoding:Encoding }
    override x.ToString() = 
        let builder = StringBuilder()
        builder.AppendFormat("HTTP/{0}.{1} {2} {3}\r\n", x.HttpVersion.Major, x.HttpVersion.Minor, x.StatusCode, x.StatusDescription)
        |> ignore
        for (k,v) in x.Headers do
            builder.AppendFormat("{0}: {1}\r\n", k, v) |> ignore
        builder.Append("\r\n") |> ignore
        match x.Body with
        | None
        | Some [||] -> ()
        | Some body ->
            builder.AppendFormat("{0}\r\n", Encoding.ASCII.GetString(body)) |> ignore
        builder.ToString()

type CookiesParser (defaultDomain:string) =
    let splitCookiesRegex = new Regex(@"\s*(?<name>[^=]+)=(?<val>[^;]+)?[,;]+", RegexOptions.Compiled)

    member x.ParseValuePairs exp =
        let rec iterate (m:Match) (acc:(string*string) list) =
            let n = m.Groups.["name"]
            let v = m.Groups.["val"]
            if m.Success && n.Success && v.Success then
                iterate (m.NextMatch()) ((n.Value,v.Value) :: acc)
            else acc
        let m = splitCookiesRegex.Match exp
        iterate m []

    member x.ParseCookies exp =
        let cmp (s1:string) (s2:string) = s1.Equals(s2, StringComparison.InvariantCultureIgnoreCase)
        let (|Cmp|_|) s1 s2 = if cmp s1 s2 then Some () else None
        let isKeywordCookie s = 
            match s with
            | Cmp "path"
            | Cmp "domain"
            | Cmp "expires" -> true
            | _ -> false
        let valuePairs = x.ParseValuePairs exp
        let decK (k,_) = k
        let decV (_,v) = v
        let cleanName (k:string) =
            if k.Contains "HttpOnly"
            then
                let p = k.Split ([|','|], StringSplitOptions.RemoveEmptyEntries) 
                        |> Seq.map (fun i -> i.Trim())
                        |> Seq.filter (fun i -> i <> "HttpOnly")
                        |> Seq.toArray
                String.Join(",", p)
            else k
        let tryParse n o s =
            if n+o < valuePairs.Length && cmp (decK valuePairs.[n+o]) s
            then Some valuePairs.[n+o]
            else None
        let tryFind n s =
            match tryParse n 2 s with
            | None -> tryParse n 1 s
            | p -> p
        let tryParsePath n = tryFind n "path"
        let tryParseDomain n = tryFind n "domain"
        seq { 
            for i in 0..valuePairs.Length-1 do
                let (k1,v) = valuePairs.[i]
                let k = cleanName k1
                if isKeywordCookie k |> not then
                    let path = tryParsePath i
                    let domain = tryParseDomain i
                    match (path, domain) with
                    | Some p, None -> yield Cookie(k, v, decV p, defaultDomain)
                    | Some p, Some d -> 
                        let p2 = decV p
                        let d2 = decV d
                        yield Cookie(k, v, p2, d2)
                    | _ -> yield Cookie(k, v, "/", defaultDomain)
        } |> Seq.toList

type WebPage = 
    { AbsoluteUrl:Uri
      //Browser: ScrapingBrowser
      Request:RawRequest
      Response:RawResponse
      AutoDetectCharsetEncoding:bool
      Resources:WebResource list
      BaseUrl:string }
    member x.Html() =
        match x.Response.Body with
        | Some body -> 
            let text = body |> x.Response.Encoding.GetString 
            text
            |> FSharp.Data.HtmlDocument.Parse 
            |> Some
        | None -> None
    
type WebRedirect =
| HttpRedirect of Uri
| HtmlRedirect of Uri * TimeSpan

type BrowserState =
    { Cookies: Cookie list
      AbsoluteUrl:Uri
      Referer:string
      Request:RawRequest
      Response:RawResponse }
    member x.WebPage (?autoDetectCharsetEncoding:bool) : WebPage =
        let a = match autoDetectCharsetEncoding with | Some b -> b | None -> false
        { AbsoluteUrl = x.AbsoluteUrl
          Request=x.Request
          Response=x.Response
          AutoDetectCharsetEncoding=a
          Resources = []
          BaseUrl = x.AbsoluteUrl.AbsolutePath }
    
type ScrapingBrowser() =
    let cookieContainer = CookieContainer()
    let tryFindRedirect (headers:WebHeaderCollection) (body:byte array option) =
        if headers.["Location"] |> isNull |> not
        then
            let location = headers.["Location"] |> Uri
            Some (HttpRedirect location)
        else
            None

    member val UserAgent = FakeUserAgent.Chrome24 with get,set
    member val Referer = "" with get,set
    member val Proxy:IWebProxy = WebRequest.DefaultWebProxy with get,set
    member val DecompressionMethod:DecompressionMethods option = None with get,set
    member val Language:CultureInfo = CultureInfo.CreateSpecificCulture("EN-US") with get,set
    member val Headers:Dictionary<string, string> = Dictionary<string, string>() with get
    member val CachePolicy:RequestCachePolicy option = None with get, set
    member val Timeout:TimeSpan = TimeSpan.Zero with get,set
    member val KeepAlive:bool = true with get,set
    member val ProtocolVersion:Version = HttpVersion.Version10 with get,set
    member val AutoDetectCharsetEncoding = true with get,set
    member val Encoding = Encoding.UTF8 with get,set
    member val AllowAutoRedirect = true with get,set
    member val TransferEncoding = "" with get,set
    member val SendChunked = false with get,set
    member val IgnoreCookies = false with get,set
    member val UseDefaultCookiesParser = false with get,set
    member val AllowMetaRedirect = false with get,set

    member private x.CreateRequest (url:Uri) (verb:HttpVerb) =
        let request = WebRequest.Create url.AbsoluteUri :?> HttpWebRequest
        request.Method <- verb.ToString()
        request.Referer <- x.Referer
        request.CookieContainer <- cookieContainer
        request.UserAgent <- x.UserAgent.UserAgent
        request.Proxy <- x.Proxy
        match x.DecompressionMethod with | Some d -> request.AutomaticDecompression <- d | None -> ()
        request.AllowAutoRedirect <- false
        request.Headers.["Accept-Language"] <- x.Language.Name
        for h in x.Headers do
            request.Headers.[h.Key] <- h.Value
        x.Headers.Clear()
        if x.Timeout > TimeSpan.Zero then
            request.Timeout <- int x.Timeout.TotalMilliseconds
        request.KeepAlive <- x.KeepAlive;
        request.ProtocolVersion <- x.ProtocolVersion;
        if x.TransferEncoding |> String.IsNullOrWhiteSpace |> not then
            request.SendChunked <- true
            request.TransferEncoding <- x.TransferEncoding
        else
            request.SendChunked <- x.SendChunked
        request

    member x.SetCookies (cookieUrl:Uri) exp =
        let parser = CookiesParser(cookieUrl.Host)
        let cookies = parser.ParseCookies exp
        let previousCookies = cookieContainer.GetCookies cookieUrl
        for cookie in cookies do
            let c = previousCookies.[cookie.Name]
            if c |> isNull |> not
            then c.Value <- cookie.Value
            else cookieContainer.Add cookie

    member x.GetWebResponse (url:Uri) (request:HttpWebRequest) =
        x.Referer <- url.AbsoluteUri
        async {
            let! r = request.GetResponseAsync() |> Async.AwaitTask
            let response = r :?> HttpWebResponse
            let headers = response.Headers
            if x.IgnoreCookies |> not then
                let cookiesExpression = headers.["Set-Cookie"]
                if cookiesExpression |> String.IsNullOrEmpty |> not then
                    let cookieUrl = sprintf "%s://%s:%d/" 
                                        response.ResponseUri.Scheme
                                        response.ResponseUri.Host
                                        response.ResponseUri.Port |> Uri
                    if x.UseDefaultCookiesParser
                    then cookieContainer.SetCookies(cookieUrl, cookiesExpression);
                    else x.SetCookies url cookiesExpression
            return response
        }

    member private x.GetResponse (url:Uri) (request:HttpWebRequest) (iteration:int) (requestBody:byte array option) =
        if iteration > 5 then failwithf "Redirection loop %d iterations" iteration
        let toHeaderList (headers:WebHeaderCollection) =
            headers.AllKeys 
            |> Seq.map (fun k -> (k, headers.[k]))
            |> Seq.toList
        let getContentEncoding (s:string) =
            try Encoding.GetEncoding(s) with | _ -> x.Encoding
        async {
            match requestBody with
            | Some data ->
                let rqs = request.GetRequestStream()
                use writer = new BinaryWriter(rqs)
                writer.Write data
                writer.Flush()
            | None -> ()
            let! response = x.GetWebResponse url request
            let responseStream = response.GetResponseStream()
            let rq = { Verb = request.Method
                       Url = request.RequestUri
                       HttpVersion = request.ProtocolVersion
                       Headers = request.Headers |> toHeaderList
                       Body = requestBody
                       Encoding = x.Encoding }
            let body = if isNull responseStream
                        then None
                        else
                            use m = new MemoryStream()
                            responseStream.CopyTo m
                            responseStream.Flush()
                            m.Flush()
                            responseStream.Close()
                            m.Position <- 0L
                            Some (m.ToArray())
            match tryFindRedirect response.Headers body with
            | Some(HttpRedirect location) ->
                let data = match requestBody with | Some d -> Some (Buffer d) | None -> None
                let verb = match HttpVerb.Parse request.Method with
                            | Some v -> v
                            | None -> failwithf "Invalid HTTP method %s" request.Method
                return! x.ExecuteRequest (location, verb, data, iteration+1)
            | _ ->
                let rs = { HttpVersion = request.ProtocolVersion
                           StatusCode = response.StatusCode |> int
                           StatusDescription = response.StatusDescription
                           Headers = response.Headers |> toHeaderList
                           Body = body
                           Encoding = getContentEncoding response.ContentEncoding }
                let cookies = [for i in 0..response.Cookies.Count-1 -> response.Cookies.Item(i)]
                return { Cookies = cookies
                         AbsoluteUrl = url
                         Referer = x.Referer
                         Request = rq
                         Response = rs }
        }

    member x.DownloadString(url:Uri) =
        async {
            let request = x.CreateRequest url HttpVerb.Get
            let! state = (x.GetResponse url request 0 None)
            return match state.Response.Body with
                    | Some body -> state.Response.Encoding.GetString body
                    | None -> String.Empty
        }

    member x.DownloadFile(url:Uri) =
        async {
            let request = x.CreateRequest url HttpVerb.Get
            let! response = x.GetWebResponse url request
            return response.GetResponseStream()
        }

    member private x.ExecuteRequest (url:Uri, verb:HttpVerb, data:HttpRequestData option, iteration) =
        async {
            let request = x.CreateRequest url verb
            let path = 
                match verb, data with
                | Get, Some d -> sprintf "%s?%s" url.AbsoluteUri (d.ToRawParams()) |> Uri
                | _ -> url
            let! body =
                async {
                    match verb with
                    | Put
                    | Post -> 
                        request.ContentType <- "application/x-www-form-urlencoded"
                        use stream = new MemoryStream()
                        use writer = new StreamWriter(stream)
                        match data with 
                        | Some d ->
                            let raw = d.ToRawParams()
                            writer.Write raw
                        | None -> ()
                        writer.Flush()
                        return stream.ToArray() |> Some
                    | _ -> return None
                }
            return! x.GetResponse path request iteration body
        }

    member x.NavigateTo (url:Uri, ?verb:HttpVerb, ?data:HttpRequestData) =
        let v = match verb with | Some m -> m | None -> Get
        x.ExecuteRequest (url, v, data, 0)


