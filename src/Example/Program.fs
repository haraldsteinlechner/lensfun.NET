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
    let remap,_ = LensFun.createModifier db cameraParameters width height 
    let outimg = undistort remap img

    Cv2.ImWrite(undistored, outimg) |> printfn "ok: %A"



[<EntryPoint;STAThread>]
let main argv =
    LensFun.downloadLensFunFromWheels "." "./db_files"

    undistortJpg @"C:\Users\hs\Downloads\IMG_4491.JPG" @"C:\Users\hs\Downloads\IMG_4491_undist.JPG"
    Raw.test @"C:\Users\hs\Pictures\H5_105\IMG_6975.CR2" @"C:\Users\hs\Pictures\H5_105\IMG_6975_undist.exr"
    0
 