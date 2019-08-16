﻿module Legivel.ThompsonParser

open Legivel.Utilities.RegexDSL

open Legivel.Tokenizer
open System.Drawing
open System.Diagnostics
open NUnit.Framework
open FsUnitTyped
open TestUtils
open System.Text.RegularExpressions


type ExactChar = {
        Char       : char
        ListCheck  : Token list
    }


type OneOfChar = {
        QuickCheck : uint32
        ListCheck  : Token list
    }


type SingleCharMatch =
    |   ExactMatch      of ExactChar
    |   OneInSetMatch   of OneOfChar
    with
        member this.Match (t:TokenData) =
            match this with
            |   ExactMatch    c  ->  if t.Source.Length > 0 then t.Source.[0] = c.Char else false
            |   OneInSetMatch sm ->
                if uint32(t.Token) >= 0b0100_0000_0000_0000_0000_0000_0000_0000u then
                    sm.ListCheck |> List.exists(fun e -> e=t.Token)
                else
                    (sm.QuickCheck &&& uint32(t.Token) > 0u)


type StateId = System.UInt32                


type SinglePathPointer = | SinglePathPointer of StateId
    with
        static member Create i = SinglePathPointer i
        member this.Id 
            with get() = 
                let (SinglePathPointer i) = this
                i
        member this.StatePointer = StatePointer.SinglePathPointer(this)

and MultiPathPointer  = | MultiPathPointer  of StateId
    with
        static member Create i = MultiPathPointer i
        member this.Id 
            with get() = 
                let (MultiPathPointer i) = this
                i
        member this.StatePointer = StatePointer.MultiPathPointer(this)

and StatePointer =
    |   SinglePathPointer of SinglePathPointer
    |   MultiPathPointer  of MultiPathPointer
    member this.Id  
        with get() =
            match this with
            | SinglePathPointer i -> i.Id
            | MultiPathPointer  i -> i.Id
    member this.StatePointer 
        with get() = 
            match this with
            | SinglePathPointer i -> i.StatePointer
            | MultiPathPointer  i -> i.StatePointer

    member this.SinglePathPointerValue 
        with get() =
            match this with
            | SinglePathPointer i -> i
            | MultiPathPointer  i -> failwith "MultiPathPointer has no SinglePathPointer"
    member this.IsSinglePath
        with get() =
            match this with
            | SinglePathPointer i -> true
            | MultiPathPointer  i -> false

            

type RepeatId = | RepeatId of System.UInt32
    with
        member this.Id 
            with get() = 
                let (RepeatId i) = this
                i


let mutable currentId = 0u
let mutable currentRepeatId = 0u
let CreateNewId() =
    currentId <- (currentId + 1u)
    currentId

let CreateNewRepeatId() =
    currentRepeatId <- (currentRepeatId + 1u)
    RepeatId currentRepeatId


type SinglePath = {
        Id         : StateId
        State      : SingleCharMatch
        NextState  : StatePointer 
    }
    with
        static member Create mt nx = { Id = CreateNewId(); State = mt; NextState = nx }
        member this.LinkTo i = { this with NextState = i}
        member this.StatePointer = SinglePathPointer(SinglePathPointer.Create this.Id)
        member this.SinglePathPointer = SinglePathPointer.Create this.Id
        member this.Duplicate() = {this with Id = CreateNewId()}


type MultiPath = {
        Id         : StateId
        States     : SinglePathPointer list
    }
    with
        static member Create mt = { Id = CreateNewId(); States = mt }
        member this.StatePointer = MultiPathPointer(MultiPathPointer.Create this.Id)


type RepeatInit = {
        Id          : StateId
        RepeatId    : RepeatId
        NextState   : SinglePathPointer 
    }
    with
        static member Create id ri nx = { Id = id; RepeatId = ri; NextState = nx }
        member this.StatePointer = SinglePathPointer(SinglePathPointer.Create this.Id)
        member this.SinglePathPointer = SinglePathPointer.Create this.Id


type RepeatIterateOrExit = {
        Id          : StateId
        RepeatId    : RepeatId
        IterateState: StatePointer
        NextState   : StatePointer 
    }
    with
        static member Create id it ri nx = { Id = id; IterateState = it; RepeatId = ri; NextState = nx }
        member this.StatePointer = SinglePathPointer(SinglePathPointer.Create this.Id)
        member this.SinglePathPointer = SinglePathPointer.Create this.Id


type RepeatState = {
        RepeatId  : RepeatId
        Iteration : int
        Min       : int
        Max       : int
    }
    with
        static member Create i n x = {RepeatId = i; Iteration = 0; Min = n; Max = x}
        member this.Iterate() = { this with Iteration = this.Iteration + 1}
        member this.CanExit() = this.Iteration >= this.Min && (this.Max <= 0 || this.Iteration < this.Max)
        member this.MustExit() = this.Max > 0 && this.Iteration >= this.Max


type EmptyPath = {
        Id         : StateId
        NextState  : StatePointer 
    }
    with
        static member Create id nx = { Id = id; NextState = nx }
        member this.LinkTo i = { this with NextState = i}
        member this.StatePointer = SinglePathPointer(SinglePathPointer.Create this.Id)
        member this.SinglePathPointer = SinglePathPointer.Create this.Id

let PointerToStateFinal = SinglePathPointer (SinglePathPointer.Create 0u) 


type StateNode =
    |   SinglePath of SinglePath
    |   MultiPath  of MultiPath
    |   EmptyPath  of EmptyPath
    |   RepeatInit of RepeatInit
    |   RepeatStart of EmptyPath
    |   RepeatIterOrExit of RepeatIterateOrExit
    |   NoMatch of StateId
    with
        member this.Id 
            with get() =
                match this with
                |   SinglePath d -> d.Id
                |   MultiPath  d -> d.Id
                |   EmptyPath  d -> d.Id
                |   RepeatInit d -> d.Id
                |   RepeatStart d -> d.Id
                |   RepeatIterOrExit  d -> d.Id
                |   NoMatch d -> d

        member this.IsEmptyPathValue 
            with get() =
                match this with
                |   EmptyPath _ -> true
                |   _ -> false
        
        member this.IsRepeatStartValue
            with get() =
                match this with
                |   RepeatStart _ -> true
                |   _ -> false

        member this.NextState 
            with get() =
                match this with
                |   SinglePath d -> d.NextState.Id
                |   EmptyPath  d -> d.NextState.Id
                |   RepeatInit d -> d.NextState.Id
                |   RepeatStart d -> d.NextState.Id
                |   RepeatIterOrExit  d -> d.NextState.Id
                |   MultiPath  _ -> failwith "Multipath has no single nextstate"
                |   NoMatch _ -> failwith "NoMatch has no single nextstate"

        member this.NextStatePtr 
            with get() =
                match this with
                |   SinglePath d -> d.NextState
                |   EmptyPath  d -> d.NextState
                |   RepeatInit d -> d.NextState.StatePointer
                |   RepeatStart d -> d.NextState
                |   RepeatIterOrExit  d -> d.NextState
                |   MultiPath  _ -> failwith "Multipath has no single nextstate"
                |   NoMatch _ -> failwith "NoMatch has no single nextstate"


        member this.SetNextState i =
                match this with
                |   SinglePath d -> SinglePath { d with NextState = i }
                |   EmptyPath  d -> EmptyPath { d with NextState = i }
                |   RepeatStart d -> RepeatStart { d with NextState = i }
                |   RepeatIterOrExit  d -> RepeatIterOrExit  { d with NextState = i }
                |   _ -> failwith "Illegal to set nextstate"

        member this.SetNextState i =
                match this with
                |   RepeatInit d -> RepeatInit { d with NextState = i }
                |   _ -> this.SetNextState (i.StatePointer)

        member this.StatePointer  
            with get() =
                match this with
                |   SinglePath d -> d.StatePointer
                |   MultiPath  d -> d.StatePointer
                |   EmptyPath  d -> d.StatePointer
                |   RepeatInit d -> d.StatePointer
                |   RepeatStart d -> d.StatePointer
                |   RepeatIterOrExit  d -> d.StatePointer
                |   NoMatch d -> SinglePathPointer (SinglePathPointer.Create d)

        member this.SinglePathPointer
            with get() =
                match this with
                |   SinglePath d -> d.SinglePathPointer
                |   EmptyPath  d -> d.SinglePathPointer
                |   RepeatInit d -> d.SinglePathPointer
                |   RepeatStart d -> d.SinglePathPointer
                |   RepeatIterOrExit  d -> d.SinglePathPointer
                |   MultiPath  d -> failwith "Multipath has no SinglePathPointer"
                |   NoMatch d -> SinglePathPointer.Create d


