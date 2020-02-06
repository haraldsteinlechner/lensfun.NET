#nowarn "9"
namespace lensfunNet

open System
open System.Runtime.InteropServices


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
  (** Unsigned 8-bit R,G,B *)
  | LF_PF_U8 = 0
   (** Unsigned 16-bit R,G,B *)
  | LF_PF_U16 = 1
   (** Unsigned 32-bit R,G,B *)
  | LF_PF_U32 = 2
   (** 32-bit floating-point R,G,B *)
  | LF_PF_F32 = 3
   (** 64-bit floating-point R,G,B *)
  | LF_PF_F64 = 4


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

    (*
    /** @brief Camera maker (ex: "Rollei") -- same as in EXIF */
    lfMLstr Maker;
    /** @brief Model name (ex: "Rolleiflex SL35") -- same as in EXIF */
    lfMLstr Model;
    /** @brief Camera variant. Some cameras use same EXIF id for different models */
    lfMLstr Variant;
    /** @brief Camera mount type (ex: "QBM") */
    char *Mount;
    /** @brief Camera crop factor (ex: 1.0). Must be defined. */
    float CropFactor;
    /** @brief Camera matching score, used while searching: not actually a camera parameter */
    int Score; *)



module Native = 

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

    //[<DllImport(lib, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    //extern bool lf_modifier_apply_geometry_distortionArr (
    //    IntPtr modifier, float32 xu, float32 yu, int width, int height, float32[])


type Params = 
    {
        cam_maker : string
        cam_model : string
        focal_len : float
        aperture : float
        lens_maker : string
        lens_model : string
        distance : float
    }

module NativeInt =
    open FSharp.NativeInterop
    let inline read<'a when 'a : unmanaged> (ptr : nativeint) =
        NativePtr.read (NativePtr.ofNativeInt<'a> ptr) 

module LensFun = 
    open System.IO

    let initLf (dbDir : string) = 
        let db = Native.lf_db_new()

        for p in Directory.EnumerateFiles("./db_files","*.xml") do
            let e = Native.lf_db_load_file (db, p)
            if e <> lfError.LF_NO_ERROR then 
                failwithf "could not load: %s" p

        db

    let createModifier (db : IntPtr) (width : int) (height : int) (p : Params) =
        let cams = Native.lf_db_find_cameras(db, p.cam_maker, p.cam_model)
        if cams = IntPtr.Zero then failwith "could not find camera"
        let cam0ptr = NativeInt.read (nativeint cams)
        let cam : lfCamera = NativeInt.read cam0ptr
        let maker = Marshal.PtrToStringAnsi(cam.Maker)
        let model = Marshal.PtrToStringAnsi(cam.Model)
        let variant = Marshal.PtrToStringAnsi(cam.Variant)
        let mount =  Marshal.PtrToStringAnsi(cam.Mount)
        let lenses = Native.lf_db_find_lenses_hd(db, cam0ptr, p.lens_maker, p.lens_model, 0)
        if cams = IntPtr.Zero then failwith "could not find lens"
        let lens : IntPtr = NativeInt.read (nativeint lenses)
        
        let modifier = Native.lf_modifier_new(lens,cam.CropFactor,width,height)
        let work = Native.lf_modifier_initialize(modifier, lens, lfPixelFormat.LF_PF_U8, float32 p.focal_len, float32 p.aperture,float32 p.distance,0.0f,lfLensType.LF_RECTILINEAR,~~~0,false)

        let remap = Array.zeroCreate (width * height * 2)
        let ok = Native.lf_modifier_apply_geometry_distortion(modifier,0.0f,0.0f,width,height,remap)
        if not ok then failwith "could not apply distortion"
        remap, cam.Score
  

