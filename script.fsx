#load "config.fsx"
#load "packages/FsLab/FsLab.fsx"
#load "packages/FSharp.Azure.StorageTypeProvider/StorageTypeProvider.fsx"
open System
open FSharp.Data
open Microsoft.WindowsAzure.Storage

// ------------------------------------------------------------------------------------------------
// Azure connection
// ------------------------------------------------------------------------------------------------

// Sending sample request to the logging service
Http.RequestString
  ( "http://thegamma-logs.azurewebsites.net/log/test/", httpMethod="POST", 
    body=HttpRequestBody.TextRequest "testing..." )

// Read the logs from Azure storage
let getContainer key = 
  let account = CloudStorageAccount.Parse(Connection.TheGammaLogStorage)
  let client = account.CreateCloudBlobClient()
  client.GetContainerReference(key)

// List all log files
let logs = getContainer "turing"
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 

// List all log files & read their contents
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn "\n\n%s\n%s" (Seq.last log.Uri.Segments) (Seq.last log.Uri.Segments |> String.map (fun _ -> '-'))
  logs.GetAppendBlobReference(log.Uri.Segments |> Seq.last).DownloadText() |> printfn "%s"

// Download all files to a local folder with logs
let downloadAll key = 
  let root = __SOURCE_DIRECTORY__ + "/logs/" + key
  if not (IO.Directory.Exists root) then IO.Directory.CreateDirectory root |> ignore
  let logs = getContainer key
  for log in logs.GetDirectoryReference("").ListBlobs() do
    printfn " - %s" (Seq.last log.Uri.Segments) 
    let file = Seq.last log.Uri.Segments
    let localFile = root + "/" + file
    if not (IO.File.Exists localFile) then
      printfn "   ...downloading..."
      logs.GetBlobReference(file).DownloadToFile(localFile, IO.FileMode.Create)

downloadAll "olympics"
downloadAll "turing"
downloadAll "datavizstudy"
downloadAll "histogram"

// ------------------------------------------------------------------------------------------------
// Logs analaysis for Olympics
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts

let [<Literal>] olympicsSample = __SOURCE_DIRECTORY__ + "/samples/olympics.json"
let olympicsRoot = __SOURCE_DIRECTORY__ + "/logs/olympics"
type Olympics = JsonProvider<olympicsSample>