type NFAMachine = {
        Start   : StatePointer
        States  : StateNode list
        Repeats : RepeatState list
    }
    with
        static member Create (i, s, r) = { States = s; Start = i; Repeats = r}


let qcOneInSet ls = ls |> List.fold(fun s i -> s ||| uint32(i)) 0u
    
let createSpp i = SinglePathPointer (SinglePathPointer.Create i)

let createSinglePathFromRgx rgx =
    match rgx with
    |   Plain       d ->
        let sp = SinglePath.Create (ExactMatch({ Char = d.``fixed``.[0]; ListCheck = d.Token })) PointerToStateFinal
        sp.StatePointer, [SinglePath sp], []
    |   OneInSet    d -> 
        let listCheck = d.Token'.Force()
        let quickCheck = qcOneInSet listCheck
        let sp = SinglePath.Create (OneInSetMatch({ QuickCheck = quickCheck; ListCheck = listCheck})) PointerToStateFinal
        sp.StatePointer, [SinglePath sp], []
    |   _ -> failwith "Not a single char match"


let SortStateNodes lst =
    lst
    |>  List.sortWith(fun c1 c2 ->
        match (c1, c2) with
        |   (EmptyPath _,  MultiPath _) -> 1
        |   (SinglePath _, MultiPath _) -> -1
        |   (MultiPath  _, EmptyPath _) -> -1
        |   (MultiPath _, SinglePath _) -> 1
        |   (EmptyPath _, SinglePath _) -> 1
        |   (SinglePath _, EmptyPath _) -> -1
        |   (RepeatInit _, SinglePath _) -> 1
        |   (SinglePath _, RepeatInit _) -> -1
        |   (RepeatInit _, EmptyPath _) ->  -1
        |   (EmptyPath _, RepeatInit _) -> 1
        |   (RepeatIterOrExit _, SinglePath _) -> 1
        |   (SinglePath _, RepeatIterOrExit _) -> -1
        |   (RepeatIterOrExit _, EmptyPath _) ->  -1
        |   (EmptyPath _, RepeatIterOrExit _) -> 1
        |   _ -> 0
    )


let nonGenericTokens =
    [   Token.``t-space``; Token.``t-tab``; Token.NewLine; Token.``t-hyphen``; Token.``t-plus``; Token.``t-questionmark``; Token.``t-colon``;
        Token.``t-dot``; Token.``t-square-bracket-start``; Token.``t-square-bracket-end``; Token.``t-curly-bracket-start``; Token.``t-curly-bracket-end``;
        Token.``t-hash``; Token.``t-ampersand``; Token.``t-asterisk``; Token.``t-quotationmark``; Token.``t-pipe``; Token.``t-gt``;
        Token.``t-single-quote``; Token.``t-double-quote``; Token.``t-percent``; Token.``t-commat``; Token.``t-tick``; Token.``t-forward-slash``; Token.``t-equals``;
        Token.``ns-dec-digit``; Token.``c-escape``; Token.``t-comma``]
    |>   Set.ofList



type NormalizedExactChar = { Token : Token; IdSp : StatePointer; Next : StatePointer ; ExactChar : char }
type NormalizedOneInSet  = { Token : Token; IdSp : StatePointer; Next : StatePointer }

type NormalizedForRefactoring =
    |   ExactMatchItem of NormalizedExactChar
    |   OneInSetItem of NormalizedOneInSet


let normalizeForRefactoring (stNodes:StateNode list) =
    stNodes
    |>  List.fold(fun s e ->
        match e with
        |   SinglePath sp -> 
            match sp.State with
            |   ExactMatch ch       ->  [ExactMatchItem { Token = ch.ListCheck.Head; IdSp = sp.StatePointer; Next = sp.NextState ; ExactChar = ch.Char}]
            |   OneInSetMatch ois   ->  
                ois.ListCheck
                |>  List.map(fun t  ->   OneInSetItem { Token = t; IdSp = sp.StatePointer; Next = sp.NextState })
            :: s
        |   _  -> s
    ) []
    |>  List.collect id


type RefactoringCanditate =
    |   PlainMerge          of char  * (NormalizedExactChar list)
    |   OneInSetOverlap     of Token * (NormalizedOneInSet list)
    |   CrossTypeOverlap    of Token * (NormalizedExactChar list) * (NormalizedOneInSet list)


let getPlainMerges (lst:NormalizedForRefactoring list) =
    lst
    |>  List.fold(fun s e -> 
        match e with
        |   ExactMatchItem e -> e :: s
        |   _ -> s
    ) []
    |>  List.groupBy(fun e -> e.ExactChar)


let getOneInSetOverlap (lst:NormalizedForRefactoring list) =
    lst
    |>  List.fold(fun s e ->
        match e with
        |   OneInSetItem e -> e :: s
        |   _ -> s
    ) []
    |>  List.groupBy(fun e -> e.Token)


let getCrossTypeOverlap (lst:NormalizedForRefactoring list) =
    lst
    |>  List.groupBy(fun e ->
        match e with
        |   ExactMatchItem i -> i.Token
        |   OneInSetItem   i -> i.Token
    )
    |>  List.filter(fun (t, _) -> nonGenericTokens |> Set.contains t)
    |>  List.fold(fun s (t, nlst) ->
        let (ecl, osl) =
            nlst
            |>  List.fold(fun (s1,s2) e ->
                match e with
                |   ExactMatchItem d -> (d::s1, s2)
                |   OneInSetItem   d -> (s1, d::s2)
            ) ([],[])
        (t,ecl, osl) :: s
    ) []

let getRefactoringCanditates (lst:NormalizedForRefactoring list) =
    let l1 =
        lst
        |>  getPlainMerges
        |>  List.filter(fun (c,l) -> l.Length > 1)
        |>  List.map(fun e -> PlainMerge e)
    let l2 =
        lst
        |>  getOneInSetOverlap
        |>  List.filter(fun (c,l) -> l.Length > 1)
        |>  List.map(fun e -> OneInSetOverlap e)
    let l3 =
        lst
        |>  getCrossTypeOverlap
        |>  List.filter(fun (c,l1,l2) -> l1.Length > 0 && l2.Length > 0)
        |>  List.map(fun e -> CrossTypeOverlap e)
    l1 @ l2 @ l3


let wsUpdate (n:StateNode) (ws:Map<StateId, StateNode>) = ws.Remove(n.Id).Add(n.Id, n)
let wsAdd (n:StateNode) (ws:Map<StateId, StateNode>) = ws.Add(n.Id, n)


