open System
open lensfunNet
open OpenCvSharp


let undistort (remap : float32[])  (img : Mat)  = 
    let size = img.Size()
    let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

    let outimg = new Mat(size, img.Type())
    let map = InputArray.Create(remapMat)
    Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
    outimg

let undistortJpg (filename : string) (undistored : string) =
    let db = LensFun.initLf "./db_files"
    let cameraParameters = Exif.fileToParams filename
    let img = Cv2.ImRead filename
    let width,height = img.Size(1), img.Size(0)
    let lensInfo = LensFun.createModifier db cameraParameters width height 
    let outimg = undistort lensInfo.remap.Value img

    let info = { MetaData.imageInfo = lensInfo } |> MetaData.toJson

    Cv2.ImWrite(undistored, outimg) |> printfn "ok: %A"



[<EntryPoint;STAThread>]
let main argv =
    LensFun.downloadLensFunFromWheels "." "./db_files"


    undistortJpg @"C:\Users\hs\OneDrive - Aardworx GmbH\fotofascade\Datensaetze Fotoaufnahmen\H6_50\IMG_5545.JPG" @"img_undist.jpg"

    0
 