let olympicsJson =
  [ for f in IO.Directory.GetFiles(olympicsRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let olympics = Olympics.Parse("[" + olympicsJson + "]")

olympics
|> Seq.filter (fun a -> a.Event.IsNone)
|> Seq.iter (fun e -> printfn "%O" e.Time.Date)

olympics
|> Seq.countBy (fun a -> defaultArg a.Event "?")
|> Chart.Column

olympics
|> Seq.countBy (fun d -> d.Time.Day)
|> Chart.Column

olympics
|> Seq.countBy (fun d -> d.Category + ", " + (defaultArg d.Event ""))
|> Seq.filter (fun (e, c) -> c > 200)
|> Chart.Column

let topUsers =
  olympics
  |> Seq.countBy (fun d -> d.User.ToString())
  |> Seq.filter (fun (u, c) -> c > 50)
  |> Seq.map fst |> set

olympics 
|> Seq.filter (fun e -> e.Event = Some "loaded" && topUsers.Contains (e.User.ToString()))
|> Seq.map (fun e -> e.Data.String, e.User)
|> Seq.distinct
|> Seq.iter (printfn "%O")

// ------------------------------------------------------------------------------------------------
// Logs analaysis for Turing
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts
open System.Windows.Forms

let play strings = Async.StartImmediate <| async {
  use f = new Form(TopMost=true, Visible=true, Width=1200, Height=800)
  use t = new Label(Dock=DockStyle.Fill, Font = new Drawing.Font("consolas", 16.f))
  f.Controls.Add(t)
  do! Async.Sleep(2000)
  let mutable i = 0
  let len = Seq.length strings
  for s in strings do
    t.Text <- s
    f.Text <- sprintf "%d/%d" i len
    i <- i + 1
    do! Async.Sleep(250) }
    
let [<Literal>] turingSample = __SOURCE_DIRECTORY__ + "/samples/turing.json"
let turingRoot = __SOURCE_DIRECTORY__ + "/logs/turing"
type Turing = JsonProvider<turingSample>

let turingJson =
  [ for f in IO.Directory.GetFiles(turingRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let turing = 
  Turing.Parse("[" + turingJson + "]")
  |> Array.filter (fun e -> e.Url.Contains("gamma.turing.ac.uk"))

turing
|> Seq.countBy (fun e -> e.Time.Month, e.Time.Day)
|> Seq.map (fun ((m,d), n) -> sprintf "%d/%d" d m, n)
|> Chart.Bar

turing
|> Seq.filter (fun e -> e.Event="completions")
|> Seq.countBy (fun e -> e.User)
|> Seq.sortBy snd
|> Seq.iter (printfn "%A")

turing
|> Seq.filter (fun e -> e.Event="completions")
|> Seq.map (fun e -> e.Data.Record.Value.Source.Value)
|> play

turing
|> Seq.countBy (fun e -> e.Category, e.Event)
|> Seq.sortByDescending snd
|> Seq.iter (fun ((c,e),n) -> printfn "(%d) %s - %s" n c e)

turing
|> Seq.filter (fun e -> e.Category = "source" && e.Event = "update")
|> Seq.map (fun e -> e.Data.String.Value)
|> Seq.countBy (fun s -> s.Contains "let data", s.Contains "topThreeSpendings", s.Contains "Top4SpendsForSocialProtection")

turing
|> Seq.filter (fun e -> e.Category = "source" && e.Event = "update")
|> Seq.map (fun e -> e.Data.String.Value)
|> Seq.filter (fun s -> not (s.Contains "let data"))

turing
|> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
|> Seq.countBy (fun e -> e.Element)

turing
|> Seq.filter (fun e -> e.Event = "exception")

let realign actual guesses = 
  let guesses = dict guesses
  let mutable last = None
  [| for k, v in actual ->
        if guesses.ContainsKey k then last <- Some guesses.[k]; k, guesses.[k]
        elif last.IsSome then k, last.Value
        else k, v |]

let lightColors = seq {
  while true do
    for c1 in ["ff"; "dd"; "bb"] do
      for c2 in ["dd"; "ff"; "bb"] do
        for c3 in ["99"; "bb"; "dd"] do
          yield "#" + c1 + c2 + c3 }

let shuffle s = 
  let rnd = System.Random()
  s |> Seq.map (fun v -> rnd.Next(), v) |> Seq.sortBy fst |> Seq.map snd

let lineGuesses id = 
  let all = 
    turing
    |> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
    |> Seq.map (fun e -> e.Element.Value, e.Data.Record) 
    |> Seq.filter (fun (e, v) -> e = id)
  let actual = 
    all |> Seq.map (fun (_, v) -> v.Value.Values |> Array.map (fun g -> int g.Numbers.[0], float g.Numbers.[1])) |> Seq.head
  let guesses = 
    all |> Seq.map (fun (_, v) -> v.Value.Guess |> Array.map (fun g -> int g.Numbers.[0], float g.Numbers.[1]) |> realign actual) 
  let colors = lightColors |> Seq.take (Seq.length guesses)
  Seq.append guesses [actual] 
  |> Chart.Line
  |> Chart.WithOptions(Options(colors=Array.append (Array.ofSeq colors) [| "black" |] ))

let barGuesses id = 
  let all = 
    turing
    |> Seq.filter (fun e -> e.Category = "interactive" && e.Event = "completed")
    |> Seq.map (fun e -> e.Element.Value, e.Data.Record)
    |> Seq.filter (fun (e, v) -> e = id)
  let actual = 
    all |> Seq.map (fun (_, v) -> v.Value.Values |> Array.map (fun g -> g.JsonValue.AsArray().[0].AsString(), g.JsonValue.AsArray().[1].AsFloat())) |> Seq.head 
  let guesses = 
    all |> Seq.map (fun (_, v) -> v.Value.Guess |> Array.map (fun g -> g.JsonValue.AsArray().[0].AsString(), g.JsonValue.AsArray().[1].AsFloat()) |> dict) 
  let colors = Seq.append ["black"] (lightColors |> Seq.take (Seq.length guesses) |> shuffle)
  Chart.Bar
    [ yield actual
      for g in guesses -> [| for k, _ in actual -> k, g.[k] |] ]
    |> Chart.WithOptions(Options(colors=Array.ofSeq colors ))


lineGuesses "thegamma-scienceAndTech-out"
lineGuesses "thegamma-greenGovernment-out"
barGuesses "thegamma-grossDomesticProduct-out"


// ------------------------------------------------------------------------------------------------
// Logs analaysis for Dataviz study
// ------------------------------------------------------------------------------------------------

open XPlot.GoogleCharts

let [<Literal>] datavizSample = __SOURCE_DIRECTORY__ + "/samples/dataviz.json"
let datavizRoot = __SOURCE_DIRECTORY__ + "/logs/datavizstudy"
type Dataviz = JsonProvider<datavizSample>

let datavizJson =
  [ for f in IO.Directory.GetFiles(datavizRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let dataviz = 
  Dataviz.Parse("[" + datavizJson + "]")
  |> Array.filter (fun e -> e.Url.Contains("dataviz-study.azurewebsites.net"))

// Unique Prolific IDs
dataviz
|> Seq.choose (fun e -> e.Data.Record)
|> Seq.choose (fun e -> e.Prolificid)
|> Seq.distinct
|> Seq.iter (printfn "%s")

let read file = System.IO.File.ReadAllLines(__SOURCE_DIRECTORY__ + "/data/" + file)
let pids = read "pids.txt"
let university = read "uni.txt" |> set

type Answers =      
  { share1: int
    share2: int
    q1: string
    q2: string
    q3: string
    q4: string }

type Info = 
  { university: bool
    mode: string 
    time: float 
    backbutton: bool
    completed: list<Map<string,(float * float)>>
    answers: Answers }

// let pid = "558aab19fdf99b65685f0142"
let printEventsFor pid = 
  let info = 
    dataviz |> Array.find (fun e -> 
      e.Category = "user" && e.Event = "info" && 
        e.Data.Record.IsSome && e.Data.Record.Value.Prolificid = Some pid)
  let user = info.User
  let events = 
    dataviz |> Array.filter (fun e -> e.User = user)
  for e in events do 
    printfn "%s %s %s" e.Category e.Event (defaultArg e.Element "")

let info = 
  [ for pid in pids do
    printfn "\n%s" pid
    if pid <> "5b2465d4007d870001c7926a" && pid <> "5b7341a7543d1c0001c80a26" &&
       pid <> "581fd4d3a099610001b702a2" && pid <> "5ac54531e1546900019c0487" &&
       pid <> "5b27416a7297750001c7183d" && pid <> "5b245c2e007d870001c7915f" &&
       pid <> "558aab19fdf99b65685f0142"
        then
      let info = 
        dataviz |> Array.find (fun e -> 
          e.Category = "user" && e.Event = "info" && 
            e.Data.Record.IsSome && e.Data.Record.Value.Prolificid = Some pid)
      let user = info.User

      let events = 
        dataviz |> Array.filter (fun e -> e.User = user)
      let times = 
        events |> Array.map (fun e -> e.Time)
      let time = (Array.max times) - (Array.min times)

      let res = 
        events 
        |> Seq.filter (fun e -> e.Event = "loaded") 
        |> Seq.choose (fun e -> e.Element) |> String.concat " "
      let back = (res.Substring(res.IndexOf("step5")+5)).Contains("step3") 

      let load3 = events |> Seq.pick (fun e -> if e.Element = Some "step3" && e.Event = "loaded" then Some e.Time else None) 
      let load5 = events |> Seq.pick (fun e -> if e.Element = Some "step5" && e.Event = "loaded" then Some e.Time else None) 
      //yield (load5 - load3).TotalMinutes 

      let mode = 
        if not (events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r -> r.Visual)) then "image"
        elif not (events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r -> r.Interactive)) then "chart"
        else "interactive"

      let asMap (guesses:Dataviz.DecimalOrString[]) (values:Dataviz.DecimalOrString[]) = 
        let guesses = guesses |> Array.sortBy (fun g -> g.JsonValue.AsArray().[0].AsString())
        let values = values |> Array.sortBy (fun g -> g.JsonValue.AsArray().[0].AsString())
        let map = (guesses, values) ||> Array.map2 (fun g v ->
          if g.JsonValue.AsArray().[0].AsString() <> v.JsonValue.AsArray().[0].AsString() then failwith "oops"
          g.JsonValue.AsArray().[0].AsString(),
          ( v.JsonValue.AsArray().[1].AsFloat(),
            g.JsonValue.AsArray().[1].AsFloat() ) ) |> Map.ofSeq
        if map.ContainsKey "Miscellaneous" then Map.ofSeq [ for k,v in Map.toSeq map -> k + " (China exports)", v ]
        elif map.ContainsKey "Vegetable Products" then Map.ofSeq [ for k,v in Map.toSeq map -> k + " (US exports)", v ]
        else map

      let completed = 
        events |> Seq.choose (fun e -> if e.Event <> "completed" then None else e.Data.Record) 
          |> Seq.map (fun r -> asMap r.Guess r.Values)

      let answers = 
        events |> Seq.choose (fun e -> e.Data.Record) |> Seq.pick (fun r ->
          match defaultArg r.Question1 "", r.Question2, r.Question3, r.Question4, r.Question5, r.Share1, r.Share2 with
          | q1, Some q2a, Some q2b, Some q3, Some q4, Some s1, Some s2 ->
              Some { share1=s1; share2=s2; q1=q1; q2=q2a+" "+q2b; q3=q3; q4=q4 }
          | _ -> None)

      printfn "  events: %d" (events |> Seq.length)
      printfn "  time: %.2f" (time.TotalMinutes)
      printfn "  mode: %s" mode 
      yield { backbutton=back; university=university.Contains pid; mode=mode; time=time.TotalMinutes; answers=answers; completed = List.ofSeq completed } ]

info |> Seq.filter (fun e -> e.backbutton) |> Seq.map (fun e -> e.answers.q1) |> List.ofSeq

let mid = info |> Seq.map (fun u -> u.time) |> Seq.sort |> Seq.item 46
let finfo = info

finfo |> Seq.length
finfo |> Seq.countBy (fun u -> u.mode)
//let finfo = info |> Seq.filter (fun u -> u.time < mid)
//let finfo = info |> Seq.filter (fun u -> u.university)

// TIME: How long it took people to complete?
 
let parseNumber (n:string) =
  if n = "" then nan else
  let i = n |> String.filter Char.IsNumber |> int64
  if i < 10000L then float i else float (i / 1000000000000L)

let modes = ["interactive";"image";"chart"]
let data1 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed finfo do
        if v.mode = mode then yield i, v.time ] ]
Chart.Scatter(data1) |> Chart.WithLabels modes

data1 |> Seq.map (Seq.averageBy snd) |> Seq.zip modes

// Q1: Average guess of exports from US to China

let data2 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed finfo do
        let f = parseNumber v.answers.q1
//        if f < 700.0 then
//        if f <> 122.0 then
//        if f < 120.0 || f > 123.0 then
        if not (Double.IsNaN f) then
          if v.mode = mode then yield i, f ] ]