let appendStateIdToAllFinalPathNodes (entryStartId:StatePointer) (entryStateList:StateNode list) (concatPtr:StatePointer) =
    let workingSet = entryStateList |> List.map(fun n -> n.Id, n) |> Map.ofList
    let passedNodes = Set.empty<StateId>
    
    let rec traverse (prev:StatePointer) (current:StatePointer) (workingSet:Map<StateId, StateNode>) (passedNodes:Set<StateId>) (concatPtr:StatePointer) =
        if  current.Id = 0u || current.Id = concatPtr.Id || passedNodes.Contains (current.Id) then workingSet 
        else
            let node = workingSet.[current.Id]
            match node with
            |   MultiPath  d -> 
                d.States 
                |> List.fold(fun ws stid -> traverse d.StatePointer (stid.StatePointer) ws (passedNodes.Add d.Id) concatPtr) workingSet
            |   EmptyPath  d  when d.NextState.Id = PointerToStateFinal.Id -> 
                match workingSet.[prev.Id] with
                |   SinglePath sp -> 
                    let p = SinglePath({ sp with NextState = concatPtr})
                    wsUpdate p workingSet
                |   MultiPath  _ -> workingSet
                |   RepeatIterOrExit   re  -> 
                    let p = RepeatIterOrExit({ re with NextState = concatPtr})
                    wsUpdate p workingSet
                |   _ -> failwith "Not implemented yet"
            |   EmptyPath     d -> traverse d.StatePointer (d.NextState) workingSet (passedNodes.Add (d.Id)) concatPtr
            |   RepeatIterOrExit    d -> traverse d.StatePointer (d.NextState) workingSet (passedNodes.Add (d.Id)) concatPtr
            |   RepeatInit   d -> traverse d.StatePointer (d.NextState.StatePointer) workingSet (passedNodes.Add (d.Id)) concatPtr
            |   RepeatStart d -> traverse d.StatePointer (d.NextState) workingSet (passedNodes.Add (d.Id)) concatPtr
            |   SinglePath d ->
                if d.NextState.Id = 0u then
                    let p = SinglePath({ d with NextState = concatPtr})
                    wsUpdate p workingSet
                else
                    traverse d.StatePointer (d.NextState) workingSet (passedNodes.Add (d.Id)) concatPtr
            |   NoMatch _ -> workingSet
    traverse PointerToStateFinal entryStartId workingSet passedNodes concatPtr
    |> Map.toList |> List.map(snd)


let rec refactorCommonPlains (sil:SinglePathPointer list, snl:StateNode list) =
    let stMap = snl |> List.map(fun e -> e.Id, e) |> Map.ofList

    //  holds all plain (exact char matches) for which there are multiple occurences in ``sil``
    let stnl = 
        sil 
        |>  List.map(fun sp -> stMap.[sp.Id])
        |>  normalizeForRefactoring
        |>  getPlainMerges
        |>  List.filter(fun (_,lst) -> List.length lst > 1)    // only with more than one occurrence (ie only ambigious situations)

    if stnl.Length = 0 then
        (sil, snl)
    else
        let rec refactorPlains (sil:SinglePathPointer list) (snl:StateNode list) (stnl:(char * (NormalizedExactChar list)) list) =
            match stnl with
            |   []  -> (sil, snl)
            |   hd :: tail ->
                let target = hd |> snd |> List.map(fun e -> (e.IdSp, e.Next))
                let primary = 
                    let id = fst target.Head
                    snl |>  List.find(fun e -> e.Id = id.Id)
                let (filterIds, nextIds) = target |> List.unzip
                let silNew, snlNew = 
                    let (siln, snln) = refactorCommonPlains (nextIds |> List.map(fun e -> e.SinglePathPointerValue), snl)
                    let snlnMap = snln |> List.map(fun e -> e.Id, e) |> Map.ofList
                    let filtIdSet = filterIds |> List.map(fun e -> e.Id) |> Set.ofList

                    let silNew = primary.SinglePathPointer :: (sil |> List.filter(fun e -> not(filtIdSet.Contains e.Id)))
                    if siln.Length = 1 then
                        silNew, (primary.SetNextState siln.Head) :: (snln |>  List.filter(fun e -> filterIds |> List.exists(fun x -> x.Id = e.Id) |> not))
                    else 
                        let isAllEmpty = 
                            siln 
                            |> List.forall(fun e -> 
                                let nd = snl |> List.find(fun sn -> sn.Id = e.Id)
                                nd.IsEmptyPathValue
                            )
                        if isAllEmpty then
                            let link = snl.Head
                            silNew, (primary.SetNextState link.StatePointer) :: link :: (snln |>  List.filter(fun e -> filterIds |> List.exists(fun x -> x.Id = e.Id) |> not))
                        else
                            let silnSorted =
                                siln
                                |>  List.map(fun sp -> snlnMap.[sp.Id])
                                |>  SortStateNodes
                                
                            let bundle = MultiPath(MultiPath.Create (silnSorted|>List.map(fun sn -> sn.SinglePathPointer)))
                            silNew, (primary.SetNextState bundle.StatePointer) :: bundle :: (snln |>  List.filter(fun e -> filterIds |> List.exists(fun x -> x.Id = e.Id) |> not))
                refactorPlains silNew snlNew tail
        refactorPlains sil snl stnl
   


let removeTokenFromOneInSet (chtLst: Token list) (nodes:StateNode list) =
    let tks = Set.ofList chtLst
    nodes
    |>  List.map(fun n ->
        match n with
        |   SinglePath sp -> 
            let (OneInSetMatch ois) = sp.State
            let newLC = ois.ListCheck |> List.filter(fun t -> not(tks.Contains t))
            if newLC.Length > 0 then
                let newQC = qcOneInSet newLC
                SinglePath({ sp with State = (OneInSetMatch({ QuickCheck = newQC; ListCheck = newLC}))})
            else 
                EmptyPath({Id = 0u; NextState = PointerToStateFinal})
        |   _ -> failwith "Not implemented - this should never happen"
    )
    |>  List.filter(fun e -> not(e.IsEmptyPathValue))



let refactorConflictingCharacterSets (sil:SinglePathPointer list, snl:StateNode list) =
    let stMap = snl |> List.map(fun e -> e.Id, e) |> Map.ofList

    let stnl =
        sil
        |>  List.map(fun sp -> stMap.[sp.Id])
        |>  normalizeForRefactoring
        |>  getOneInSetOverlap
        |>  List.filter(fun (t, ls) -> List.length ls > 1)

    if List.length stnl = 0 then
        (sil, snl)
    else
        //  Combine duplicate tokens
        let rec refactorCharacterSets (sil:SinglePathPointer list) (snl:StateNode list) (stnl:(Token*(NormalizedOneInSet list)) list) =
            let stMap = snl |> List.map(fun e -> e.Id, e) |> Map.ofList
            match stnl with
            |   []  -> (sil, snl)
            |   (cht, lst) :: tail ->
                let toRemove, allNextIds =
                    lst
                    |>  List.map(fun e -> e.IdSp.Id, (e.Next.SinglePathPointerValue))
                    |>  List.unzip

                let allRefactoredOneInSet = 
                    let oisids = lst |> List.map(fun e -> e.IdSp)
                    let (nodes:StateNode list) = lst |> List.map(fun i -> stMap.[i.IdSp.Id])
                    removeTokenFromOneInSet [cht] nodes

                let snln =
                    snl 
                    |>  List.filter(fun e -> toRemove |> List.exists(fun x -> x = e.Id) |> not)
                    |>  List.append allRefactoredOneInSet

                let bundle = MultiPath(MultiPath.Create allNextIds)
                let sp = SinglePath (SinglePath.Create (OneInSetMatch({ ListCheck = [cht]; QuickCheck = uint32(cht)})) (bundle.StatePointer))

                let silNew = 
                    sil 
                    |>  List.filter(fun i -> toRemove |> List.contains(i.Id) |> not)
                    |>  List.append (allRefactoredOneInSet |> List.map(fun i -> i.SinglePathPointer))
                let snlNew = sp :: bundle :: snln
                refactorCharacterSets (sp.SinglePathPointer :: silNew) snlNew tail
        refactorCharacterSets sil snl stnl


