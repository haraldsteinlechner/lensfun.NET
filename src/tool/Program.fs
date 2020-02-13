open System
open System.IO
open lensfunNet
open OpenCvSharp
open System.Text.RegularExpressions
open System.Collections.Concurrent
open System.Threading

open Chiron

type Params with
    static member ToJson (x : Params) = 
        json {
            do! Json.write "Make" x.cam_maker
            do! Json.write "Model" x.cam_model
            do! Json.write "FocalLength" x.focal_len
            do! Json.write "ApertureValue" x.aperture
            do! Json.write "LensMake" x.lens_maker
            do! Json.write "LensModel" x.lens_model
            do! Json.write "Distance" x.distance
        }

module Tool = 

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

        let mappers = ConcurrentDictionary<_,LensFun.LensInfo>()

        let q = new BlockingCollection<_>(20)
        let w = new BlockingCollection<_>(20)
        let distorter () = 
            for (sourceFile, img, info : LensFun.LensInfo, targetFile) in q.GetConsumingEnumerable() do 
                try 
                    let undistorted = undistort info.remap.Value img
                    w.Add((sourceFile, info, undistorted, targetFile))
                with e -> 
                    printfn "failed to convert: %s" sourceFile
            

        let writer () =
            for (sourceFile, info, img, targetFile) in w.GetConsumingEnumerable() do 
                if Cv2.ImWrite(targetFile,img) then
                    printfn "%s -> %s" sourceFile targetFile
                else printfn "failed: %s" sourceFile

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