[ for v in finfo -> v.mode, 122.0 = parseNumber v.answers.q1 ]
|> Seq.countBy id
|> Seq.toList
 
Chart.Scatter(data2) |> Chart.WithLabels modes

data2 |> Seq.map (Seq.averageBy snd) |> Seq.zip modes

data2 |> Seq.map (fun data ->
  let exact = data |> Seq.filter (fun v -> snd v = 122.0) |> Seq.length
  float exact / float (Seq.length data) ) |> Seq.zip modes

// Q2: How many people got this right?

finfo |> Seq.map (fun v -> v.answers.q2) |> Seq.distinct |> Seq.iter (printfn "%s")

let data3 = 
  [ for mode in modes -> 
    [ for i, v in Seq.indexed finfo do
        if v.mode = mode then yield i, v.answers.q2= "machines transportation" || v.answers.q2 = "transportation machines" ] ]

data3 |> Seq.map (fun d -> 
  let right = Seq.filter snd d |> Seq.length
  float right / float (Seq.length d), right, Seq.length d ) |> Seq.zip modes

// SHARE: How likely are people to share?

let data4 = 
  [ [ for i, v in Seq.indexed finfo do
      yield i, v.answers.share2 ] ] @
  [ for mode in modes ->
    [ for i, v in Seq.indexed finfo do
        if v.mode = mode then yield i, v.answers.share1 ] ] 