let refacorConflictingPlainWithCharacterSets (sil:SinglePathPointer list, snl:StateNode list) =
    let stMap = snl |> List.map(fun e -> e.Id, e) |> Map.ofList

    let stnl =
        sil 
        |>  List.map(fun id -> stMap.[id.Id])
        |>  normalizeForRefactoring
        |>  getCrossTypeOverlap
        |>  List.filter(fun (_, l1, l2) -> l1.Length > 0 && l2.Length >0)

    if List.length stnl = 0 then
        (sil, snl)
    else
        let rec refactorPlainsWithCharSets (sil:SinglePathPointer list) (snl:StateNode list) (stnl:(Token*(NormalizedExactChar list)*(NormalizedOneInSet list)) list) =
            let stMap = snl |> List.map(fun e -> e.Id, e) |> Map.ofList
            match stnl with
            |   []  -> (sil, snl)
            |   (cht, elst, clst)  :: tail -> 
                let primary = elst |> List.head |> fun i -> stMap.[i.IdSp.Id]
                let toRemove, allNextIds =
                    clst
                    |>  List.map(fun e -> e.IdSp, e.Next.SinglePathPointerValue)
                    |>  List.append (elst |> List.map(fun e -> e.IdSp, e.Next.SinglePathPointerValue))
                    |>  List.distinct
                    |>  List.unzip

                let allRefactoredOneInSet = 
                    let (nodes:StateNode list) = clst |> List.map(fun e -> stMap.[e.IdSp.Id])
                    removeTokenFromOneInSet [cht] nodes

                let snln =
                    snl 
                    |>  List.filter(fun e -> toRemove |> List.exists(fun x -> x.Id = e.Id) |> not)
                    |>  List.append allRefactoredOneInSet

                let bundle = MultiPath(MultiPath.Create allNextIds)

                let silNew = 
                    sil 
                    |>  List.filter(fun i -> toRemove |> List.exists(fun x -> x.Id = i.Id) |> not)
                    |>  List.append ( primary.SinglePathPointer :: (allRefactoredOneInSet |> List.map(fun i -> i.SinglePathPointer)))
                let snlNew = (primary.SetNextState bundle.StatePointer) :: bundle :: snln
                refactorPlainsWithCharSets silNew snlNew tail
        refactorPlainsWithCharSets sil snl stnl


type RefactorResult =
    |   Refactored of StatePointer
    |   Unrefactored 

