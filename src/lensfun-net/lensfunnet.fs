(*

lightweight dotnet wrapper for the awesome AGPL licensed lensfun library.
https://github.com/lensfun/lensfun/blob/master/COPYING

please note that lensfun itself is not included in this package but there is a utility
for downloading it from wheels.

*)

#nowarn "9"
namespace lensfunNet

open System
open System.Runtime.InteropServices
open System.IO.Compression


type lfError =
    | LF_NO_ERROR = 0
    | LF_WRONG_FORMAT = 1
    | LF_NO_DATABASE = 2

type lfLensType =
    | LF_UNKNOWN = 0
    | LF_RECTILINEAR = 1
    | LF_FISHEYE = 2
    | LF_EQUIRECTANGULAR = 3
    | LF_FISHEYE_ORTHOGRAPHIC = 4
    | LF_FISHEYE_STEREOGRAPHIC = 5
    | LF_FISHEYE_EQUISOLID = 6
    | LF_FISHEYE_THOBY = 7

type lfPixelFormat =
  | LF_PF_U8 = 0
  | LF_PF_U16 = 1
  | LF_PF_U32 = 2
  | LF_PF_F32 = 3
  | LF_PF_F64 = 4




module Native = 

    [<StructLayout(LayoutKind.Sequential)>]
    type lfCamera =
        struct
            val Maker : IntPtr
            val Model : IntPtr
            val Variant : IntPtr
            val Mount : IntPtr
            val CropFactor : float32
            val Score : int
        end

    [<StructLayout(LayoutKind.Sequential)>]
    type lfLens =
        struct
            val maker_ptr : IntPtr
            val model_ptr : IntPtr
            val MinFocal : float32
            val MaxFocal : float32
            val MinAperture : float32
            val MaxAperture : float32
            val mounts_ptr : IntPtr
            //val LensType : int
            val CenterX : float32
            val CenterY : float32
            val CropFactor : float32
            val AspectRatio : float32
            val distortion_ptr : IntPtr
            val calidTCA_ptr : IntPtr
            val calibVignetting_ptr : IntPtr
            val calibVignetting_ptr2 : IntPtr
            val calibVignetting_ptr3 : IntPtr
            val Score : int
        end

        with 
            member x.Maker = Marshal.PtrToStringAnsi(x.maker_ptr)
            member x.Model = Marshal.PtrToStringAnsi(x.model_ptr)
            member x.Mounts =  "" //Marshal.PtrToStringAnsi(Marshal.ReadIntPtr x.mounts_ptr)

    [<Literal>]
    let lib = "lensfun.dll"
    
    // lfDatabase *lf_db_new (void
    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern IntPtr lf_db_new();

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern lfError lf_db_load(IntPtr)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern lfError lf_db_load_path (IntPtr db, string pathname)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl)>]
    extern lfError lf_db_load_file (IntPtr db, string pathname)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern IntPtr lf_db_find_cameras (IntPtr db, string maker, string model)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern IntPtr lf_db_find_lenses_hd (IntPtr db, nativeint camera, string maker, string lens, int flags)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern IntPtr lf_modifier_new (IntPtr lens, float32 crop, int width, int height)

    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern int lf_modifier_initialize(IntPtr modifier, IntPtr lens, lfPixelFormat format, float32 focal, float32 aperture, float32 distance, float32 scale,lfLensType targeom, int flags, bool reverse)
    
    [<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern bool lf_modifier_apply_geometry_distortion (
        IntPtr modifier, float32 xu, float32 yu, int width, int height, float32[] res)


type Params = 
    {
        cam_maker : string
        cam_model : string
        focal_len : float
        aperture  : float
        lens_maker : string
        lens_model : string
        distance   : float
    }

module NativeInt =
    open FSharp.NativeInterop
    let inline read<'a when 'a : unmanaged> (ptr : nativeint) =
        NativePtr.read (NativePtr.ofNativeInt<'a> ptr) 

module LensFun = 
    open System.IO
    open System.Net

    [<Literal>]
    let private Win = "https://files.pythonhosted.org/packages/34/cd/91caaf4323d738ff3757271569ba4e9335c9b2bbc78500370d90a4ecbb7b/lensfunpy-1.7.0-cp35-cp35m-win_amd64.whl"
    [<Literal>]
    let private Linux = "https://files.pythonhosted.org/packages/fd/41/b1642ae2df00864b1ad6d1ef9d8fc5a1a73a50432dc6b25bcc1cf351a990/lensfunpy-1.7.0-cp35-cp35m-manylinux2010_x86_64.whl"
    [<Literal>]
    let private Mac = "https://files.pythonhosted.org/packages/2a/ba/1e8013654a2def861efa2cd93cc4cfdec3e209d78275d8dd167d423856b8/lensfunpy-1.7.0-cp35-cp35m-macosx_10_9_intel.whl"

    /// either use custom build & db. this call downloads and unpacks lensfunpy's bundle, see: lensfunpy
    /// thanks to lensfunpy for the wheels!
    let downloadLensFunFromWheels (libDir : string) (dbDir : string) = 
        if not (Directory.Exists dbDir) || Seq.isEmpty (Directory.EnumerateFiles dbDir) then
            let tmp = Path.GetTempFileName()
            let tmpFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())
            use wc = new WebClient()
            let download =
                if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then Win
                elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then Linux
                elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then Mac
                else failwith "unsupported platform"
            wc.DownloadFile(download, tmp)
            ZipFile.ExtractToDirectory(tmp, tmpFolder)
            let libs = 
                Directory.EnumerateFiles(Path.Combine(tmpFolder,"lensfunpy"))
                |> Seq.filter (fun f -> let e = Path.GetExtension f in e <> ".py" && e <> ".pyd")
            for l in libs do File.Copy(l, Path.Combine(libDir, Path.GetFileName l), false)
            Directory.CreateDirectory dbDir |> ignore
            for dbFile in Directory.EnumerateFiles(Path.Combine(tmpFolder,"lensfunpy","db_files")) do
                File.Copy(dbFile, Path.Combine(dbDir, Path.GetFileName dbFile))

    let initLf (dbDir : string) = 
        let db = Native.lf_db_new()

        for p in Directory.EnumerateFiles(dbDir,"*.xml") do
            let e = Native.lf_db_load_file (db, p)
            if e <> lfError.LF_NO_ERROR then 
                failwithf "could not load: %s" p

        db

    type CameraInfo = 
        {
            Maker : string
            Model : string
            Variant : string
            Mount : string
        }

    type LensInfo = 
        {
            Maker : string
            Model : string
            LensType : int
            MinFocal : float32
            MaxFocal : float32
            MinAperture : float32
            MaxAperture : float32
            CropFactor  : float32
            CenterX : float32
            CenterY : float32
            Score : int
        }

    type ImageInfo = 
        {
            imageParams : Params
            width    : int
            height   : int
            cameraInfo : CameraInfo
            lensInfo : LensInfo
            remap    : Option<float32[]>
        }

    let createModifier (db : IntPtr) (p : Params) (width : int) (height : int)  =
        let cams = Native.lf_db_find_cameras(db, p.cam_maker, p.cam_model)
        if cams = IntPtr.Zero then failwith "could not find camera"
        let cam0ptr = NativeInt.read (nativeint cams)
        let cam : Native.lfCamera = NativeInt.read cam0ptr
        let maker = Marshal.PtrToStringAnsi(cam.Maker)
        let model = Marshal.PtrToStringAnsi(cam.Model)
        let variant = Marshal.PtrToStringAnsi(cam.Variant)
        let variant = if variant = null then "" else variant
        let mount =  Marshal.PtrToStringAnsi(cam.Mount)
        let lenses = Native.lf_db_find_lenses_hd(db, cam0ptr, p.lens_maker, p.lens_model, 0)
        if cams = IntPtr.Zero then failwith "could not find lens"
        let lens : IntPtr = NativeInt.read (nativeint lenses)
        let lensValue : Native.lfLens = NativeInt.read lens
        
        let modifier = Native.lf_modifier_new(lens,cam.CropFactor,width,height)
        let work = Native.lf_modifier_initialize(modifier, lens, lfPixelFormat.LF_PF_U32, float32 p.focal_len, float32 p.aperture,float32 p.distance,0.0f,lfLensType.LF_RECTILINEAR,~~~0,false)

        let remap = Array.zeroCreate (width * height * 2)
        let ok = Native.lf_modifier_apply_geometry_distortion(modifier,0.0f,0.0f,width,height,remap)
        if not ok then failwith "could not apply distortion"
        {
            remap = Some remap
            lensInfo = 
                {
                    Maker        = lensValue.Maker
                    Model        = lensValue.Model
                    LensType     = 0
                    MinFocal     = lensValue.MinFocal
                    MaxFocal     = lensValue.MaxFocal
                    MinAperture  = lensValue.MinAperture
                    MaxAperture  = lensValue.MaxAperture
                    CropFactor   = lensValue.CropFactor
                    CenterX      = lensValue.CenterX
                    CenterY      = lensValue.CenterY
                    Score        = lensValue.Score
                }
            cameraInfo = 
                {   
                    Maker = maker
                    Model = model
                    Variant = variant
                    Mount = mount
                }
            imageParams = p
            width = width
            height = height
        }
  
type Modifier(db : IntPtr, p : Params) = 
    let m = LensFun.createModifier db p
    member x.ComputeMap(width : int, height: int) : LensFun.ImageInfo = m width height

type LensFun(dbDir : string) =
    let db = LensFun.initLf dbDir
    member x.CreateModifier(p : Params) = 
        Modifier(db, p)