let labels = "Share 2" :: (List.map (sprintf "Share 1 (%s)") modes)
Chart.Scatter(data4) 
|> Chart.WithOptions(Options(colors=[|"#e0e0e0"; "blue";"red";"orange"|]))
|> Chart.WithLabels labels

data4 |> Seq.map (Seq.averageBy (snd >> float)) |> Seq.zip ("share2"::modes)

// GUESS: How good guesses people make?
let getKeys a b c = 
  [ 
    if a then yield "Total exports to USA from China"
    if a then yield "Total exports to China  from USA"
    if a then yield "Tariffs on exports to USA from China"
    if a then yield "Tariffs on exports to China from USA"
    if b then yield "Machines (China exports)"
    if b then yield "Metals (China exports)"
    if b then yield "Miscellaneous (China exports)"
    if b then yield "Plastics and Rubbers (China exports)"
    if b then yield "Textiles (China exports)"
    if c then yield "Chemical Products (US exports)"
    if c then yield "Instruments (US exports)"
    if c then yield "Machines (US exports)"
    if c then yield "Transportation (US exports)"
    if c then yield "Vegetable Products (US exports)"
  ]

let keys = getKeys true false false 

let append m1 m2 = Map.toList m1 |> List.fold (fun m (k, v) -> Map.add k v m) m2
let maps = 
  finfo |> Seq.choose (fun v -> 
    if List.isEmpty v.completed then None else
      Some(v.university, List.reduce append v.completed) )