let refacorRepeaterStateCollisions (start:StatePointer, nodes:StateNode list, repeaters) =
    //  Repeat is used for option, zero-or-more, once-or-more and repeat-between-min-and-max.
    //  Repeat has two outgoing paths: iteration, and exit.
    //  When iter and exit paths have something in common - collisions, it is difficult to determine
    //  in which path the parser is supposed to be. To solve this, the collissions are joined together in both paths.
    //  The choice whether to follow the iter- or exit-paths is moved to a new location, where this choice is non-ambigiuos.
    //
    //  All repeater involved paths must pass the "RepeatIterOrExit" state, which during parsing, checks the "min" and "max" constraint.
    //  This constraint is essential in a range-repeat. It also removes the repeat-state (iteration count) from the "running loops" list.
    let stMap = nodes |> List.map(fun e -> e.Id, e) |> Map.ofList

    let rec refactorRepeaterRec (iterPtr : StatePointer) (nextPtr : StatePointer) (exitPtr:StatePointer) (repeatId:RepeatId) (stMap:Map<StateId, StateNode>) : RefactorResult * Map<StateId, StateNode> =
        let NextStp (sp:StatePointer) = stMap.[stMap.[sp.Id].NextState].StatePointer
        
        let lst =
            [iterPtr; nextPtr]
            |>  List.map(fun e -> stMap.[e.Id])
            |>  normalizeForRefactoring

        let filterStatePointers (st1:SinglePathPointer list, st3:SinglePathPointer list) = 
            let m = st3 |> List.map(fun i -> i.Id) |> Set.ofList
            st1 |> List.filter(fun i -> not(m.Contains i.Id))

        let createAndSimplifyMultiPath (lst:StatePointer list, stMap:Map<StateId, StateNode>) : StatePointer * Map<StateId, StateNode> =
            if lst |> List.length = 1 then (lst |> List.head).StatePointer, stMap
            else 
                let sorted =
                    lst
                    |>  List.map(fun i -> stMap.[i.Id])
                    |>  SortStateNodes
                    |>  List.map(fun i -> i.SinglePathPointer)
                let mn = MultiPath(MultiPath.Create sorted)
                mn.StatePointer, wsAdd mn stMap

        let createAndSimplifyMultiPathSp (lst:SinglePathPointer list, stMap:Map<StateId, StateNode>) : StatePointer * Map<StateId, StateNode> =
            createAndSimplifyMultiPath (lst |> List.map(fun i -> i.StatePointer), stMap)
        
        let setNextStateForSet (st3:SinglePathPointer list) (target:StatePointer) (stMap:Map<StateId, StateNode>) = 
            st3
            |>  List.fold(fun s i ->
                let sn = stMap.[i.Id]
                wsUpdate (sn.SetNextState target) s
            ) stMap

        let getExistingOneInSetT3 (st3lst:(SinglePathPointer*(StatePointer list)) list) (smp:Map<StateId, StateNode>) = 
            st3lst
            |>  List.tryPick(fun (i, lst) -> 
                match smp.[i.Id] with
                |   SinglePath sp ->
                    match sp.State with
                    |   OneInSetMatch ois -> Some (sp, ois.ListCheck, lst)
                    |   _ -> None
                |   _ -> None
            )
        
        let splitToIOrX (st1:SinglePathPointer list) (st2:SinglePathPointer list) (idlst:StateId list) = 
            let st1s = st1 |> List.map(fun i-> i.Id) |> Set.ofList
            let st2s = st2 |> List.map(fun i-> i.Id) |> Set.ofList
            idlst
            |>  List.fold(fun (s1:Set<StateId>,s2:Set<StateId>) i ->
                match st1s.Contains i, st2s.Contains i with
                |   (true, false) -> (s1.Add i, s2)
                |   (false, true) -> (s1, s2.Add i)
                |   (true,true)   -> (s1.Add i, s2.Add i) // should never happen?
                |   (false,false) -> (s1,s2)             //  filter
            ) (Set.empty,Set.empty)
            

        let optimizeOis (st3andIAndX: (SinglePathPointer * StatePointer list * StatePointer list) list) (stMap:Map<StateId, StateNode>) =
            let oisMap = 
                st3andIAndX
                |>  List.fold(fun s (i, _,_) -> 
                    match stMap.[i.Id] with
                    |   SinglePath sp ->
                        match sp.State with
                        |   OneInSetMatch ois -> Map.add sp.Id ois.ListCheck s
                        |   _ -> s
                    |   _ -> s
                ) Map.empty

            let oisOptLst =
                st3andIAndX
                |>  List.filter(fun (i, _,_) -> oisMap.ContainsKey i.Id)
                |>  List.groupBy(fun (_, st1, st2) -> st1,st2)
                |>  List.filter(fun (k, v) -> v |> List.length > 1) //  only with joined I- and X-paths

            let stn3optimized =
                oisOptLst
                |>  List.map(fun ((s1,s2), lst) ->
                    let tokens =
                        lst
                        |>  List.map(fun (i, _,_) -> oisMap.[i.Id])
                        |>  List.collect id
                    SinglePath(SinglePath.Create (OneInSetMatch { QuickCheck = qcOneInSet tokens; ListCheck = tokens}) PointerToStateFinal), (s1,s2)
                )
            let stMap =
                stn3optimized
                |>  List.fold(fun s (i,_) -> wsAdd i s) stMap

            let remove = stn3optimized |> List.map(fun (i,_) -> i.Id) |> Set.ofList
            let ret =
                st3andIAndX
                |>  List.filter(fun (i,_,_) -> not(remove.Contains i.Id))
            ret @ (stn3optimized |> List.map(fun (i,(s1,s2)) -> i.SinglePathPointer, s1,s2)), stMap


        let refactorCollisionsGeneric (st1:SinglePathPointer list) (st2:SinglePathPointer list) (stMap:Map<StateId, StateNode>) =
            //  These scenario are refactor candidates:
            //  (mp = multipath, sp = singlepath, ns = next step)
            //  
            //  Scenario 1:
            //  Repeat Start -> |I  -> sp -> ns1 -> Next()
            //                  |X  -> mp -> ns2 -> End
            //
            //  Scenario 2:
            //  Repeat Start -> |I  -> mp -> -> ns3 -> Next()
            //                  |X  -> sp -> -> ns4 -> End
            //
            //  Scenario 3:
            //  Repeat Start -> |I  -> mp1 -> ns5 -> Next()
            //                  |X  -> mp2 -> ns6 -> End
            //
            //  Splitting up the commonalities in the I- and X- path, in the RepeatIterOrExit step
            //  results into these generic step-types:
            //
            //  st1: a stripped sp/mp that continues the I-path, stripped from overlapping singlepaths, stripped-empty's removed
            //  st2: a stripped sp/mp that continues the X-path, stripped from overlapping singlepaths, stripped-empty's removed
            //  st3: a new construct with merged sp's, that continues to a next I/X choice
            //
            //  Note: all step types are paths or collections of paths; this detail is invisible in the following.
            //
            //  The generic construct after refactoring becomes this, when not(st1.IsEmpty) and not(st2.IsEmpty):
            //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
            //                  |       |X -> (ns2|ns4|ns6)  :2
            //                  ||I  -> st1 -> (ns1|ns3|ns5) :3
            //                  ||X  -> st2 -> (ns2|ns4|ns6) :4
            //
            //  This construct must be simplifie for the following cases:
            //
            //  If st1.IsEmpty and st2.IsEmpty then :3 and :4 are discarded:
            //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
            //                  |       |X -> (ns2|ns4|ns6)  :2
            //  
            //  If st1.IsEmpty and not(st2.IsEmpty) then :3 is discarded:
            //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
            //                  |       |X -> (ns2|ns4|ns6)  :2
            //                  |st2 -> (ns2|ns4|ns6)        :4
            //
            //  If not(st1.IsEmpty) and st2.IsEmpty then the tree is rearranged as follows
            //  Repeat Start -> |st1 -> |I -> (ns1|ns3|ns5)  :1     The I/X choice is added to check min/max loop constraints 
            //                  |       |X -> NoMatch        :2     on the I-path, while the X-path is invalid.
            //                  |st3 -> |I  -> (ns1|ns3|ns5) :3
            //                  |       |X  -> (ns2|ns4|ns6) :4
            //
            //  And, in those cases st1/st2/st3 are multipaths on the same level, these are combined to one.
            //  
            //  Scenario's where plain-to-plain is merged, or oneinset-to-oneinset is merged, fit nice into the above scenario's,
            //  because they can easily (lineair) be split into donator and receiving states.
            //
            //  Where plain-to-oneinset merges happen, it becomes more complex, because there can be multiple receivers states.
            //
            //  The following scenario's apply for plain-to-oneinset merges:
            //  Scenario 1, merge canditate (A):
            //  Repeat Start -> |I -> A    -> np1 -> Next()
            //                  |X -> [AC] -> np2
            //
            //  This is the st1.IsEmpty & not (st2.IsEmpty) scenario from above, it refactors to:
            //  (st3)  Repeat Start -> |A -> |I -> np1 -> Next()
            //  (st3)                  |     |X -> np2
            //  (st2)                  |[C] -> np2      (match [C] sure exits loop)
            //                   
            //
            //  Scenario 2, , merge canditate (A):
            //  Repeat Start -> |I -> [AC] -> np1 -> Next()
            //                  |X -> A    -> np2
            //
            //  This is the not(st1.IsEmpty) & st2.IsEmpty scenario from above, it refactors to:
            //  (st1) Repeat Start -> |[C]  -> |I -> np1 -> Next()   RepeatIterOrExit is Required to check iteration-constraints (min/max).
            //  (st1)                 |        |X -> NoMatch         
            //  (st3)                 |A    -> |I -> np1 -> Next()
            //                                 |X -> np2
            //
            //  Scenario 3, I/X crosswise overlap , merge canditates (A,B):
            //  Repeat Start -> |I -> |[AC] -> np1 -> Next()
            //                  |     |B    -> np2 
            //                  |X -> |A    -> np3 
            //                  |     |[BD] -> np4
            //
            //  This is the not(st1.IsEmpty) & not(st2.IsEmpty) scenario from above, it refactors to:
            //  (st3)  Repeat Start -> |A    -> |I -> np1 -> Next()
            //  (st3)                  |        |X -> np3
            //  (st3)                  |B    -> |I -> np2
            //  (st3)                  |        |X -> np4
            //  (st1)                  ||I   -> |[C]  -> np1 -> Next()
            //  (st2)                  ||X   -> |[D]  -> np4                

            //
            //  Scenario 4 adds a unaffected path to scenario 3:
            //  Repeat Start -> |I -> |[AC] -> np1 -> Next()
            //                  |     |B    -> np2 
            //                  |X -> |A    -> np3 
            //                  |     |[BD] -> np4
            //                  |     |F    -> np5      unaffected!
            //
            //  This is the not(st1.IsEmpty) & not(st2.IsEmpty) scenario from above, it refactors to:
            //  (st3)  Repeat Start -> |A    -> |I -> np1 -> Next()
            //  (st3)                  |        |X -> np3
            //  (st3)                  |B    -> |I -> np2
            //  (st3)                  |        |X -> np4
            //  (st1)                  ||I   -> |[C] -> np1 -> Next()
            //  (st2)                  ||X   -> |[D] -> np4                
            //  (st2)                  |        |F   -> np5      unaffected!
            //
            //  Scenario 5 adds a unaffected path to scenario 4:
            //  Repeat Start -> |I -> |[AC] -> np1 -> Next()
            //                  |     |B    -> np2 
            //                  |     |G    -> np6      unaffected! 
            //                  |X -> |A    -> np3 
            //                  |     |[BD] -> np4
            //                  |     |F    -> np5      unaffected!
            //
            //  This is the not(st1.IsEmpty) & not(st2.IsEmpty) scenario from above, it refactors to:
            //  (st3)  Repeat Start -> |A    -> |I -> np1 -> Next()
            //  (st3)                  |        |X -> np3
            //  (st3)                  |B    -> |I -> np2
            //  (st3)                  |        |X -> np4
            //  (st1)                  ||I   -> |[C] -> np1 -> Next()
            //  (st1)                  |        |G   -> np6     unaffected!
            //  (st2)                  ||X   -> |[D] -> np4                
            //  (st2)                  |        |F   -> np5     unaffected!
            //  
            //  This shows that st3 has a special property: 
            //      Each element in st3 must know it's successor I- and X-path for refactoring, wich comes from its origin.
            //
            let (st1clean, st2clean, st3andIAndX, stMap) =
                st1 @ st2
                |>  List.map(fun i -> stMap.[i.Id])
                |>  normalizeForRefactoring
                |>  getRefactoringCanditates
                |>  List.fold(fun (st1lst, st2lst, st3lst, smp) rc ->
                    match rc with
                    |   PlainMerge (c, cl) ->
                        let dup = cl.Head
                        let stNew = SinglePath.Create (ExactMatch {Char = dup.ExactChar; ListCheck = [dup.Token]}) PointerToStateFinal |> SinglePath
                        let (ilstorig, xlstorig) = splitToIOrX st1lst st2lst (cl |> List.map(fun i -> i.IdSp.Id))
                        let ilst = cl |> List.filter(fun i -> ilstorig.Contains i.IdSp.Id) |> List.map(fun i -> i.Next)
                        let xlst = cl |> List.filter(fun i -> xlstorig.Contains i.IdSp.Id) |> List.map(fun i -> i.Next)
                        let st3 = (stNew.SinglePathPointer, ilst, xlst) :: st3lst
                        let removeIds = cl |> List.map(fun e -> e.IdSp.SinglePathPointerValue)
                        filterStatePointers (st1lst, removeIds), filterStatePointers (st2lst, removeIds), st3, wsAdd stNew smp

                    |   OneInSetOverlap (t, ls) ->
                        let allOverlappingTokens = [t]

                        let cleanedNodes = removeTokenFromOneInSet allOverlappingTokens (ls |> List.map(fun i -> smp.[i.IdSp.Id]))
                        let smp = cleanedNodes |>  List.fold(fun s i -> wsUpdate i s) smp

                        let removeIds =
                            let orig = ls |> List.map(fun i -> i.IdSp.Id) |> Set.ofList
                            let proj = cleanedNodes |> List.map(fun i -> i.Id) |> Set.ofList
                            Set.difference orig proj
                            |>  Set.toList
                            |>  List.map(fun i -> SinglePathPointer.Create i)

                        let lst = allOverlappingTokens
                        let t3 = SinglePath(SinglePath.Create (OneInSetMatch { QuickCheck = qcOneInSet lst; ListCheck = lst}) PointerToStateFinal) 

                        let (ilstorig, xlstorig) = splitToIOrX st1lst st2lst (ls |> List.map(fun i -> i.IdSp.Id))
                        let ilst = ls |> List.filter(fun i -> ilstorig.Contains i.IdSp.Id) |> List.map(fun i -> i.Next)
                        let xlst = ls |> List.filter(fun i -> xlstorig.Contains i.IdSp.Id) |> List.map(fun i -> i.Next)

                        let st3New = (t3.SinglePathPointer, ilst, xlst):: st3lst
                        filterStatePointers (st1lst, removeIds), filterStatePointers (st2lst, removeIds), st3New, wsAdd t3 smp
                    |   CrossTypeOverlap (t, ecl, oisl) ->
                        let allOverlappingTokens = [t]

                        let cleanedNodes = removeTokenFromOneInSet allOverlappingTokens (oisl |> List.map(fun i -> smp.[i.IdSp.Id]))
                        let smp = cleanedNodes |> List.fold(fun s i -> wsUpdate i s) smp

                        //  Current assumption, there can only be one plain in here.
                        //  if not, then another optimization has failed (ie multipath optimization)
                        //  you can have multiple different plains in a plain-to-oneinset collision here, but not multiple times the same plain
                        let mtcChr = ecl |> List.head 
                        let t3 = SinglePath(SinglePath.Create (ExactMatch { Char = mtcChr.ExactChar ; ListCheck = [mtcChr.Token]}) PointerToStateFinal) 

                        let allNextPtrs = (ecl |> List.map(fun i -> i.IdSp.Id, i.Next)) @ (oisl |> List.map(fun i -> i.IdSp.Id, i.Next))

                        let (ilstorig, xlstorig) = splitToIOrX st1lst st2lst (allNextPtrs  |> List.map(fun (i,_)-> i))
                        let ilst = allNextPtrs |> List.filter(fun (i,_) -> ilstorig.Contains i) |> List.map(fun (_,i) -> i)
                        let xlst = allNextPtrs |> List.filter(fun (i,_) -> xlstorig.Contains i) |> List.map(fun (_,i) -> i)
                        let st3 = (t3.SinglePathPointer, ilst, xlst) :: st3lst
                        let removeIds = ecl |> List.map(fun e -> e.IdSp.SinglePathPointerValue)
                        filterStatePointers (st1lst, removeIds), filterStatePointers (st2lst, removeIds), st3, wsAdd t3 smp
                ) (st1, st2, [], stMap)

            if st3andIAndX.Length = 0 then 
                Unrefactored, stMap
            else
                let st1Next = st1 |> List.map(fun i -> stMap.[i.Id].NextStatePtr.StatePointer) |> List.distinct
                let st2Next = st2 |> List.map(fun i -> stMap.[i.Id].NextStatePtr.StatePointer) |> List.distinct

                let (st3andIAndX, stMap) = optimizeOis st3andIAndX stMap

                let (st3,stMap) =
                    st3andIAndX
                    |>  List.fold(fun (st3New, smp) (i, ilst,xlst) ->
                        let (RIPath, smp) = createAndSimplifyMultiPath (ilst, smp)
                        let (RXPath, smp) = createAndSimplifyMultiPath (xlst, smp)

                        let (repIoE, smp) = 
                            let (potNxt,stMapNew) = refactorRepeaterRec RIPath RXPath exitPtr repeatId smp
                            match potNxt with
                            |   Unrefactored      -> 
                                let p = RepeatIterateOrExit.Create (CreateNewId()) RIPath repeatId RXPath |> RepeatIterOrExit
                                (p.StatePointer), wsAdd p stMapNew
                            |   Refactored single -> single, stMapNew
                        let smp = smp |>  setNextStateForSet [i] repIoE
                        (i::st3New,smp)
                    ) ([], stMap)

                match (st1clean.Length, st2clean.Length) with
                |   (0, 0) ->  
                    //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
                    //                  |       |X -> (ns2|ns4|ns6)  :2
                    let (root, stMap) = createAndSimplifyMultiPathSp(st3, stMap)
                    Refactored(root.StatePointer), stMap
                |   (0, _) ->
                    //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
                    //                  |       |X -> (ns2|ns4|ns6)  :2
                    //                  |st2 -> (ns2|ns4|ns6)        :4
                    let (root, stMap) = createAndSimplifyMultiPathSp(st3 @ st2clean, stMap)

                    Refactored(root.StatePointer), stMap
                |   (_,0)   ->
                    //  Repeat Start -> |st1 -> |I -> (ns1|ns3|ns5)  :1   
                    //                  |       |X -> NoMatch        :2
                    //                  |st3 -> |I  -> (ns1|ns3|ns5) :3
                    //                  |       |X  -> (ns2|ns4|ns6) :4

                    let noMatch = NoMatch (CreateNewId())
                    let (st1NextState, stMap) = createAndSimplifyMultiPath (st1Next, stMap)
                    let repIoNm = RepeatIterateOrExit.Create (CreateNewId()) st1NextState repeatId noMatch.StatePointer |> RepeatIterOrExit

                    let stMap = 
                        stMap
                        |>  wsAdd repIoNm
                        |>  wsAdd noMatch
                        |>  setNextStateForSet st1clean repIoNm.StatePointer

                    let (root, stMap) = createAndSimplifyMultiPathSp (st3 @ st1clean, stMap)
                    Refactored(root.StatePointer), stMap
                |   (_,_)   ->
                    //  Repeat Start -> |st3 -> |I -> (ns1|ns3|ns5)  :1   
                    //                  |       |X -> (ns2|ns4|ns6)  :2
                    //                  ||I  -> st1 -> (ns1|ns3|ns5) :3
                    //                  ||X  -> st2 -> (ns2|ns4|ns6) :4

                    let (st1Cleared, stMap) = createAndSimplifyMultiPathSp(st1clean, stMap)
                    let (st2Cleared, stMap) = createAndSimplifyMultiPathSp(st2clean, stMap)
                    let repOld = RepeatIterateOrExit.Create (CreateNewId()) st1Cleared.StatePointer repeatId st2Cleared.StatePointer |> RepeatIterOrExit

                    let stMap = 
                        stMap
                        |>  wsAdd repOld

                    let (root, stMap) = createAndSimplifyMultiPathSp (repOld.SinglePathPointer::st3, stMap)
                    Refactored(root.StatePointer), stMap
        
        let refactorCollissionsForPathType() =
            let iterNode = stMap.[iterPtr.Id]
            let nextNode = stMap.[nextPtr.Id]

            match (iterNode, nextNode) with
            |   (MultiPath mp1, MultiPath mp2)   -> refactorCollisionsGeneric mp1.States mp2.States stMap
            |   (MultiPath mp, SinglePath sp)    -> refactorCollisionsGeneric mp.States [(sp.SinglePathPointer)] stMap
            |   (SinglePath sp, MultiPath mp)    -> refactorCollisionsGeneric [(sp.SinglePathPointer)] mp.States stMap
            |   (SinglePath sp1, SinglePath sp2) -> refactorCollisionsGeneric [(sp1.SinglePathPointer)] [(sp2.SinglePathPointer)] stMap
            |   _ -> Unrefactored, stMap

        refactorCollissionsForPathType()

    let refactor (ep:EmptyPath) =
        match stMap.[ep.NextState.Id] with
        |   RepeatIterOrExit re ->
            let (refac,stNew) = refactorRepeaterRec (re.IterateState.StatePointer) (re.NextState.StatePointer) (re.StatePointer) re.RepeatId stMap
            match refac with
            |   Refactored single    ->  //  was refactored
                let p = RepeatStart { ep with NextState = single.StatePointer }
                let nodes = stNew |> wsUpdate p |> Map.toList |> List.map(snd)
                (start, nodes, repeaters)
            |   Unrefactored     ->  //  was not refactored
                (start, nodes, repeaters)
        | _ -> (start, nodes, repeaters)

    match stMap.[start.Id] with
    |   RepeatInit d -> 
        let (RepeatStart ep) = stMap.[d.NextState.Id]
        refactor ep
    |   _ -> (start, nodes, repeaters)


