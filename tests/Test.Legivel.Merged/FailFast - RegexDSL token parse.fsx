﻿#I __SOURCE_DIRECTORY__ 

#time

//#r @"bin/Debug/net45/FSharp.Core.dll"
//#r @"C:\Program Files (x86)\Reference Assemblies\Microsoft\FSharp\.NETFramework\v4.0\4.4.0.0\FSharp.Core.dll"
#r @"bin/Debug/net45/Legivel.Parser.dll"
#r @"bin/Debug/net45/NLog.dll"
#r @"bin/Debug/net45/nunit.framework.dll"

//#r @"bin/Release/net45/FSharp.Core.dll"
//#r @"bin/Release/net45/Legivel.Parser.dll"
//#r @"bin/Release/net45/NLog.dll"

open System
open System.Text.RegularExpressions
open System.Globalization
open System.Text
open Legivel.Parser
open Legivel.TagResolution
open Legivel.Serialization
open Legivel.RepresentationGraph
open Legivel.Common
open NLog
open System.IO
open Legivel.Tokenizer
open NUnit.Framework


#load "nlog.fsx"
#load "RegexDSL-Base.fsx"


open System
open System.Globalization
open Legivel.Tokenizer
open System.Threading
open System
open NLog.Config
open System.ComponentModel
open ``RegexDSL-Base``
open System.Drawing
open System.Numerics

//  ================================================================================================
//  Experimental - Thompson algorithm for Regex parsing
//  ================================================================================================

type CharacterMatch =
    |   NoMatch         //  decided no match
    |   Match           //  decided match

type ThompsonFunc = TokenData -> CharacterMatch

type SingleStepState = {
    Info            :   string
    Transiton       :   ThompsonFunc
    NextState       :   RegexState
}
and SinglePathState = {
    MainState       :   RegexState
    NextState       :   RegexState
}
and DoublePathState = {
    CharCount       :   int
    MainState       :   RegexState
    AlternateState  :   (CharacterMatch*RegexState) option
    NextState       :   RegexState
}
and RepeatDoublePathState = {
    CharCount       :   int
    IterCount       :   int
    Min             :   int
    Max             :   int
    MainState       :   RegexState
    InitialState    :   RegexState
    AlternateState  :   (CharacterMatch*RegexState) option
    NextState       :   RegexState
}
and ParallelPathState = {
    Targets         :   RegexState list
    NextState       :   RegexState
}
and RegexState =
    |   SingleRGS   of  SingleStepState
    |   ConcatRGS   of  SinglePathState
    |   OptionalRGS of  DoublePathState
    |   RepeatRGS   of  RepeatDoublePathState
    |   MultiRGS    of  ParallelPathState
    |   Final
    with
        member this.NextState 
            with get() = 
                match this with
                |   SingleRGS   s -> s.NextState
                |   ConcatRGS   s -> s.NextState
                |   MultiRGS    s -> s.NextState
                |   OptionalRGS s -> s.NextState
                |   RepeatRGS   s -> s.NextState
                |   Final -> Final

        member this.SetNextState ns =
                match this with
                |   SingleRGS   s -> SingleRGS {s  with NextState = ns}
                |   ConcatRGS   s -> ConcatRGS {s  with NextState = ns}
                |   MultiRGS    s -> MultiRGS  {s  with NextState = ns}
                |   OptionalRGS s -> OptionalRGS {s  with NextState = ns}
                |   RepeatRGS   s -> RepeatRGS {s  with NextState = ns}
                |   Final -> Final

        member this.IsFinalValue
            with get() = 
                match this with
                |   Final -> true
                |   _     -> false


type ProcessResult = {
    IsMatch     : CharacterMatch
    NextState   : RegexState
    Reduce      : int
}
    with
        static member Create (m, n, r) = {IsMatch = m; NextState = n; Reduce = r}

