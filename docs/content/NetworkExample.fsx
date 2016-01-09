(*** hide ***)
#I "../../packages/FSharp.Data/lib/net40"
#r "FSharp.Data.dll"

#load "../../src/ScrapyFSharp/Network.fs"
#load "../../src/ScrapyFSharp/HtmlCssSelectors.fs"

(** 
# Simulate a browser

This module help you to navigate on a website.
Cookies, Referer, etc are automatically handled.

### Openning namespaces
*)
open System
open System.Net
open ScrapyFSharp.Network

(**
### Creating a browser
*)

let b1 = ScrapingBrowser()
(** 
Some fake user agents are available
*)
b1.UserAgent <- FakeUserAgent.Chrome

(** 
Enable redirect headers hanling
*)
b1.AllowAutoRedirect <- true

(** 
Enable redirects via HTML meta like 'meta http-equiv="refresh"'
*)
b1.AllowMetaRedirect <- true

(**
Sometimes, default .Net cookies parser can throw exception when you are scraping old java websites
So, you can use this workaround.
 *)
b1.UseDefaultCookiesParser <- false

(**
If you are scraping GZIP compressed web pages, you can specify the decompression method.
*)
b1.DecompressionMethod <- Some DecompressionMethods.GZip

(**
## Another syntax to create browser
*)

let b = browser (fun c -> { c with UserAgent=FakeUserAgent.InternetExplorer8 })

(**
## Simply download a raw content as text
*)

let text1 =
    async {
        return! b.DownloadString(Uri "http://www.youscribe.com/")
    } |> Async.RunSynchronously

(*** hide ***)
let text1Truncated = text1.Substring(0, 200) + " ... [truncated]"

(** text1 value is: *)

(*** include-value:text1Truncated ***)
(** 
 end ...
*)

(**
## Simulate a form submit and parsing result

In this example, you can save the browser state after each page download.
It can be usefull if you plan to parallelize your scraping on multiples machines, just pause a job, 
or if you are implementing a saga.

In the following example, we search .Net books on Youscribe
*)

open FSharp.Data
open ScrapyFSharp.CssSelectorExtensions

// small hack because HtmlNode.HtmlText is internal
let nodeText (n:HtmlNode) = 
    n.DescendantsAndSelf() 
    |> Seq.tryFind (fun c -> c.Name() |> String.IsNullOrEmpty)
    |> function
       | Some e -> e.InnerText()
       | None -> n.InnerText()

let books =
    async {
        let! state1 = b.NavigateTo(Uri "http://www.youscribe.com/Search", 
                        Get, HttpRequestData.FormData ["quick_search", ".net"; "theme_id", "99"])
        let homePage = state1.WebPage()
        return 
            match homePage.Html() with 
            | Some html ->
                [ for div in html.CssSelect "div.explore-item.explore-doc .document-infos" do
                    yield nodeText div ]
            | None -> List.empty
    } |> Async.RunSynchronously

(** books value is: *)

(*** include-value:books ***)