let refactorNestedRepeater (start:StatePointer, nodes:StateNode list, repeaters) = true
    //  This function deals with state-collisions in nested Repeater constructs.
    //  It assumes that "refacorRepeaterStateCollisions" has been called for every inner-loop
    //
    //  The problem this function solves is when an inner-loop has a state collission with an outer-loop.
    //  This may happen on the iter-path, and the exit-path.
    //
    //  (RS = Repeat Start, ns = next step, |I = iter path, |X = exit path)
    //
    //  Scenario 1, no collission:
    //  RS(1)   ->  |I -> A -> RS(2) -> ns1
    //              |X -> A -> ns2
    //  The above should be refactored with "refacorRepeaterStateCollisions", removing the double "A"
    //
    //  Scenario 2, collision in the main path:
    //  RS(1)   -> |I1 -> RS(2) -> A -> |I2 -> ns1
    //             |I1                  |X2 -> ns2
    //             |X1 -> A -> ns3
    //  
    //  Should be refactored to:
    //


let removeUnused (nfa:NFAMachine) =
    let stMap = nfa.States |> List.map(fun e -> e.Id, e) |> Map.ofList

    let rec traverse (current:StatePointer) (passedNodes:Set<StateId>) (used:StateId list) =
        if  current.Id = 0u || passedNodes.Contains (current.Id) then current.Id::used
        else
            let node = stMap.[current.Id]
            match node with
            |   SinglePath sp -> traverse (sp.NextState) (passedNodes.Add (sp.Id)) (sp.Id::used)
            |   MultiPath  mp -> 
                let newPassed = passedNodes.Add(mp.Id)
                mp.States 
                |> List.fold(fun u stid -> traverse stid.StatePointer newPassed u) (mp.Id::used)
            |   EmptyPath  ep -> traverse (ep.NextState) (passedNodes.Add (ep.Id)) (ep.Id::used)
            |   RepeatInit rs -> traverse (rs.NextState.StatePointer) (passedNodes.Add (rs.Id)) (rs.Id::used)
            |   RepeatIterOrExit   re  -> 
                let ri =traverse (re.IterateState) (passedNodes.Add (re.Id)) (re.Id::used) 
                traverse (re.NextState) (passedNodes.Add (re.Id)) (re.Id::ri)
            |   RepeatStart ep -> traverse (ep.NextState) (passedNodes.Add (ep.Id)) (ep.Id::used)
            |   NoMatch d -> current.Id::used
    let usedLst =
        traverse nfa.Start Set.empty []
        |>  List.distinct
        |>  Set.ofList
    let nodes = nfa.States |> List.filter(fun n -> usedLst.Contains n.Id)
    {nfa with States = nodes}


