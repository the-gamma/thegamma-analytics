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
  ( "http://coeffectlogs.azurewebsites.net/log", httpMethod="POST", 
    body=HttpRequestBody.TextRequest "testing..." )

// Read the logs from Azure storage
let account = CloudStorageAccount.Parse(Connection.CoeffectLogStorage)
let client = account.CreateCloudBlobClient()
let logs = client.GetContainerReference("logs")

// List all log files
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 

// List all log files & read their contents
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn "\n\n%s\n%s" (Seq.last log.Uri.Segments) (Seq.last log.Uri.Segments |> String.map (fun _ -> '-'))
  logs.GetAppendBlobReference(log.Uri.Segments |> Seq.last).DownloadText() |> printfn "%s"

// Download all files to a local folder with logs
let root = __SOURCE_DIRECTORY__ + "/logs"
if not (IO.Directory.Exists root) then IO.Directory.CreateDirectory root |> ignore
for log in logs.GetDirectoryReference("").ListBlobs() do
  printfn " - %s" (Seq.last log.Uri.Segments) 
  let file = Seq.last log.Uri.Segments
  let localFile = root + "/" + file
  if not (IO.File.Exists localFile) then
    IO.File.WriteAllText(localFile, logs.GetAppendBlobReference(file).DownloadText())


// ------------------------------------------------------------------------------------------------
// Logs analaysis 
// ------------------------------------------------------------------------------------------------

open FSharp.Charting

let [<Literal>] sampleLogs = __SOURCE_DIRECTORY__ + "/logs.json"
type Logs = JsonProvider<sampleLogs>

let json =
  [ for f in IO.Directory.GetFiles(root) do
      for l in IO.File.ReadAllLines(f) do 
        if l <> "testing..." then yield l ] |> String.concat ","

let all = Logs.Parse("[" + json + "]")

// Summary of events
all 
|> Seq.groupBy (fun l -> l.Session)
|> Seq.mapi (fun i (_, events) -> i, events)
|> Seq.filter (fun (_, e) -> Seq.length e > 5)
|> Seq.iter (fun (i, es) ->
  printfn "\n---------- %d -----------" i
  for e in es do 
    printfn "%s %s" e.Category e.Event)

// What are the most common things people do?
all 
|> Seq.countBy (fun l -> l.Category + " " + l.Event)
|> Seq.sortBy (fun (_, n) -> -n)
|> Seq.iter (fun (e, n) -> printfn "%s %d" (e.PadRight 30) n)

// How long people stay
all 
|> Seq.groupBy (fun l -> l.Session)
|> Seq.map (fun (_, e) -> 
    let lo = e |> Seq.map (fun v -> v.Time) |> Seq.min
    let hi = e |> Seq.map (fun v -> v.Time) |> Seq.max
    (hi - lo).TotalMinutes )
|> Seq.filter (fun m -> m < 10.0)
|> Chart.Histogram

// List all entered source code
all
|> Seq.choose (fun e -> e.Data.Record)
|> Seq.choose (fun r -> r.Source)
|> Seq.distinct
|> Seq.iter (printfn "----------------------------\n%s\n")


// Errors that we reported to users
all 
|> Seq.scan (fun (st, _) el -> 
    if el.Data.Record.IsSome && el.Data.Record.Value.Source.IsSome then
      (Some el.Data.Record.Value.Source.Value), el
    else st, el) (None, Seq.head all)
|> Seq.filter (fun (st, e) -> e.Category = "error")

