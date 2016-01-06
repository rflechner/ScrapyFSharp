#I "../../packages/FSharp.Data/lib/net40"
#r "FSharp.Data.dll"

#load "../../src/ScrapyFSharp/Network.fs"

open System
open System.Net
open ScrapyFSharp.Network

let b = ScrapingBrowser()
b.UserAgent <- FakeUserAgent.Chrome
async {
    return! b.DownloadString(Uri("http://www.youscribe.com/"))
} |> Async.RunSynchronously



