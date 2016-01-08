(*** hide ***)
#I "../../packages/FSharp.Data/lib/net40"
#r "FSharp.Data.dll"

#load "../../src/ScrapyFSharp/Network.fs"

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
Create a browser
*)

let b = ScrapingBrowser()
(** 
Some fake user agents are available
*)
b.UserAgent <- FakeUserAgent.Chrome
async {
    return! b.DownloadString(Uri("http://www.youscribe.com/"))
} |> Async.RunSynchronously

(** 
 end ...
*)
