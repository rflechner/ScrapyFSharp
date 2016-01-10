namespace ScrapyFSharp

module CssParser =

    open System
    open System.IO
    open System.Text

    type BufferedString = 
        { mutable Content:string }
        static member Empty = { Content=String.Empty }
        member x.IsNullOrEmpty() = x.Content |> String.IsNullOrEmpty
        member x.Chars i = x.Content.Chars i
        member x.RemoveFirst i = 
            if i > x.Content.Length
            then x.Clear()
            else x.Content <- x.Content.Substring(i)
        member x.Length with get() = x.Content.Length
        member x.Clear() = x.Content <- String.Empty
        member x.ToCharArray() = x.Content.ToCharArray()
        member x.SetContent s = x.Content <- s

    type SubBufferedTextReader (reader:TextReader) =
        let buffer:BufferedString = BufferedString.Empty
    
        member x.PeekChar() = 
            if buffer.IsNullOrEmpty()
            then reader.Peek() |> char
            else buffer.Chars 0

        member x.ReadNChar n = 
            if buffer.IsNullOrEmpty()
            then
                let chars = Array.zeroCreate n
                reader.ReadBlock(chars, 0, n) |> ignore
                String(chars)
            elif buffer.Length >= n then
                let s = buffer.Content.Substring(0, n)
                buffer.RemoveFirst n
                s
            else
                let l = buffer.Length - n
                let chars = Array.zeroCreate l
                reader.ReadBlock(chars, 0, n) |> ignore
                let s = buffer.Content
                buffer.Clear()
                s + String(chars)

        member x.Peek() =
            if buffer.IsNullOrEmpty()
            then reader.Peek()
            else buffer.Chars 0 |> int

        member x.Read() = x.ReadNChar 1
        member x.Pop() = x.Read() |> ignore
        member x.Pop(count) = 
            [|0..(count-1)|] |> Array.map (fun _ -> x.ReadChar())
    
        member x.ReadChar() = 
            if buffer.IsNullOrEmpty()
            then x.Read() |> char
            else
                let c = buffer.Chars 0
                buffer.RemoveFirst 1
                c

        member x.PeekNChar n = 
            if n <= 1
            then [|x.PeekChar()|]
            elif buffer.IsNullOrEmpty() then
                let chars = Array.zeroCreate n
                reader.ReadBlock(chars, 0, n) |> ignore
                let s = String(chars)
                buffer.SetContent s
                let b = buffer.ToCharArray()
                b
            else
                let l = n - buffer.Length
                let chars = Array.zeroCreate l
                reader.ReadBlock(chars, 0, l) |> ignore
                buffer.SetContent (buffer.Content + String(chars))
                buffer.ToCharArray()

    let toPattern f c = if f c then Some c else None

    let (|EndOfFile|_|) (c : char) =
        let value = c |> int
        if (value = -1 || value = 65535) then Some c else None

    let (|Whitespace|_|) = toPattern Char.IsWhiteSpace
    let (|LetterDigit|_|) = toPattern Char.IsLetterOrDigit
    let (|Letter|_|) = toPattern Char.IsLetter
    let (|SelectorChar|_|) (c:char) = 
        if Char.IsLetterOrDigit c || (".>-_:()[=],#@".ToCharArray() |> Array.exists (fun i -> i = c) )
        then Some c
        else None

    type CharList = 
        { mutable Contents : char list }
        static member Empty = { Contents = [] }
        override x.ToString() = String(x.Contents |> List.rev |> List.toArray)
        member x.Acc c = x.Contents <- c :: x.Contents
        member x.Length = x.Contents.Length
        member x.Clear() = x.Contents <- []

    type Token = 
        | Selector of string
        | OpenBlock
        | CloseBlock
        | PropertyName of string
        | PropertyValue of string
        | Comment of string
        | Unmanaged of string

    type ReadContext =
        | InSelector
        | InPropertyName
        | InPropertyValue

    type State=
        { Content : CharList ref
          Tokens : Token list ref
          Context : ReadContext ref
          Reader : SubBufferedTextReader }
        static member Create (reader:TextReader) = 
                { Content = ref CharList.Empty
                  Tokens = ref List.Empty
                  Context = ref InSelector
                  Reader = SubBufferedTextReader(reader) }
        member x.Pop() = x.Reader.Read() |> ignore
        member x.Peek() = x.Reader.PeekChar()
        member x.Pop(count) = 
            [|0..(count-1)|] |> Array.map (fun _ -> x.Reader.ReadChar()) |> ignore
    
        member x.Contents = (!x.Content).ToString().Trim()
        member x.ContentLength = (!x.Content).Length
        member x.Acc() = (!x.Content).Acc(x.Reader.ReadChar())
        member x.ClearContent() = x.Content := CharList.Empty
        member private x.Emit (f : unit -> Token) =
            let token = f()
            x.Tokens := token :: !x.Tokens
            x.ClearContent()
        member x.EmitSelector() = x.Emit (fun _ -> Selector(x.Contents))
        member x.EmitOpenBlock() = x.Emit (fun _ -> OpenBlock)
        member x.EmitCloseBlock() = x.Emit (fun _ -> CloseBlock)
        member x.EmitPropertyName() = x.Emit (fun _ -> PropertyName(x.Contents))
        member x.EmitPropertyValue() = x.Emit (fun _ -> PropertyValue(x.Contents))
        member x.EmitComment() = 
            x.Emit (fun _ -> Comment(x.Contents.Substring(0, x.Contents.Length-2)))
        
    type CssBlock = 
        { Selector:string
          Properties:CssProperty list }
        member x.Property p =
            x.Properties
            |> List.tryFind (fun i -> i.Name = p)
            |> function
                | Some v -> Some v.Value
                | None -> None
    and CssProperty = 
        { Name:string
          Value:string }

    type StyleSheet = 
        { Blocks:CssBlock list }
        static member From blocks =
            { Blocks=blocks }
        member x.Block s =
            x.Blocks
            |> List.tryFind (fun b -> b.Selector = s)

    let tokenize txt =

        let (|StartsWith|_|) (state:State,str:string) (c:char) =
            match str.Length with
            | 0 -> None
            | 1 when c = str.Chars(0) -> Some()
            | len -> 
                let chars = state.Reader.PeekNChar len
                let s = chars |> String
                if s = str then Some() else None
        
        let rec parse (state:State) =
            match (state.Peek(), !state.Context) with
            | ':', InPropertyName ->
                state.Pop()
                state.EmitPropertyName()
                state.Context := InPropertyValue
            | ';', InPropertyValue ->
                state.Pop()
                state.EmitPropertyValue()
                state.Context := InPropertyName
            | StartsWith (state,"/*"), _ ->
                parseComment state
            | SelectorChar _, _ -> 
                state.Acc()
            | Whitespace _, InSelector -> state.Acc()
            | '{', InSelector -> 
                state.Pop()
                state.EmitSelector()
                state.EmitOpenBlock()
                state.Context := InPropertyName
            | '}', InPropertyName
            | '}', InPropertyValue ->
                state.Pop()
                state.EmitCloseBlock()
                state.Context := InSelector
            | _, InPropertyName ->
                state.Acc()
            | _, InPropertyValue -> 
                state.Acc()
            | _ -> 
                state.Pop()
                state.ClearContent()
        and parseComment (state:State) =
            state.Pop 2
            while state.Contents.EndsWith "*/" |> not do
                state.Acc()
            state.EmitComment()
    
        let reader = new StringReader(txt)
        let state = State.Create reader
        let next = ref (state.Reader.Peek())
        while !next > 0 do
            parse state
            next := state.Reader.Peek()
        !state.Tokens |> List.rev

    let parse (tokens:Token list) =
        let (|CssProps|_|) (tail:Token list) = 
            let rec iterate (items:Token list) acc =
                match items with
                | PropertyName n :: PropertyValue v :: t ->
                    iterate t ((n,v) :: acc)
                | CloseBlock :: t when acc.Length <= 0 -> None
                | _ :: t -> Some (acc, t)
                | _ -> None
            iterate tail []
        let rec loop (tail:Token list) acc = 
            match tail with
            | Selector s :: OpenBlock :: CssProps (props, t) ->
                {Selector=s; Properties=(props |> List.map(fun (k,v) -> {Name=k;Value=v}))} :: acc
                |> loop t
            | _ :: t -> loop t acc
            | [] -> acc |> List.rev
        loop tokens [] |> StyleSheet.From

    let parseCss = tokenize >> parse

