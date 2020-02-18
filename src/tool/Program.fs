open System
open System.IO
open lensfunNet
open OpenCvSharp
open System.Text.RegularExpressions
open System.Collections.Concurrent
open System.Threading



module Tool = 

    type Processed = 
        {
            inputFileName : string
            info          : LensFun.ImageInfo
            undistorted   : Mat
            targetFile    : string
            downsampled   : Option<Mat>
        }

    let undistort (remap : float32[])  (img : Mat)  = 
        let size = img.Size()
        let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

        let outimg = new Mat(size, img.Type())
        let map = InputArray.Create(remapMat)
        Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
        outimg

    let undistortJpg (db : IntPtr) (filename : string) (undistored : string) =
        let cameraParameters = Exif.fileToParams filename
        let img = Cv2.ImRead filename
        let width,height = img.Size(1), img.Size(0)
        let lensInfo = LensFun.createModifier db cameraParameters width height 
        let outimg = undistort lensInfo.remap.Value img

        Cv2.ImWrite(undistored, outimg) |> printfn "ok: %A"


    let run (argv : array<string>) =
        let app = 
            Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location)
        let dbD = Path.Combine(app,"./db_files")
        LensFun.downloadLensFunFromWheels app dbD
        let db = LensFun.initLf dbD

        let mappers = ConcurrentDictionary<_,LensFun.ImageInfo>()

        let q = new BlockingCollection<_>(20)
        let w = new BlockingCollection<_>(20)
        let distorter () = 
            for (sourceFile, img, info : LensFun.ImageInfo, targetFile) in q.GetConsumingEnumerable() do 
                try 
                    let undistorted = undistort info.remap.Value img
                    let thumbSize = Size(img.Size().Width / 4, img.Size().Height / 4)
                    let thumb = new Mat(thumbSize, img.Type())
                    let downsampled = Cv2.Resize(InputArray.Create undistorted, OutputArray.Create thumb, thumbSize)
                    w.Add { inputFileName = sourceFile; info = info; undistorted = undistorted; targetFile = targetFile; downsampled = Some thumb }
                with e -> 
                    printfn "failed to convert: %s" sourceFile
            

        let writer () =
            for d in w.GetConsumingEnumerable() do 
                try
                    if Cv2.ImWrite(d.targetFile,d.undistorted) then
                        let json = MetaData.toJson { imageInfo = d.info }
                        let info = Path.ChangeExtension(d.targetFile,"json")
                        File.WriteAllText(info,json)
                        let a = MetaData.fromJson json
                        printfn "%s -> %s" d.inputFileName d.targetFile
                        match d.downsampled with
                            | Some s -> Cv2.ImWrite(Path.ChangeExtension(d.targetFile,"thumb.JPG"), s) |> ignore
                            |_ -> ()
                    else printfn "failed to ImWrite: %s" d.inputFileName
                with e -> 
                     printfn "failed to write: %s" d.inputFileName

        let reader (files : seq<string*string>) =
            for (s,d) in files do   
                try
                    let img = Cv2.ImRead s
                    let p = Exif.fileToParams s
                    let key = (img.Size(),p)
                    let mapper  = 
                        mappers.GetOrAdd(key, fun v -> 
                            let width,height = img.Size(1), img.Size(0)
                            let lensInfo = LensFun.createModifier db p width height 
                            lensInfo
                        )
                    q.Add((s, img, mapper, d))
                with e -> 
                    printfn "%s failed with: %A" s e
            q.CompleteAdding()
                           

                
        match argv with
            | [|dir;pat;tPat|] -> 
                let start f =
                    async {
                        do! Async.SwitchToNewThread()
                        return f()
                    } 


                let writer = Async.StartAsTask <| start writer
                let runners = 
                    async {
                        let! dists = Async.Parallel [for i in 0 .. 8 do yield distorter |> start] 
                        w.CompleteAdding()
                    } |> Async.StartAsTask

                let r = 
                    if pat.Contains("%d") then
                        pat.Replace("%d","(?<id>[0-9]*)") + "$"
                    else pat
                let regex = Regex r
                let files = 
                    seq { 
                        for f in Directory.EnumerateFiles(dir,"*", SearchOption.AllDirectories) do
                            let file = Path.GetFileName f
                            let m = regex.Match(file)
                            if m.Success then
                                let n = 
                                    if m.Groups.ContainsKey "id" && tPat.Contains("%d") then 
                                        tPat.Replace("%d",m.Groups.["id"].Value)
                                    else tPat
                                let dir = Path.GetDirectoryName f
                                yield f, Path.Combine(dir,n)
                    } |> Seq.toList

                reader files 
                runners.Wait()
                writer.Wait()
                0
            | _ -> 
                printfn "usage: tool path searchPattern\n e.g. tool . \"IMG_%%d.JPG\" \"undist_%%d.jpg0\""
                1


 

[<EntryPoint>]
let main argv =
    Tool.run argv