let _, ans = maps |> Seq.head

let rnd = System.Random()
let clr () = "#" + String.concat "" [ for i in 1 .. 6 -> string "89abcdef".[rnd.Next("89abcdef".Length)] ]
let colors = "black"::[for i in 0 .. 100 -> clr()]

[ yield [ for k in keys -> k, fst ans.[k] ] 
  for _, ans in maps -> [ for k in keys -> k, snd ans.[k] ] ] 
|> Chart.Scatter
|> Chart.WithOptions(Options(colors=Array.ofSeq colors))
|> Chart.WithLabels ("Value"::[for i, (u, _) in Seq.indexed maps -> sprintf "Guess %d (%s)" i (if u then "U" else "X") ])

// Q1: Does university matter
let uni = [true;false]
let data5 = 
  [ for u in uni -> 
    [ for i, v in Seq.indexed finfo do
        let f = parseNumber v.answers.q1
        if not (Double.IsNaN f) then
          if v.university = u then yield i, f ] ]

Chart.Scatter(data5) |> Chart.WithLabels (List.map string uni)

// Q2: Does university matter
let data6 = 
  finfo
  |> Seq.countBy (fun v -> v.mode, v.university, v.answers.q2= "machines transportation" || v.answers.q2 = "transportation machines" )
  |> Seq.map (fun ((m, u, c), n) -> (if u then "Uni" else "None") + (if c then "Correct" else "Wrong"), (m, n))
  |> Seq.groupBy fst 