let rec processState (td:TokenData) (st:RegexState) =

    let processAlternativeState (cm:CharacterMatch, st:RegexState) = 
        if cm = CharacterMatch.NoMatch ||  st.IsFinalValue then
            ProcessResult.Create (CharacterMatch.NoMatch, Final, 0)
        else
            processState td st

    match st with
    |   SingleRGS s -> 
        s.Transiton td
        |>  function
            |   CharacterMatch.Match   -> ProcessResult.Create (CharacterMatch.Match,   st.NextState, 0)
            |   CharacterMatch.NoMatch -> ProcessResult.Create (CharacterMatch.NoMatch, Final, 0)
    |   ConcatRGS s -> 
        let repeatThisState ns =
            ConcatRGS {
                s with
                    MainState = ns
            }
        let r = processState td s.MainState
        match r.IsMatch with
        |   CharacterMatch.Match when r.NextState.IsFinalValue
                                    -> ProcessResult.Create (CharacterMatch.Match, s.NextState, r.Reduce)
        |   CharacterMatch.Match    -> ProcessResult.Create (CharacterMatch.Match, repeatThisState r.NextState, 0)
        |   CharacterMatch.NoMatch  -> ProcessResult.Create (CharacterMatch.NoMatch, Final, 0)
    |   MultiRGS m ->
        let rr =
            m.Targets
            |>  List.map(processState td)
            |>  List.filter(fun rt -> rt.IsMatch = CharacterMatch.Match)
            |>  List.map(fun rt -> rt.NextState)
        let isFinal = rr |> List.exists(fun s -> s.IsFinalValue)
        if isFinal then
            ProcessResult.Create (CharacterMatch.Match, st.NextState, 0)
        else
            if rr.Length = 0 then 
                ProcessResult.Create (CharacterMatch.NoMatch, Final,0)
            else
                ProcessResult.Create (CharacterMatch.Match, MultiRGS { m with Targets = rr }, 0)
    |   OptionalRGS o ->
        let ost = 
            { o with
                AlternateState = if o.AlternateState.IsNone then Some (CharacterMatch.Match, o.NextState) else o.AlternateState
            }

        let rm = processState td ost.MainState
        let ra = processAlternativeState (ost.AlternateState.Value)

        let continueThisState ns =
            OptionalRGS {
                ost with
                    MainState = ns
                    AlternateState = Some(ra.IsMatch, ra.NextState)
                    CharCount = ost.CharCount + 1
            }

        match (rm.IsMatch, ra.IsMatch) with
        |   (CharacterMatch.Match, _)  when rm.NextState.IsFinalValue  
                                                                -> ProcessResult.Create (CharacterMatch.Match, ost.NextState, rm.Reduce)
        |   (CharacterMatch.Match, _)                           -> ProcessResult.Create (CharacterMatch.Match, continueThisState rm.NextState, 0)
        |   (CharacterMatch.NoMatch, CharacterMatch.Match)      -> ProcessResult.Create (CharacterMatch.Match, ra.NextState, 0)
        |   (CharacterMatch.NoMatch, CharacterMatch.NoMatch)    -> ProcessResult.Create (CharacterMatch.Match, ost.NextState, ost.CharCount+1)
    |   RepeatRGS zom ->
        let hasMax = zom.Max > zom.Min
        let zost = 
            { zom with
                MainState       = if zom.MainState.IsFinalValue then zom.InitialState else zom.MainState
                AlternateState  = if zom.AlternateState.IsNone then Some (CharacterMatch.Match, zom.NextState) else zom.AlternateState
            }

        let rm = processState td zost.MainState
        let ra = processAlternativeState (zost.AlternateState.Value)

        let iterBelowMax zost = (not(hasMax) || zost.IterCount + 1 < zost.Max)

        let continueThisState ns =
            RepeatRGS {
                zost with
                    MainState = ns
                    AlternateState = Some(ra.IsMatch, ra.NextState)
                    CharCount = zost.CharCount + 1
            }

        let repeatThisState z =
            RepeatRGS 
                { z with
                    MainState = z.InitialState
                    AlternateState = None
                    CharCount = 0
                    IterCount = z.IterCount + 1
                }

        match (rm.IsMatch, ra.IsMatch) with
        |   (CharacterMatch.Match, _)  when rm.NextState.IsFinalValue  -> 
            if iterBelowMax zost then // do repeat
                ProcessResult.Create (CharacterMatch.Match, repeatThisState zost, 0)
            else
               ProcessResult.Create (CharacterMatch.Match, zost.NextState, 0) 
        |   (CharacterMatch.Match, _)                           -> ProcessResult.Create (CharacterMatch.Match, continueThisState rm.NextState, 0)
        |   (CharacterMatch.NoMatch, CharacterMatch.Match)      -> 
            if (iterBelowMax zost && zost.Min <= zost.IterCount) then
                ProcessResult.Create (CharacterMatch.Match, ra.NextState, 0)
            else
                ProcessResult.Create (CharacterMatch.NoMatch, Final, zost.CharCount+1)
        |   (CharacterMatch.NoMatch, CharacterMatch.NoMatch)    -> 
            if (iterBelowMax zost && zost.Min <= zost.IterCount) then
                ProcessResult.Create (CharacterMatch.Match, zost.NextState, zost.CharCount+1)
            else
                ProcessResult.Create (CharacterMatch.NoMatch, Final, zost.CharCount+1)

    |   Final   -> ProcessResult.Create (CharacterMatch.Match, Final, 0)


