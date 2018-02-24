﻿module Test.Legivel.RegexDSL

open Legivel.Utilities.RegexDSL
open NUnit.Framework
open FsUnitTyped
open System.Collections.Generic
open Legivel.Tokenizer

let EndOfStream = TokenData.Create (Token.EOF) ""

let ReadStream (ip:TokenData list) =
    let q = new Queue<TokenData>(ip)
    (fun () -> if q.Count > 0 then q.Dequeue() else EndOfStream)


module ``Test Regex Constructs``=

    [<Test>]
    let ``Parse Plain Character - sunny day``() =
        let testConstuct = RGP("A", [Token.``c-printable``])

        let yaml = "A"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual true
        tkl 
        |>  List.map(fun td -> td.Token)
        |>  shouldEqual [
            Token.``c-printable``
        ]
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.EOF    

    [<Test>]
    let ``Parse Plain Character - rainy day``() =
        let testConstuct = RGP("A", [Token.``c-printable``])

        let yaml = "-"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual false


    [<Test>]
    let ``Parse RGO Character - sunny day``() =
        let testConstuct = RGO("A-", [Token.``c-printable``;Token.``t-hyphen``])

        let yaml = "-"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual true
        tkl 
        |>  List.map(fun td -> td.Token)
        |>  shouldEqual [
            Token.``t-hyphen``
        ]
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.EOF 


    [<Test>]
    let ``Parse RGO Character - rainy day``() =
        let testConstuct = RGO("A-", [Token.``c-printable``;Token.``t-hyphen``])

        let yaml = "9"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual false


    [<Test>]
    let ``Parse ZOM Character - sunny day``() =
        let testConstuct = ZOM(RGP("A", [Token.``c-printable``]))

        let yaml = "A"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual true
        tkl 
        |>  List.map(fun td -> td.Token)
        |>  shouldEqual [
            Token.``c-printable``
        ]
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.EOF 


    [<Test>]
    let ``Parse ZOM Character - rainy day``() =
        let testConstuct = ZOM(RGP("A", [Token.``c-printable``]))

        let yaml = "-"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual true
        tkl |>  List.length |> shouldEqual 0
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.``t-hyphen``

    [<Test>]
    let ``Parse ZOM infinite loop bug``() =
        let testConstuct = ZOM(OPT(RGP("A", [Token.``c-printable``])))

        let yaml = "-"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens testConstuct

        b   |>  shouldEqual true
        tkl |>  List.length |> shouldEqual 0
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.``t-hyphen``


module ``AssesInput for Block Sequence``=
    let ``l+block-sequence`` =
        let ``b-break`` = RGP("\n", [Token.NewLine])
        let ``c-l-block-seq-entry`` = RGP("-", [Token.``t-hyphen``]) + OOM(RGP(" ", [Token.``t-space``])) + OOM(RGO("\u0009\u0020-\uffff", [Token.``c-printable``; Token.``nb-json``; Token.``t-hyphen``; Token.``ns-dec-digit``]))
        OOM(``c-l-block-seq-entry`` + ``b-break``)


    [<Test>]
    let ``Parse Token Stream - full match``() =
        let yaml = "- 5\n- 10\n- -9\n"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens ``l+block-sequence``

        b   |>  shouldEqual true
        tkl 
        |>  List.map(fun td -> td.Token)
        |>  shouldEqual [
            Token.``t-hyphen``; Token.``t-space``; Token.``ns-dec-digit``; Token.NewLine
            Token.``t-hyphen``; Token.``t-space``; Token.``ns-dec-digit``; Token.``ns-dec-digit``; Token.NewLine
            Token.``t-hyphen``; Token.``t-space``; Token.``t-hyphen``; Token.``ns-dec-digit``; Token.NewLine
        ]
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.EOF
    


    [<Test>]
    let ``Parse Token Stream - partial match``() =
        let yaml = "- 5\n- 10\n- -9 # does not match\n"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens ``l+block-sequence``

        b   |>  shouldEqual true
        tkl 
        |>  List.map(fun td -> td.Token)
        |>  shouldEqual [
            Token.``t-hyphen``; Token.``t-space``; Token.``ns-dec-digit``; Token.NewLine
            Token.``t-hyphen``; Token.``t-space``; Token.``ns-dec-digit``; Token.``ns-dec-digit``; Token.NewLine
        ]

        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.``t-hyphen``

    [<Test>]
    let ``Parse Token Stream - no match``() =
        let yaml = "[ 1, 2, 3 ]"

        let tokens = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
        let (b, tkl) = AssesInput tokens ``l+block-sequence``

        b   |>  shouldEqual false
        tkl |>  shouldEqual []
        tokens.Stream |> Seq.head |> fun td -> td.Token |> shouldEqual Token.``t-square-bracket-start``