data6 
|> Seq.map (fun (_, v) -> Seq.map snd v)
|> Chart.Column
|> Chart.WithOptions(Options(vAxis=Axis(minValue=0)))
|> Chart.WithLabels (Seq.map fst data6)


// Export likes
type Csv1 = CsvProvider<"Group (string), Value (int), Education (string)">

let exp1 = 
  [ for i, v in Seq.indexed finfo ->
    Csv1.Row(v.mode, v.answers.share1, if v.university then "university" else "none") ] //@

System.IO.File.Delete("C:/temp/exp.csv")
Csv1.GetSample().Append(exp1).Save("C:/temp/exp.csv")

let exp1b = 
  [ for v in finfo ->
    Csv1.Row("tariffs", v.answers.share1, if v.university then "university" else "none") ] @
  [ for v in finfo ->
    Csv1.Row("movies", v.answers.share2, if v.university then "university" else "none") ] 

System.IO.File.Delete("C:/temp/exp1b.csv")
Csv1.GetSample().Append(exp1b).Save("C:/temp/exp1b.csv")

// Export guesses
type Csv2 = CsvProvider<"Group (string), Guess (string), Education (string)">

let exp2 =
  [ for v in finfo ->
    let s =
      (if v.answers.q2.Contains("machines") then "Mach" else "") +
      (if v.answers.q2.Contains("transportation") then "Trans" else "") 
    let s = if s = "" then "Nothing" else s
    Csv2.Row(v.mode, s, if v.university then "University" else "None") ] 

System.IO.File.Delete("C:/temp/exp2.csv")
Csv2.GetSample().Append(exp2).Save("C:/temp/exp2.csv")

// Export likes of Q2
type Csv3 = CsvProvider<"Group (string), Value (int)">

let exp3 = 
  [ for i, v in Seq.indexed finfo ->
    Csv3.Row("static", v.answers.share2) ]

Csv3.GetSample().Append(exp3).Save("C:/temp/exp3.csv")

// 
let exp4 = 
  [ for v in finfo do
      let f = parseNumber v.answers.q1
      if not (Double.IsNaN f) then 
        yield Csv1.Row(v.mode, int f, if v.university then "University" else "None") ] 

System.IO.File.Delete("C:/temp/exp4.csv")
Csv1.GetSample().Append(exp4).Save("C:/temp/exp4.csv")


// ------------------------------------------------------------------------------------------------
// Logs analaysis for Histogram
// ------------------------------------------------------------------------------------------------


open XPlot.GoogleCharts

let [<Literal>] histogramSample = __SOURCE_DIRECTORY__ + "/samples/histogram.json"
let histogramRoot = __SOURCE_DIRECTORY__ + "/logs/histogram"
type Histogram = JsonProvider<histogramSample>

let histogramJson =
  [ for f in IO.Directory.GetFiles(histogramRoot) do
      for l in IO.File.ReadAllLines(f) do 
        if not (String.IsNullOrWhiteSpace l) && l <> "testing..." then yield l ] |> String.concat ","

let histogram = Histogram.Parse("[" + histogramJson + "]")

histogram 
|> Seq.countBy (fun e -> e.Event)

histogram 
|> Seq.filter (fun e -> e.Event = "event")
|> Seq.map (fun e -> e.Data.Record.Value.Kind)
|> Seq.iter (printfn "%A")