let boolToMatch() = 
    function
    |   true ->  CharacterMatch.Match
    |   false -> CharacterMatch.NoMatch

let plainParser (pl:Plain) =
    let fn =
        fun (td:TokenData) ->
            pl.``fixed`` = td.Source 
            |>  boolToMatch()
    SingleRGS { Transiton = fn; NextState = Final; Info = pl.``fixed``}

let oneInSetParser (ois:OneInSet) =
    let fn =
        fun (td:TokenData) ->
            if uint32(td.Token) >= 0b0100_0000_0000_0000_0000_0000_0000_0000u then
                ois.Token |> List.exists(fun e -> e=td.Token)
            else
                let checkItWith = ois.TokenQuickCheck.Force()
                (checkItWith &&& uint32(td.Token) > 0u)
            |>  boolToMatch()
    SingleRGS { Transiton = fn; NextState = Final; Info = sprintf "(%s)-(%s)" ois.mainset ois.subtractset}


let oredParser (rl:RegexState list) =
    MultiRGS {
        Targets     = rl
        NextState   = Final
    }


let concatParser (rl:RegexState list) = 
    let rec knit (acc:RegexState) (l:RegexState list) =
        match l with
        |   []     -> acc 
        |   h :: t -> knit (h.SetNextState acc) t
    let ccc =
        rl
        |>  List.rev
        |>  knit Final
    ConcatRGS { MainState =ccc; NextState = Final}

let optionalParser (ro:RegexState) =
    OptionalRGS {MainState = ro; AlternateState = None; NextState = Final; CharCount = 0}

let zeroOrMoreParser (ro:RegexState) = 
    RepeatRGS 
        {
            Min = 0
            Max = -1
            MainState = ro
            InitialState = ro
            AlternateState = None
            NextState = Final
            CharCount = 0
            IterCount = 0
        }

let oneOrMoreParser (ro:RegexState) = 
    RepeatRGS 
        {
            Min = 1
            Max = -1
            MainState = ro
            InitialState = ro
            AlternateState = None
            NextState = Final
            CharCount = 0
            IterCount = 0
        }

let RangeParser (ro:RegexState) mn mx = 
    RepeatRGS 
        {
            Min = mn
            Max = mx
            MainState = ro
            InitialState = ro
            AlternateState = None
            NextState = Final
            CharCount = 0
            IterCount = 0
        }

//let itemRangeParser minRep maxRep (ts:ThompsonState, td:TokenData) =
//    let quitOnkUpperBound (r:ThompsonState) = 
//        if r.IterationCount = (maxRep-1) then r.SetState CharacterMatch.Match else r.SetState CharacterMatch.Indecisive
//    let r = (ts,td) |> (ts.FunctionState |> List.head |> snd)
//    match r.State with
//    |   CharacterMatch.Match        -> quitOnkUpperBound r |> iterate |> reinit
//    |   CharacterMatch.MatchContinue
//    |   CharacterMatch.Indecisive   -> r
//    |   CharacterMatch.MatchReduce  -> quitOnkUpperBound r |> iterate 
//    |   CharacterMatch.NoMatch      ->
//        if r.IterationCount < minRep || r.IterationCount >= maxRep then
//            r
//        else
//            r.SetState (CharacterMatch.MatchReduce)
//    |   _ -> failwith "Illegal state"


