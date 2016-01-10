namespace ScrapyFSharp

module HtmlRasterizer =

    open System
    open System.IO
    open System.Drawing
    open System.Windows.Forms
    open FSharp.Data
    open ScrapyFSharp.CssParser
    open ScrapyFSharp.CssSelectorExtensions

    module CssGrammar =
        let [<Literal>] public FontSize = "font-size"
        let [<Literal>] public FontFace = "font-face"
        let [<Literal>] public FontColor = "color"
        let [<Literal>] public Width = "width"
        let [<Literal>] public Height = "height"
        let [<Literal>] public BackgroundColor = "background-color"
        let [<Literal>] public Color = "color"
        let [<Literal>] public DisplayMode = "display"
    
    type DomItem = 
        { Node:HtmlNode
          Parent:DomItem option }
        override x.ToString() =
            x.Node.ToString()

    type NodeStyle = 
        { Node:HtmlNode
          Styles:CssBlock list }

    type HtmlStyle = NodeStyle list
    type CssPropertyAggregate = System.Collections.Generic.Dictionary<string, string>

    type CssColor =
        | HexaColor  of string
        | RgbColor   of int * int * int
        | RgbaColor  of int * int * int * int

    //http://www.w3schools.com/cssref/pr_class_display.asp
    type CssDisplayMode =
        | Block
        | InlineMode
        | Flex
        | InlineBlock
        | InlineFlex
        | InlineTable
        | ListItem
        | RunIn
        | TableMode
        | TableCaption
        | TableColumnGroup
        | TableHeaderGroup
        | TableFooterGroup
        | TableRowGroup
        | TableCellMode
        | TableColumnMode
        | TableRowMode
        | DontDisplay
        | InitialMode
        | InheritMode

    let getDeepestNodes (node:HtmlNode) =
        let rec loop (n:DomItem) (acc:DomItem list) = 
            let elements = n.Node.Elements() |> Seq.toList
            if elements |> Seq.isEmpty || (elements.Length = 1 && String.IsNullOrWhiteSpace(elements.Head.Name()))
            then [{Node=elements.Head; Parent=Some(n)}]
            else
                let subnodes = elements 
                               |> Seq.collect (
                                    fun d ->
                                        let i = {Node=d; Parent=Some(n)}
                                        loop i acc
                               )
                               |> Seq.toList
                subnodes |> List.append acc
        loop {Node=node;Parent=None} []

    let rec tryFindNode (items:DomItem list) node =
        let item = items
                   |> List.tryFind (fun i -> i.Node = node)
        match item with
        | Some i -> Some i
        | None ->
            let parents = 
                seq {
                    for i in items do
                        match i.Parent with
                        | Some p -> yield p
                        | None   -> ()
                } |> Seq.toList

            if parents |> List.isEmpty
            then None
            else tryFindNode parents node

    let associateStyle (html:HtmlDocument) (style:StyleSheet) : HtmlStyle =
        seq {
            for block in style.Blocks do
                let nodes = html.CssSelect block.Selector
                for n in nodes do
                    yield (n,style)
        }
        |> Seq.toList
        |> List.groupBy (fun (n,s) -> n)
        |> List.map (
            fun (k,vs) -> 
                let b = vs |> List.collect (fun (_,s) -> s.Blocks)
                { Node=k; Styles=b }
            )

    let findStyle (css:HtmlStyle) (node:HtmlNode) =
        let style = css
                    |> List.filter (fun s -> s.Node = node)
                    |> List.exactlyOne
        style.Styles


    let resolveCssProperties (css:HtmlStyle) (node:HtmlNode) =
        let d = new CssPropertyAggregate()
        let styles = 
            match css |> List.filter (fun s -> s.Node = node) with
            | [] -> []
            | l -> l
                   |> List.exactlyOne
                   |> fun s -> s.Styles
                   |> List.collect (fun s -> s.Properties)
        for s in styles do
            if d.ContainsKey s.Name
            then d.[s.Name] <- s.Value
            else d.Add(s.Name, s.Value)
        d

    let getCssValue (css:HtmlStyle) name defaultValue (item:DomItem) =
        let rec loop (n:DomItem) =
            let d = resolveCssProperties css (n.Node)
            if d.ContainsKey name
            then d.[name]
            else
                match n.Parent with
                | Some p -> loop p
                | None   -> defaultValue
        loop item

    let parseMetric (t:string) =
        if t.EndsWith "px"
        then t.Substring(0, t.Length - 2) |> System.Int32.Parse
        else 0

    let parseDisplayMode (s:string) =
        match s.Trim() with
        | "inline" -> InlineMode
        | "block" -> Block
        | "flex" -> Flex
        | "inline-block" -> InlineBlock
        | "inline-flex" -> InlineFlex
        | "list-item" -> ListItem
        | "run-in" -> RunIn
        | "table" -> TableMode
        | "table-caption" -> TableCaption
        | "table-column-group" -> TableColumnGroup
        | "table-header-group" -> TableHeaderGroup
        | "table-footer-group" -> TableFooterGroup
        | "table-row-group" -> TableRowGroup
        | "table-cell" -> TableCellMode
        | "table-column" -> TableColumnGroup
        | "table-row" -> TableRowMode
        | "none" -> DontDisplay
        | "initial" -> InitialMode
        | "inherit" -> InheritMode
        | _ -> InitialMode

    let calulateHeight (g:Graphics) (node:HtmlNode) (w:int) font =
        let rec loop (n:HtmlNode) h font =
            let text = n.InnerText()
            let s = g.MeasureString(text, font, w)
            printfn "size %A" s
            if n.Elements().IsEmpty
            then int(s.Height)
            else
                let sh = n.Elements() 
                         |> Seq.map(fun e -> loop e 0 font)
                         |> Seq.max
                //int(s.Height) + sh
                sh
        loop node 0 font

    let rec drawText (g:Graphics) (item:DomItem) (rect:RectangleF) font color =
        let brush = new SolidBrush(color)
        g.DrawString(item.Node.InnerText().Trim(), font, brush, rect)

    let drawHtmlNode (html:HtmlDocument) selector (g:Graphics) =
        let deepestNodes = html.Body() |> getDeepestNodes
        let css = html.Descendants "style"
                  |> Seq.collect getDeepestNodes
                  |> fun strings -> System.String.Join("", strings)
                  |> ScrapyFSharp.CssParser.tokenize
                  |> ScrapyFSharp.CssParser.parse
        let styles = associateStyle html css
        let target = selector |> html.CssSelect |> List.exactlyOne
        let props = resolveCssProperties styles target

        match tryFindNode deepestNodes target with
        | Some n ->
            let font = new Font("Times New Roman", 12.f, FontStyle.Regular)
            let w = getCssValue styles CssGrammar.Width "600px" n
                    |> parseMetric
            let h = getCssValue styles CssGrammar.Height "" n
                    |> fun t -> 
                        if String.IsNullOrWhiteSpace t |> not
                        then parseMetric t
                        else calulateHeight g target w font

            let b = getCssValue styles CssGrammar.BackgroundColor "#FFFFFF" n 
                    |> ColorTranslator.FromHtml
            let color = getCssValue styles CssGrammar.Color "#000000" n 
                        |> ColorTranslator.FromHtml
            let displayMode = getCssValue styles CssGrammar.DisplayMode "block" n 
                              |> parseDisplayMode
        
            let brush = new SolidBrush(b)
            let pen = new Pen(b, 1.0f)
        
            match n.Node.Name(), displayMode with
            | _, Block
            | "div", InitialMode
            | "span", InitialMode -> 
                g.FillRectangle(brush, new Rectangle(0,0,w,h))
                drawText g n (new RectangleF(0.f, 0.f, float32 w, float32 h)) font color
            | _ -> ()

            ()
        | None -> ()