let rgxToNFA rgx =
    currentId <- 0u
    currentRepeatId <- 0u
    let rec processConversion rgx : NFAMachine =
        let convert rg : NFAMachine =
            match rg with
            |   Plain pl   -> if pl.``fixed``.Length > 1 then processConversion rg else createSinglePathFromRgx rg |> NFAMachine.Create
            |   OneInSet _ -> createSinglePathFromRgx rg |> NFAMachine.Create
            |   _ -> processConversion rg
            
        let emptyState() = EmptyPath({ Id = CreateNewId(); NextState = PointerToStateFinal})

        let createRepeat o min max =
            let linkState = emptyState()
            let repPath = convert o
            let repState = RepeatState.Create (CreateNewRepeatId()) min max
            let repExit = RepeatIterOrExit <| RepeatIterateOrExit.Create (CreateNewId())  repPath.Start repState.RepeatId linkState.StatePointer
            let repeatLoopStart = RepeatStart <| EmptyPath.Create (CreateNewId()) repExit.StatePointer
            
            let repeatedStates = appendStateIdToAllFinalPathNodes repPath.Start repPath.States repeatLoopStart.StatePointer

            let repStart = RepeatInit <| RepeatInit.Create (CreateNewId()) repState.RepeatId repeatLoopStart.SinglePathPointer
            NFAMachine.Create (repStart.StatePointer, repStart :: repExit :: repeatLoopStart :: linkState :: repeatedStates, repState :: repPath.Repeats)


        match rgx with
        |   Plain  pl ->
            if pl.``fixed``.Length > 1 then
                pl.OptimizeOnce()
                processConversion (Concat (pl.optimized))
            else
                failwith "Uncontained plain - not implemented yet"
        |   Concat l -> 
            let linkState = emptyState()
            let converts =
                l
                |>  List.map(convert)
            let nodes = converts |> List.map(fun c -> c.States) |> List.collect id
            let repeats = converts |> List.map(fun c -> c.Repeats) |> List.collect id

            converts
            |>  List.map(fun c -> c.Start)
            |>  List.fold(fun (concatPtr:StatePointer, nodes:StateNode list, r) (entryStartId:StatePointer) ->
                    let newNodes = appendStateIdToAllFinalPathNodes entryStartId nodes concatPtr
                    (entryStartId, newNodes, repeats)
                    |>  refacorRepeaterStateCollisions
                    ) (linkState.StatePointer, linkState::nodes, repeats)
            |>  NFAMachine.Create
        |   Or     l -> 
            l
            |>  List.map(convert)
            |>  List.fold(fun (sil, snl) nfa ->
                    let mpl = 
                        nfa.States
                        |>  List.find(fun e -> e.Id = nfa.Start.Id)
                        |>  function
                            |   MultiPath  mp -> mp.States
                            |   _             -> []
                    ((nfa.Start.SinglePathPointerValue :: sil) @ mpl), (snl @ nfa.States)
            ) ([],[])
            |>  refactorConflictingCharacterSets
            |>  refacorConflictingPlainWithCharacterSets
            |>  refactorCommonPlains
            |>  fun (idlst, snlst) -> 
                if idlst.Length = 1 then
                    let id = idlst.Head
                    id.StatePointer, snlst, []
                else
                    let mp = MultiPath.Create (idlst)
                    mp.StatePointer, (MultiPath mp) :: snlst, []
            |>  NFAMachine.Create
        |   Optional    r -> createRepeat r 0 1
        |   ZeroOrMore  r -> createRepeat r 0 0
        |   OneOrMore   r -> createRepeat r 1 0
        |   _ -> failwith "Not Implemented Yet"
    processConversion rgx
    |>  removeUnused