let CreatePushParser (rgx:RGXType) =
    let rec getParser rgx : (RegexState) =
        match rgx with
        |   Plain    pl -> 
            if pl.``fixed``.Length > 1 then
                pl.OptimizeOnce()
                getParser (Concat pl.optimized) 
            else
                plainParser pl
        |   OneInSet ois -> oneInSetParser ois
        |   Or       l ->
            let orList = l |> List.map(fun i -> getParser i)
            oredParser orList 
        |   Concat   l ->
            l 
            |>  List.rev
            |>  List.map(fun i -> getParser i)
            |>  concatParser
        |   Optional   t -> 
            let orp = getParser t
            optionalParser orp
            
        |   ZeroOrMore t -> 
            let rp = getParser t
            zeroOrMoreParser rp
        |   OneOrMore  t -> 
            let rp = getParser t
            oneOrMoreParser rp
        |   IterRange(t,mx,mno) ->
            let rp = getParser t
            match mno with
            |   Some(mn) ->  RangeParser rp mn mx
            |   None     ->  RangeParser rp mx mx

        //|   ZeroOrMoreNonGreedy t -> sprintf "(?:%O)*?" t

        //|   OneOrMoreNonGreedy  t -> sprintf "(?:%O)+?" t
        //|   Group      t -> sprintf "(%O)" t
        //|   IterRange(t,mx,mno) ->
        //    ThompsonState.Create(),
        //    match mno with
        //    |   Some(mn) ->  itemRangeParser funcToRepeat mn mx
        //    |   None     ->  itemRangeParser funcToRepeat mx mx

    getParser rgx

let EndOfStream = TokenData.Create (Token.EOF) ""


let processor (streamReader:RollingStream<TokenData>) (rst:RegexState) =
    let startPos = streamReader.Position
    let rec rgxProcessor (streamReader:RollingStream<TokenData>) (rst:RegexState) (matched) =
        let tk = streamReader.Get()
        let rt = processState tk rst
        match rt.IsMatch with
        |   CharacterMatch.Match   -> 
            streamReader.Position <- streamReader.Position - rt.Reduce
            let reduceMatch = (tk.Source::matched) |> List.skip rt.Reduce
            if rt.NextState.IsFinalValue then
                (CharacterMatch.Match, reduceMatch)
            else
                rgxProcessor streamReader rt.NextState (tk.Source::matched)
        |   CharacterMatch.NoMatch when rt.Reduce = 0 -> 
            streamReader.Position <- startPos
            (CharacterMatch.NoMatch, [])
        |   CharacterMatch.NoMatch  -> 
            streamReader.Position <- streamReader.Position - rt.Reduce
            let reduceMatch = matched |> List.skip rt.Reduce
            rgxProcessor streamReader rt.NextState reduceMatch

    rgxProcessor streamReader rst []



let expected (str:string) =
    str.ToCharArray() 
    |> List.ofArray 
    |> List.map(fun c -> c.ToString())
    |> List.rev

[<Test>]
let ``Parse Plain Character - sunny day``() =
    let rgxst = RGP("A", [Token.``c-printable``]) |> CreatePushParser
    let yaml = "A"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)


[<Test>]
let ``Parse Plain String - sunny day``() =
    let rgxst = RGP("ABC", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.NoMatch, r)
    Assert.AreEqual("A", streamReader.Get().Source)


[<Test>]
let ``Parse Plain String concats - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``]) + RGP("CD", [Token.``c-printable``]) + RGP("EF", [Token.``c-printable``])|> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)


