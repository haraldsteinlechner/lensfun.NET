namespace lensfunNet

open ImageMagick
open OpenCvSharp
open System.Runtime.InteropServices

module Raw = 

    module Native = 
        open System
        [<DllImport("msvcrt.dll")>]
        extern int memcpy (IntPtr target, IntPtr src, int size);



    let undistort (remap : float32[])  (img : Mat) (outimg : Mat) = 
        let size = img.Size()
        let remapMat = new Mat(size.Height, size.Width, MatType.CV_32FC2, remap)

        //let outimg = new Mat(size, img.Type())
        let map = InputArray.Create(remapMat)
        Cv2.Remap(InputArray.Create img, OutputArray.Create outimg,map,InputArray.Create(new Mat()), InterpolationFlags.Lanczos4)
        outimg

    let test (filename : string) (undistName : string) = 
        use img = new MagickImage(filename)

        let data = img.GetPixelsUnsafe().ToArray()
        use mat = new Mat(img.Height, img.Width, MatType.CV_32FC3, data)

        let db = LensFun.initLf "./db_files"
        let cameraParameters = Exif.fileToParams @"C:\Users\hs\Downloads\IMG_4491.JPG"
        let width,height = img.Width, img.Height
        let remap,_ = LensFun.createModifier db cameraParameters width height 
        //use outmat = new Mat(img.Height, img.Width, MatType.CV_32FC3, resultPtr.AddrOfPinnedObject())
        let outimg = OpenCV.undistort remap mat 
        
        Cv2.ImWrite(undistName,outimg) |> printfn "wrote..: %A"

        //let data2 : byte[] = Array.zeroCreate (img.Width*img.Height*3*sizeof<float32>)
        //Marshal.Copy(outimg.Data,data2,0,data2.Length)    
        //img.ReadPixels(data2,PixelReadSettings(img.Width,img.Height,StorageType.Float,PixelMapping.RGB))
        //img.Write(System.IO.Path.ChangeExtension(undistName,".exr"), MagickFormat.Exr)

        ()