type ParseResult = {
    IsMatch     : bool
    FullMatch   : char list
}


type LevelType = 
    |   Concat = 0
    |   Multi   = 1
    |   RepeatIter = 4
    |   RepeatExit = 2
    |   LoopStart = 3
    |   Empty     = 5


let PrintIt (nfa:NFAMachine) =
    let stMap = nfa.States |> List.map(fun e -> e.Id,e) |> Map.ofList
    let rsMap = nfa.Repeats |> List.map(fun e -> e.RepeatId,e) |> Map.ofList
    let passedNodes = Set.empty<StateId>

    let rec printLine (hist : LevelType list) (current: StatePointer) (passedNodes:Set<StateId>) =
        let printPrefix hist =
            hist
            |>  List.rev
            |>  List.iter(fun i ->
                match i with
                |   LevelType.Concat    -> printf "         "
                |   LevelType.Empty     -> printf "     "
                |   LevelType.Multi     -> printf "|    "
                |   LevelType.RepeatExit-> printf " |X    "
                |   LevelType.RepeatIter-> printf " |I    "
                |   LevelType.LoopStart -> printf "               "
            )

        printPrefix hist
        let rec printLineRest (hist : LevelType list) (current: StatePointer) (passedNodes:Set<StateId>) =
            if current.Id = 0u then
                printf "-*\n"
            else
                if not(passedNodes.Contains current.Id) then
                    match stMap.[current.Id] with
                    |   EmptyPath  ep   ->  
                        printf "~(%2d)" ep.Id
                        printLineRest (LevelType.Empty :: hist) ep.NextState (passedNodes.Add ep.Id)
                    |   SinglePath sp   -> 
                        match sp.State with
                        |   ExactMatch c    -> printf "-(%2d:\"%s\")" sp.Id (Regex.Escape(c.Char.ToString()))
                        |   OneInSetMatch o -> printf "-(%2d:[@])" sp.Id
                        printLineRest (LevelType.Concat :: hist) sp.NextState (passedNodes.Add sp.Id)
                    |   MultiPath mp    ->
                        let h::t = mp.States
                        printf "|(%2d)" mp.Id
                        printLineRest (LevelType.Multi :: hist) h.StatePointer (passedNodes.Add mp.Id)
                        t |> List.iter(fun e -> printLine (LevelType.Multi :: hist) e.StatePointer (passedNodes.Add mp.Id))
                    |   RepeatInit rs ->
                        let rt = rsMap.[rs.RepeatId]
                        printf "->>(%2d:<%2d,%2d>)" rs.Id rt.Min rt.Max
                        printLineRest (LevelType.LoopStart :: hist) (rs.NextState.StatePointer) (passedNodes.Add rs.Id)
                    |   RepeatStart rs ->
                        printf "L(%2d)" rs.Id
                        printLineRest (LevelType.Empty :: hist) rs.NextState (passedNodes.Add rs.Id)
                    |   RepeatIterOrExit ri ->
                        printf "-|I(%2d)" ri.Id
                        printLineRest (LevelType.RepeatIter :: hist) ri.IterateState (passedNodes.Add ri.Id)

                        if ri.IterateState.Id <> ri.NextState.Id then
                            printPrefix hist
                            printf "-|X(%2d)" ri.Id
                            printLineRest (LevelType.RepeatExit :: hist) ri.NextState (passedNodes.Add ri.Id)
                        else
                            printPrefix hist
                            printf "|X(%2d) :::^^^\n" ri.NextState.Id
                    |   NoMatch d -> printf "-Err(%2d)\n" d
                else
                    printf "-Loop(%2d)\n" current.Id

        printLineRest hist current passedNodes
    printLine [] nfa.Start passedNodes


let parseIt (nfa:NFAMachine) yaml =
    let stMap = nfa.States |> List.fold(fun (m:Map<_,_>) i -> m.Add(i.Id, i)) Map.empty<StateId, StateNode>
    let stRepeat = nfa.Repeats |> List.fold(fun (m:Map<_,_>) i -> m.Add(i.RepeatId, i)) Map.empty<RepeatId, RepeatState>
    let stream = RollingStream<_>.Create (tokenProcessor yaml) (TokenData.Create (Token.EOF) "")
    let runningLoops = Map.empty<RepeatId, RepeatState>


    let rec processStr currentChar cs acc rollback (runningLoops : Map<RepeatId, RepeatState>) =
        let processCurrentChar = processStr currentChar
        let processNextChar cs acc rollback (runningLoops : Map<RepeatId, RepeatState>)= 
            let chk = (stream.Get())
            processStr chk cs acc rollback runningLoops 

        let NoMatch = { IsMatch = false ; FullMatch = [] }
        if cs = PointerToStateFinal then
            { IsMatch = true; FullMatch = acc |> List.rev }
        else
            let st = stMap.[cs.Id]
            match st with
            |   SinglePath p ->
                let nxt = p.NextState
                if (p.State.Match currentChar) then
                    processNextChar nxt (currentChar.Source.[0] :: acc) rollback runningLoops
                else 
                    NoMatch
            |   MultiPath p ->
                let pos = stream.Position
                let rec parseMultiPath (sptrLst:SinglePathPointer list) =
                    match sptrLst with 
                    |   []      -> NoMatch 
                    |   h::tail ->
                        match stMap.[h.Id] with
                        |   SinglePath st -> 
                            if st.State.Match currentChar then
                                processNextChar (st.NextState) (currentChar.Source.[0] :: acc) rollback runningLoops
                            else
                                NoMatch
                        |   EmptyPath  st  -> processCurrentChar (st.NextState) acc (rollback+1) runningLoops
                        |   RepeatStart st  -> processCurrentChar (st.NextState) acc (rollback+1) runningLoops
                        |   RepeatIterOrExit re  -> processCurrentChar re.StatePointer acc rollback runningLoops
                        |   _ -> failwith "Not implemented yet"
                        |>  fun res -> 
                            if not(res.IsMatch) && stream.Position = pos  then
                                parseMultiPath tail
                            else res

                parseMultiPath p.States
            |   EmptyPath p -> processCurrentChar p.NextState acc rollback runningLoops
            |   RepeatStart p -> processCurrentChar p.NextState acc rollback runningLoops
            |   RepeatInit r ->
                let rlnew =
                    if runningLoops.ContainsKey r.RepeatId then
                        runningLoops.Remove(r.RepeatId).Add(r.RepeatId, stRepeat.[r.RepeatId])
                    else
                        runningLoops.Add(r.RepeatId, stRepeat.[r.RepeatId])
                processCurrentChar r.NextState.StatePointer acc rollback rlnew
            |   RepeatIterOrExit r ->
                let rs = runningLoops.[r.RepeatId]
                if rs.MustExit() then
                    processCurrentChar r.NextState acc rollback runningLoops
                else
                    let pos = stream.Position
                    let rlNew = runningLoops.Remove(r.RepeatId).Add(r.RepeatId, rs.Iterate())
                    let tryIterate = processCurrentChar r.IterateState acc rollback rlNew
                    if tryIterate.IsMatch then
                        tryIterate
                    else
                        if stream.Position = pos && rs.CanExit() && r.IterateState<>r.NextState then
                            processCurrentChar r.NextState acc rollback (rlNew.Remove rs.RepeatId) // end loop by removing it from running loops
                        else
                            NoMatch
            |   NoMatch _ -> NoMatch

    processStr (stream.Get()) nfa.Start [] 0 runningLoops

let clts (cl:char list) = System.String.Concat(cl)

module ParseResult =
    let IsMatch pr = pr.IsMatch
    let FullMatch pr = pr.FullMatch