[<Test>]
let ``Parse Plain String Ored - sunny day``() =
    let rgxst = RGP("ABCE", [Token.``c-printable``]) ||| RGP("ABD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABDE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual("E", streamReader.Get().Source)

    let yaml = "ABCE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

[<Test>]
let ``Parse RGO Character - sunny day``() =
    let rgxst = RGO("A-", [Token.``c-printable``;Token.``t-hyphen``]) |> CreatePushParser

    let yaml = "-"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "A"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(["A"], m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

[<Test>]
let ``Parse Plain String with optional end - sunny day``() =
    let rgxst = RGP("ABC", [Token.``c-printable``])  + OPT(RGP("E", [Token.``c-printable``])) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(["A"; "B"; "C"] |> List.rev, m)
    Assert.AreEqual(expected "ABC", m)
    Assert.AreEqual("D", streamReader.Get().Source)

    let yaml = "ABCE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(["A"; "B"; "C"; "E"] |> List.rev, m)
    Assert.AreEqual(expected "ABCE", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)


[<Test>]
let ``Parse Plain String with optional middle - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OPT(RGP("CDF", [Token.``c-printable``])) + RGP("CDEF", [Token.``c-printable``])  |> CreatePushParser

    let yaml = "ABCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(["A"; "B"; "C"; "D"; "E"; "F"] |> List.rev, m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABCDFCDEF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(["A"; "B"; "C"; "D"; "F"; "C"; "D"; "E"; "F"] |> List.rev, m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

[<Test>]
let ``Parse Zero Or More in the middle - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABCD", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABCDE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABCD", m)
    Assert.AreEqual("E", streamReader.Get().Source)

    let yaml = "ABECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABECD", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEECD", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABEEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEEECD", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

[<Test>]
let ``Parse Zero Or More at the end - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + ZOM(RGP("E", [Token.``c-printable``])) |> CreatePushParser

    let yaml = "AB"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "AB", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "AB", m)
    Assert.AreEqual("C", streamReader.Get().Source)

    let yaml = "ABEC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABE", m)
    Assert.AreEqual("C", streamReader.Get().Source)

    let yaml = "ABEEC"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEE", m)
    Assert.AreEqual("C", streamReader.Get().Source)

    let yaml = "ABEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEE", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)


[<Test>]
let ``Parse One Or More in the middle - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("E", [Token.``c-printable``])) + RGP("CD", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABCD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.NoMatch, r)
    Assert.AreEqual("A", streamReader.Get().Source)

    let yaml = "ABECDF"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABECD", m)
    Assert.AreEqual("F", streamReader.Get().Source)

    let yaml = "ABEECD"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEECD", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let rgxst = RGP("AB", [Token.``c-printable``])  + OOM(RGP("EF", [Token.``c-printable``])) + RGP("ED", [Token.``c-printable``]) |> CreatePushParser

    let yaml = "ABEFED"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEFED", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABED"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.NoMatch, r)
    Assert.AreEqual("A", streamReader.Get().Source)


[<Test>]
let ``Parse Range at the end - sunny day``() =
    let rgxst = RGP("AB", [Token.``c-printable``])  + Range(RGP("E", [Token.``c-printable``]), 2,3) |> CreatePushParser

    let yaml = "ABEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEE", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABEEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEEE", m)
    Assert.AreEqual(Token.EOF, streamReader.Get().Token)

    let yaml = "ABEEEE"
    let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    let (r,m) = processor streamReader rgxst
    Assert.AreEqual(CharacterMatch.Match, r)
    Assert.AreEqual(expected "ABEEE", m)
    Assert.AreEqual("E", streamReader.Get().Source)

    //let yaml = "ABC"
    //let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    //let (r,m) = processor streamReader rgxst
    //Assert.AreEqual(CharacterMatch.Match, r)
    //Assert.AreEqual(expected "AB", m)
    //Assert.AreEqual("C", streamReader.Get().Source)

    //let yaml = "ABEC"
    //let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    //let (r,m) = processor streamReader rgxst
    //Assert.AreEqual(CharacterMatch.Match, r)
    //Assert.AreEqual(expected "ABE", m)
    //Assert.AreEqual("C", streamReader.Get().Source)

    //let yaml = "ABEEC"
    //let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    //let (r,m) = processor streamReader rgxst
    //Assert.AreEqual(CharacterMatch.Match, r)
    //Assert.AreEqual(expected "ABEE", m)
    //Assert.AreEqual("C", streamReader.Get().Source)

    //let yaml = "ABEE"
    //let streamReader = RollingStream<_>.Create (tokenProcessor yaml) EndOfStream
    //let (r,m) = processor streamReader rgxst
    //Assert.AreEqual(CharacterMatch.Match, r)
    //Assert.AreEqual(expected "ABEE", m)
    //Assert.AreEqual(Token.EOF, streamReader.Get().Token)


``Parse Plain Character - sunny day``()

``Parse Plain String - sunny day``()

``Parse Plain String Ored - sunny day``()

``Parse RGO Character - sunny day``()

``Parse Plain String concats - sunny day``()

``Parse Plain String with optional end - sunny day``()

``Parse Plain String with optional middle - sunny day``()

``Parse Zero Or More in the middle - sunny day``()

``Parse Zero Or More at the end - sunny day``()

``Parse One Or More in the middle - sunny day``()

``Parse Range at the end - sunny day``()


