/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Meta.XR.Acoustics;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Meta.XR.Acoustics
{
    /***********************************************************************************/
    // ENUMS and STRUCTS
    /***********************************************************************************/
    public enum AcousticModel : int
    {
        Automatic = -1, ///< Automatically select highest quality (if geometry is set the propagation system will be active, otherwise if the callback is set dynamic room modeling is enabled, otherwise fallback to the static shoe box)
        None = 0, ///< Disable all acoustics features
        ShoeboxRoom = 1, ///< Room defined by RoomAcousticProperties (MetaXRRoomAcoustic components)
        RaytracedAcoustics = 3, ///< Geometry, Material based propagation system (MetaXRAcoustic components)
    }

    [Flags]
    public enum EnableFlagInternal : uint
    {
        NONE = 0,
        SIMPLE_ROOM_MODELING = 2,
        LATE_REVERBERATION = 3,
        RANDOMIZE_REVERB = 4,
        PERFORMANCE_COUNTERS = 5,
        DIFFRACTION = 6,
    }

    public enum FaceType : uint
    {
        TRIANGLES = 0,
        QUADS
    }

    public enum MaterialProperty : uint
    {
        ABSORPTION = 0,
        TRANSMISSION,
        SCATTERING
    }

    // Matches internal mesh layout
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MeshGroup
    {
        public UIntPtr indexOffset;
        public UIntPtr faceCount;
        [MarshalAs(UnmanagedType.U4)]
        public FaceType faceType;
        public IntPtr material;
    }

    [Flags]
    public enum AcousticMapStatus : uint
    {
        EMPTY = 0,
        MAPPED = (1 << 0),
        READY = (1 << 1) | MAPPED
    }

    [Flags]
    public enum AcousticMapFlags : uint
    {
        NONE = 0,
        STATIC_ONLY = (1 << 0),
        NO_FLOATING = (1 << 1),
        MAP_ONLY = (1 << 2),
        DIFFRACTION = (1 << 3)
    }

    [Flags]
    public enum ObjectFlags : uint
    {
        EMPTY = 0,
        ENABLED = (1 << 0),
        STATIC = (1 << 1)
    }

    [Flags]
    public enum MeshFlags : uint
    {
        NONE = 0,
        ENABLE_SIMPLIFICATION = (1 << 0),
        ENABLE_DIFFRACTION = (1 << 1),
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MeshSimplification
    {
        public UIntPtr thisSize;
        [MarshalAs(UnmanagedType.U4)]
        public MeshFlags flags;
        public float unitScale;
        public float maxError;
        public float minDiffractionEdgeAngle;
        public float minDiffractionEdgeLength;
        public float flagLength;
        public UIntPtr threadCount;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool ProgressCallback(IntPtr userData, string description, float progress);

    [StructLayout(LayoutKind.Sequential)]
    public struct SceneIRCallbacks
    {
        public IntPtr userData;

        [MarshalAs(UnmanagedType.FunctionPtr)]
        public ProgressCallback progress;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MapParameters
    {
        public UIntPtr thisSize;
        public SceneIRCallbacks callbacks;
        public UIntPtr threadCount;
        public UIntPtr reflectionCount;

        [MarshalAs(UnmanagedType.U4)]
        public AcousticMapFlags flags;

        public float minResolution;
        public float maxResolution;

        public float headHeight;
        public float maxHeight;

        public float gravityVectorX;
        public float gravityVectorY;
        public float gravityVectorZ;
    }

    public enum ControlZoneProperty : uint
    {
        RT60 = 0,
        REVERB_LEVEL
    }
}

public class MetaXRAcousticNativeInterface
{
    static INativeInterface CachedInterface;
    internal static INativeInterface Interface { get { if (CachedInterface == null) CachedInterface = FindInterface(); return CachedInterface; } }

    static INativeInterface FindInterface()
    {
        const int MINIMUM_SDK_VERSION = 92;
        try
        {
            IntPtr temp = WwisePluginInterface.getOrCreateGlobalOvrAudioContext();
            WwisePluginInterface.ovrAudio_GetVersion(out int major, out int minor, out int patch);
            if (minor < MINIMUM_SDK_VERSION)
            {
                Debug.LogError("Incompatible SDK version, update your MetaXRAudioWwise plugin");
                return new DummyInterface();
            }
            Debug.Log("Meta XR Audio Native Interface initialized with Wwise plugin");
            return new WwisePluginInterface();
        }
        catch (System.DllNotFoundException)
        {
            // this is fine
        }

        try
        {
            FMODPluginInterface.ovrAudio_GetPluginContext(out IntPtr temp);
            FMODPluginInterface.ovrAudio_GetVersion(out int major, out int minor, out int patch);
            if (minor < MINIMUM_SDK_VERSION)
            {
                Debug.LogError("Incompatible SDK version, update your MetaXRAudioFMOD plugin");
                return new DummyInterface();
            }
            Debug.Log("Meta XR Audio Native Interface initialized with FMOD plugin");
            return new FMODPluginInterface();
        }
        catch (System.DllNotFoundException)
        {
            // this is fine
        }

        try
        {
            UnityNativeInterface.ovrAudio_GetPluginContext(out IntPtr temp);
            UnityNativeInterface.ovrAudio_GetVersion(out int major, out int minor, out int patch);
            if (minor < MINIMUM_SDK_VERSION)
            {
                Debug.LogError("Incompatible SDK version, update your MetaXRAudioFMOD plugin");
                return new DummyInterface();
            }
            Debug.Log("Meta XR Audio Native Interface initialized with Unity plugin");
            return new UnityNativeInterface();
        }
        catch
        {
            Debug.LogError("Unable to located MetaXRAudio plugin for MetaXRAcoustics!\n" +
                "If you're using Unity audio make sure you have imported the MetaXRAudioUnity package\n" +
                "If you're using Wwise or FMOD make sure you have their Unity integration in your project and the MetaXRAudioWwise or MetaXRAudioFMOD plugins in correct location in the Assets folder");
        }

        return new DummyInterface();
    }

    public enum ovrAudioScalarType : uint
    {
        Int8,
        UInt8,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float16,
        Float32,
        Float64
    }

    public interface INativeInterface
    {
        /***********************************************************************************/
        // Settings API
        int SetAcousticModel(AcousticModel model);

        int SetEnabled(int feature, bool enabled);
        int SetEnabled(EnableFlagInternal feature, bool enabled);

        /***********************************************************************************/
        // Geometry API
        int CreateAudioGeometry(out IntPtr geometry);
        int DestroyAudioGeometry(IntPtr geometry);
        int AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, bool enabled);
        int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount);
        int AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount,
                                                        ref MeshSimplification simplification);
        int AudioGeometrySetTransform(IntPtr geometry, in Matrix4x4 matrix);
        int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        int AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        int AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength);
        int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        int AudioGeometryGetSimplifiedMesh(IntPtr geometry, out float[] vertices, out uint[] indices, out uint[] materialIndices);

        /***********************************************************************************/
        // Material API
        int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        int CreateAudioMaterial(out IntPtr material);
        int DestroyAudioMaterial(IntPtr material);
        int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        int AudioMaterialReset(IntPtr material, MaterialProperty property);
        /***********************************************************************************/
        // Acoustic Map API
        int CreateAudioSceneIR(out IntPtr sceneIR);
        int DestroyAudioSceneIR(IntPtr sceneIR);
        int AudioSceneIRSetEnabled(IntPtr sceneIR, bool enabled);
        int AudioSceneIRGetEnabled(IntPtr sceneIR, out bool enabled);
        int AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status);
        int InitializeAudioSceneIRParameters(out MapParameters parameters);
        int AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters);
        int AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters);
        int AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount);
        int AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount);
        int AudioSceneIRSetTransform(IntPtr sceneIR, in Matrix4x4 matrix);
        int AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4);
        int AudioSceneIRWriteFile(IntPtr sceneIR, string filePath);
        int AudioSceneIRReadFile(IntPtr sceneIR, string filePath);
        int AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength);

        /***********************************************************************************/
        // Control Zone API
        int CreateControlZone(out IntPtr control);
        int DestroyControlZone(IntPtr control);
        int ControlZoneSetEnabled(IntPtr control, bool enabled);
        int ControlZoneGetEnabled(IntPtr control, out bool enabled);
        int ControlZoneSetTransform(IntPtr control, in Matrix4x4 matrix);
        int ControlZoneGetTransform(IntPtr control, out float[] matrix4x4);
        int ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        int ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        int ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        int ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        int ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        int ControlZoneReset(IntPtr control, ControlZoneProperty property);
    }

    /***********************************************************************************/
    // UNITY NATIVE
    /***********************************************************************************/
    public class UnityNativeInterface : INativeInterface
    {
        // The name used for the plugin DLL.
        public const string binaryName = "MetaXRAudioUnity";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        IntPtr context
        {
            get
            {
                if (context_ == IntPtr.Zero)
                {
                    ovrAudio_GetPluginContext(out context_);
                    ovrAudio_GetVersion(out int major, out version, out int patch);
                }
                return context_;
            }
        }
        int version = 0;

        [DllImport(binaryName)]
        public static extern int ovrAudio_GetPluginContext(out IntPtr context);

        [DllImport(binaryName)]
        public static extern IntPtr ovrAudio_GetVersion(out int Major, out int Minor, out int Patch);

        /***********************************************************************************/
        // Settings API
        [DllImport(binaryName)]
        private static extern int ovrAudio_SetAcousticModel(IntPtr context, AcousticModel quality);
        public int SetAcousticModel(AcousticModel model)
        {
            return ovrAudio_SetAcousticModel(context, model);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, EnableFlagInternal what, int enable);
        public int SetEnabled(EnableFlagInternal feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, int enabled);
        public int AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, bool enabled)
        {
            if (version < 94)
                return -1;

            return ovrAudio_AudioGeometrySetObjectFlag(geometry, flag, enabled ? 1 : 0);
        }


        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount,
                                                                        ref MeshSimplification simplification);

        public int AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount,
                                                        ref MeshSimplification simplification)
        {
            return ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount, ref simplification);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float* matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioGeometrySetTransform(geometry, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength);
        public int AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioGeometryReadMeshMemory(geometry, data, dataLength);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, IntPtr unused1, out uint numVertices, IntPtr unused2, IntPtr unused3, out uint numTriangles);

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, float[] vertices, ref uint numVertices, uint[] indices, uint[] materialIndices, ref uint numTriangles);

        public int AudioGeometryGetSimplifiedMesh(IntPtr geometry, out float[] vertices, out uint[] indices, out uint[] materialIndices)
        {
            int result = ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, IntPtr.Zero, out uint numVertices, IntPtr.Zero, IntPtr.Zero, out uint numTriangles);
            if (result != 0)
            {
                Debug.LogError("unexpected error getting simplified mesh array sizes");
                vertices = null;
                indices = null;
                materialIndices = null;
                return result;
            }

            vertices = new float[numVertices * 3];
            indices = new uint[numTriangles * 3];
            materialIndices = new uint[numTriangles];
            return ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, vertices, ref numVertices, indices, materialIndices, ref numTriangles);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }
        /***********************************************************************************/
        // Acoustic Map API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioSceneIR(IntPtr context, out IntPtr sceneIR);
        public int CreateAudioSceneIR(out IntPtr sceneIR)
        {
            return ovrAudio_CreateAudioSceneIR(context, out sceneIR);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioSceneIR(IntPtr sceneIR);
        public int DestroyAudioSceneIR(IntPtr sceneIR)
        {
            return ovrAudio_DestroyAudioSceneIR(sceneIR);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRSetEnabled(IntPtr sceneIR, int enabled);
        public int AudioSceneIRSetEnabled(IntPtr sceneIR, bool enabled)
        {
            return ovrAudio_AudioSceneIRSetEnabled(sceneIR, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetEnabled(IntPtr sceneIR, out int enabled);
        public int AudioSceneIRGetEnabled(IntPtr sceneIR, out bool enabled)
        {
            int iEnabled;
            int res = ovrAudio_AudioSceneIRGetEnabled(sceneIR, out iEnabled);
            enabled = iEnabled != 0;
            return res;
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status);
        public int AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status)
        {
            return ovrAudio_AudioSceneIRGetStatus(sceneIR, out status);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_InitializeAudioSceneIRParameters(out MapParameters parameters);
        public int InitializeAudioSceneIRParameters(out MapParameters parameters)
        {
            return ovrAudio_InitializeAudioSceneIRParameters(out parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters);
        public int AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRCompute(sceneIR, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters);
        public int AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRComputeCustomPoints(sceneIR, points, pointCount, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount);
        public int AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount)
        {
            return ovrAudio_AudioSceneIRGetPointCount(sceneIR, out pointCount);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount);
        public int AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount)
        {
            return ovrAudio_AudioSceneIRGetPoints(sceneIR, points, maxPointCount);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioSceneIRSetTransform(IntPtr sceneIR, float* matrix4x4);
        public int AudioSceneIRSetTransform(IntPtr sceneIR, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioSceneIRSetTransform(sceneIR, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4);
        public int AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4)
        {
            return ovrAudio_AudioSceneIRGetTransform(sceneIR, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRWriteFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRWriteFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRWriteFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRReadFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRReadFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength);
        public int AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioSceneIRReadMemory(sceneIR, data, dataLength);
        }

        /***********************************************************************************/
        // Control Zone API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlZone(IntPtr context, out IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlVolume(IntPtr context, out IntPtr control);
        public int CreateControlZone(out IntPtr control)
        {
            try
            {
                return ovrAudio_CreateControlZone(context, out control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_CreateControlVolume(context, out control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlZone(IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlVolume(IntPtr control);
        public int DestroyControlZone(IntPtr control)
        {
            try
            {
                return ovrAudio_DestroyControlZone(control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_DestroyControlVolume(control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetEnabled(IntPtr control, int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetEnabled(IntPtr control, int enabled);
        public int ControlZoneSetEnabled(IntPtr control, bool enabled)
        {
            try
            {
                return ovrAudio_ControlZoneSetEnabled(control, enabled ? 1 : 0);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetEnabled(control, enabled ? 1 : 0);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetEnabled(IntPtr control, out int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetEnabled(IntPtr control, out int enabled);
        public int ControlZoneGetEnabled(IntPtr control, out bool enabled)
        {
            int enabledInt = 0;
            int result;

            try
            {
                result = ovrAudio_ControlZoneGetEnabled(control, out enabledInt);
            }
            catch
            {
                // Hack for v60 compatibility
                result = ovrAudio_ControlVolumeGetEnabled(control, out enabledInt);
            }
            enabled = enabledInt != 0;
            return result;
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlZoneSetTransform(IntPtr control, float* matrix4x4);
        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlVolumeSetTransform(IntPtr control, float* matrix4x4);
        public int ControlZoneSetTransform(IntPtr control, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;
                try
                {
                    return ovrAudio_ControlZoneSetTransform(control, nativeMatrixCopy);
                }
                catch
                {
                    // Hack for v60 compatibility
                    return ovrAudio_ControlVolumeSetTransform(control, nativeMatrixCopy);
                }
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetTransform(IntPtr control, out float[] matrix4x4);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetTransform(IntPtr control, out float[] matrix4x4);
        public int ControlZoneGetTransform(IntPtr control, out float[] matrix4x4)
        {
            try
            {
                return ovrAudio_ControlZoneGetTransform(control, out matrix4x4);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetTransform(control, out matrix4x4);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        public int ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetBox(control, sizeX, sizeY, sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetBox(control, sizeX, sizeY, sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        public int ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        public int ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        public int ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        public int ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value)
        {
            try
            {
                return ovrAudio_ControlZoneSetFrequency(control, property, frequency, value);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFrequency(control, property, frequency, value);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneReset(IntPtr control, ControlZoneProperty property);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeReset(IntPtr control, ControlZoneProperty property);
        public int ControlZoneReset(IntPtr control, ControlZoneProperty property)
        {
            try
            {
                return ovrAudio_ControlZoneReset(control, property);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeReset(control, property);
            }
        }
    }

    /***********************************************************************************/
    // WWISE
    /***********************************************************************************/
    public class WwisePluginInterface : INativeInterface
    {
        // The name used for the plugin DLL.
        public const string binaryName = "MetaXRAudioWwise";
        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        int version;

        IntPtr context
        {
            get
            {
                if (context_ == IntPtr.Zero)
                {
                    context_ = getOrCreateGlobalOvrAudioContext();
                    ovrAudio_GetVersion(out int major, out version, out int patch);
                }
                return context_;
            }
        }

        [DllImport(binaryName)]
        public static extern IntPtr getOrCreateGlobalOvrAudioContext();

        [DllImport(binaryName)]
        public static extern IntPtr ovrAudio_GetVersion(out int Major, out int Minor, out int Patch);

        /***********************************************************************************/
        // Settings API
        [DllImport(binaryName)]
        private static extern int ovrAudio_SetAcousticModel(IntPtr context, AcousticModel quality);
        public int SetAcousticModel(AcousticModel model)
        {
            return ovrAudio_SetAcousticModel(context, model);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, EnableFlagInternal what, int enable);
        public int SetEnabled(EnableFlagInternal feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, int enabled);
        public int AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, bool enabled)
        {
            if (version < 94)
                return -1;

            return ovrAudio_AudioGeometrySetObjectFlag(geometry, flag, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount,
                                                                        ref MeshSimplification simplification);

        public int AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount,
                                                        ref MeshSimplification simplification)
        {
            return ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount, ref simplification);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float* matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioGeometrySetTransform(geometry, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength);
        public int AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioGeometryReadMeshMemory(geometry, data, dataLength);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, IntPtr unused1, out uint numVertices, IntPtr unused2, IntPtr unused3, out uint numTriangles);

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, float[] vertices, ref uint numVertices, uint[] indices, uint[] materialIndices, ref uint numTriangles);

        public int AudioGeometryGetSimplifiedMesh(IntPtr geometry, out float[] vertices, out uint[] indices, out uint[] materialIndices)
        {
            int result = ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, IntPtr.Zero, out uint numVertices, IntPtr.Zero, IntPtr.Zero, out uint numTriangles);
            if (result != 0)
            {
                Debug.LogError("unexpected error getting simplified mesh array sizes");
                vertices = null;
                indices = null;
                materialIndices = null;
                return result;
            }

            vertices = new float[numVertices * 3];
            indices = new uint[numTriangles * 3];
            materialIndices = new uint[numTriangles];
            return ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, vertices, ref numVertices, indices, materialIndices, ref numTriangles);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }

        /***********************************************************************************/
        // Acoustic Map API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioSceneIR(IntPtr context, out IntPtr sceneIR);
        public int CreateAudioSceneIR(out IntPtr sceneIR)
        {
            return ovrAudio_CreateAudioSceneIR(context, out sceneIR);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioSceneIR(IntPtr sceneIR);
        public int DestroyAudioSceneIR(IntPtr sceneIR)
        {
            return ovrAudio_DestroyAudioSceneIR(sceneIR);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRSetEnabled(IntPtr sceneIR, int enabled);
        public int AudioSceneIRSetEnabled(IntPtr sceneIR, bool enabled)
        {
            return ovrAudio_AudioSceneIRSetEnabled(sceneIR, enabled ? 1 : 0);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetEnabled(IntPtr sceneIR, out int enabled);
        public int AudioSceneIRGetEnabled(IntPtr sceneIR, out bool enabled)
        {
            int iEnabled;
            int res = ovrAudio_AudioSceneIRGetEnabled(sceneIR, out iEnabled);
            enabled = iEnabled != 0;
            return res;
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status);
        public int AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status)
        {
            return ovrAudio_AudioSceneIRGetStatus(sceneIR, out status);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_InitializeAudioSceneIRParameters(out MapParameters parameters);
        public int InitializeAudioSceneIRParameters(out MapParameters parameters)
        {
            return ovrAudio_InitializeAudioSceneIRParameters(out parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters);
        public int AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRCompute(sceneIR, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters);
        public int AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRComputeCustomPoints(sceneIR, points, pointCount, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount);
        public int AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount)
        {
            return ovrAudio_AudioSceneIRGetPointCount(sceneIR, out pointCount);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount);
        public int AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount)
        {
            return ovrAudio_AudioSceneIRGetPoints(sceneIR, points, maxPointCount);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioSceneIRSetTransform(IntPtr sceneIR, float* matrix4x4);
        public int AudioSceneIRSetTransform(IntPtr sceneIR, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioSceneIRSetTransform(sceneIR, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4);
        public int AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4)
        {
            return ovrAudio_AudioSceneIRGetTransform(sceneIR, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRWriteFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRWriteFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRWriteFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRReadFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRReadFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength);
        public int AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioSceneIRReadMemory(sceneIR, data, dataLength);
        }

        /***********************************************************************************/
        // Control Zone API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlZone(IntPtr context, out IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlVolume(IntPtr context, out IntPtr control);
        public int CreateControlZone(out IntPtr control)
        {
            try
            {
                return ovrAudio_CreateControlZone(context, out control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_CreateControlVolume(context, out control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlZone(IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlVolume(IntPtr control);
        public int DestroyControlZone(IntPtr control)
        {
            try
            {
                return ovrAudio_DestroyControlZone(control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_DestroyControlVolume(control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetEnabled(IntPtr control, int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetEnabled(IntPtr control, int enabled);
        public int ControlZoneSetEnabled(IntPtr control, bool enabled)
        {
            try
            {
                return ovrAudio_ControlZoneSetEnabled(control, enabled ? 1 : 0);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetEnabled(control, enabled ? 1 : 0);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetEnabled(IntPtr control, out int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetEnabled(IntPtr control, out int enabled);
        public int ControlZoneGetEnabled(IntPtr control, out bool enabled)
        {
            int enabledInt = 0;
            int result;

            try
            {
                result = ovrAudio_ControlZoneGetEnabled(control, out enabledInt);
            }
            catch
            {
                // Hack for v60 compatibility
                result = ovrAudio_ControlVolumeGetEnabled(control, out enabledInt);
            }
            enabled = enabledInt != 0;
            return result;
        }
        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlZoneSetTransform(IntPtr control, float* matrix4x4);
        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlVolumeSetTransform(IntPtr control, float* matrix4x4);
        public int ControlZoneSetTransform(IntPtr control, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;
                try
                {
                    return ovrAudio_ControlZoneSetTransform(control, nativeMatrixCopy);
                }
                catch
                {
                    // Hack for v60 compatibility
                    return ovrAudio_ControlVolumeSetTransform(control, nativeMatrixCopy);
                }
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetTransform(IntPtr control, out float[] matrix4x4);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetTransform(IntPtr control, out float[] matrix4x4);
        public int ControlZoneGetTransform(IntPtr control, out float[] matrix4x4)
        {
            try
            {
                return ovrAudio_ControlZoneGetTransform(control, out matrix4x4);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetTransform(control, out matrix4x4);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        public int ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetBox(control, sizeX, sizeY, sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetBox(control, sizeX, sizeY, sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        public int ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        public int ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        public int ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        public int ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value)
        {
            try
            {
                return ovrAudio_ControlZoneSetFrequency(control, property, frequency, value);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFrequency(control, property, frequency, value);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneReset(IntPtr control, ControlZoneProperty property);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeReset(IntPtr control, ControlZoneProperty property);
        public int ControlZoneReset(IntPtr control, ControlZoneProperty property)
        {
            try
            {
                return ovrAudio_ControlZoneReset(control, property);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeReset(control, property);
            }
        }
    }

    /***********************************************************************************/
    // FMOD
    /***********************************************************************************/
    public class FMODPluginInterface : INativeInterface
    {
        // The name used for the plugin DLL.
        public const string binaryName = "MetaXRAudioFMOD";

        /***********************************************************************************/
        // Context API: Required to create internal context if it does not exist yet
        IntPtr context_ = IntPtr.Zero;
        int version;
        IntPtr context
        {
            get
            {
                if (context_ == IntPtr.Zero)
                {
                    ovrAudio_GetPluginContext(out context_);
                    ovrAudio_GetVersion(out int major, out version, out int patch);
                }
                return context_;
            }
        }

        [DllImport(binaryName)]
        public static extern int ovrAudio_GetPluginContext(out IntPtr context);

        [DllImport(binaryName)]
        public static extern IntPtr ovrAudio_GetVersion(out int Major, out int Minor, out int Patch);

        /***********************************************************************************/
        // Settings API
        [DllImport(binaryName)]
        private static extern int ovrAudio_SetAcousticModel(IntPtr context, AcousticModel quality);
        public int SetAcousticModel(AcousticModel model)
        {
            return ovrAudio_SetAcousticModel(context, model);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, int what, int enable);
        public int SetEnabled(int feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_Enable(IntPtr context, EnableFlagInternal what, int enable);
        public int SetEnabled(EnableFlagInternal feature, bool enabled)
        {
            return ovrAudio_Enable(context, feature, enabled ? 1 : 0);
        }

        /***********************************************************************************/
        // Geometry API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioGeometry(IntPtr context, out IntPtr geometry);
        public int CreateAudioGeometry(out IntPtr geometry)
        {
            return ovrAudio_CreateAudioGeometry(context, out geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioGeometry(IntPtr geometry);
        public int DestroyAudioGeometry(IntPtr geometry)
        {
            return ovrAudio_DestroyAudioGeometry(geometry);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, int enabled);
        public int AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, bool enabled)
        {
            if (version < 94)
                return -1;

            return ovrAudio_AudioGeometrySetObjectFlag(geometry, flag, enabled ? 1 : 0);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount);

        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount)
        {
            return ovrAudio_AudioGeometryUploadMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                                        float[] vertices, UIntPtr verticesBytesOffset, UIntPtr vertexCount, UIntPtr vertexStride, ovrAudioScalarType vertexType,
                                                                        int[] indices, UIntPtr indicesByteOffset, UIntPtr indexCount, ovrAudioScalarType indexType,
                                                                        MeshGroup[] groups, UIntPtr groupCount,
                                                                        ref MeshSimplification simplification);

        public int AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount,
                                                        ref MeshSimplification simplification)
        {
            return ovrAudio_AudioGeometryUploadSimplifiedMeshArrays(geometry,
                vertices, UIntPtr.Zero, (UIntPtr)vertexCount, UIntPtr.Zero, ovrAudioScalarType.Float32,
                indices, UIntPtr.Zero, (UIntPtr)indexCount, ovrAudioScalarType.UInt32,
                groups, (UIntPtr)groupCount, ref simplification);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioGeometrySetTransform(IntPtr geometry, float* matrix4x4);
        public int AudioGeometrySetTransform(IntPtr geometry, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioGeometrySetTransform(geometry, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4);
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4)
        {
            return ovrAudio_AudioGeometryGetTransform(geometry, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshFile(IntPtr geometry, string filePath);
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryReadMeshFile(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength);
        public int AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioGeometryReadMeshMemory(geometry, data, dataLength);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath);
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath)
        {
            return ovrAudio_AudioGeometryWriteMeshFileObj(geometry, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, IntPtr unused1, out uint numVertices, IntPtr unused2, IntPtr unused3, out uint numTriangles);

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(IntPtr geometry, float[] vertices, ref uint numVertices, uint[] indices, uint[] materialIndices, ref uint numTriangles);

        public int AudioGeometryGetSimplifiedMesh(IntPtr geometry, out float[] vertices, out uint[] indices, out uint[] materialIndices)
        {
            int result = ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, IntPtr.Zero, out uint numVertices, IntPtr.Zero, IntPtr.Zero, out uint numTriangles);
            if (result != 0)
            {
                Debug.LogError("unexpected error getting simplified mesh array sizes");
                vertices = null;
                indices = null;
                materialIndices = null;
                return result;
            }

            vertices = new float[numVertices * 3];
            indices = new uint[numTriangles * 3];
            materialIndices = new uint[numTriangles];
            return ovrAudio_AudioGeometryGetSimplifiedMeshWithMaterials(geometry, vertices, ref numVertices, indices, materialIndices, ref numTriangles);
        }

        /***********************************************************************************/
        // Material API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioMaterial(IntPtr context, out IntPtr material);
        public int CreateAudioMaterial(out IntPtr material)
        {
            return ovrAudio_CreateAudioMaterial(context, out material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioMaterial(IntPtr material);
        public int DestroyAudioMaterial(IntPtr material)
        {
            return ovrAudio_DestroyAudioMaterial(material);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value);
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value)
        {
            return ovrAudio_AudioMaterialSetFrequency(material, property, frequency, value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value);
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value)
        {
            return ovrAudio_AudioMaterialGetFrequency(material, property, frequency, out value);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioMaterialReset(IntPtr material, MaterialProperty property);
        public int AudioMaterialReset(IntPtr material, MaterialProperty property)
        {
            return ovrAudio_AudioMaterialReset(material, property);
        }
        /***********************************************************************************/
        // Acoustic Map API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateAudioSceneIR(IntPtr context, out IntPtr sceneIR);
        public int CreateAudioSceneIR(out IntPtr sceneIR)
        {
            return ovrAudio_CreateAudioSceneIR(context, out sceneIR);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyAudioSceneIR(IntPtr sceneIR);
        public int DestroyAudioSceneIR(IntPtr sceneIR)
        {
            return ovrAudio_DestroyAudioSceneIR(sceneIR);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRSetEnabled(IntPtr sceneIR, int enabled);
        public int AudioSceneIRSetEnabled(IntPtr sceneIR, bool enabled)
        {
            return ovrAudio_AudioSceneIRSetEnabled(sceneIR, enabled ? 1 : 0);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetEnabled(IntPtr sceneIR, out int enabled);
        public int AudioSceneIRGetEnabled(IntPtr sceneIR, out bool enabled)
        {
            int iEnabled;
            int res = ovrAudio_AudioSceneIRGetEnabled(sceneIR, out iEnabled);
            enabled = iEnabled != 0;
            return res;
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status);
        public int AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status)
        {
            return ovrAudio_AudioSceneIRGetStatus(sceneIR, out status);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_InitializeAudioSceneIRParameters(out MapParameters parameters);
        public int InitializeAudioSceneIRParameters(out MapParameters parameters)
        {
            return ovrAudio_InitializeAudioSceneIRParameters(out parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters);
        public int AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRCompute(sceneIR, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters);
        public int AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters)
        {
            return ovrAudio_AudioSceneIRComputeCustomPoints(sceneIR, points, pointCount, ref parameters);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount);
        public int AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount)
        {
            return ovrAudio_AudioSceneIRGetPointCount(sceneIR, out pointCount);
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount);
        public int AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount)
        {
            return ovrAudio_AudioSceneIRGetPoints(sceneIR, points, maxPointCount);
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_AudioSceneIRSetTransform(IntPtr sceneIR, float* matrix4x4);
        public int AudioSceneIRSetTransform(IntPtr sceneIR, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;

                return ovrAudio_AudioSceneIRSetTransform(sceneIR, nativeMatrixCopy);
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4);
        public int AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4)
        {
            return ovrAudio_AudioSceneIRGetTransform(sceneIR, out matrix4x4);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRWriteFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRWriteFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRWriteFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadFile(IntPtr sceneIR, string filePath);
        public int AudioSceneIRReadFile(IntPtr sceneIR, string filePath)
        {
            return ovrAudio_AudioSceneIRReadFile(sceneIR, filePath);
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength);
        public int AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength)
        {
            return ovrAudio_AudioSceneIRReadMemory(sceneIR, data, dataLength);
        }

        /***********************************************************************************/
        // Control Zone API
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlZone(IntPtr context, out IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_CreateControlVolume(IntPtr context, out IntPtr control);
        public int CreateControlZone(out IntPtr control)
        {
            try
            {
                return ovrAudio_CreateControlZone(context, out control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_CreateControlVolume(context, out control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlZone(IntPtr control);
        [DllImport(binaryName)]
        private static extern int ovrAudio_DestroyControlVolume(IntPtr control);
        public int DestroyControlZone(IntPtr control)
        {
            try
            {
                return ovrAudio_DestroyControlZone(control);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_DestroyControlVolume(control);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetEnabled(IntPtr control, int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetEnabled(IntPtr control, int enabled);
        public int ControlZoneSetEnabled(IntPtr control, bool enabled)
        {
            try
            {
                return ovrAudio_ControlZoneSetEnabled(control, enabled ? 1 : 0);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetEnabled(control, enabled ? 1 : 0);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetEnabled(IntPtr control, out int enabled);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetEnabled(IntPtr control, out int enabled);
        public int ControlZoneGetEnabled(IntPtr control, out bool enabled)
        {
            int enabledInt = 0;
            int result;

            try
            {
                result = ovrAudio_ControlZoneGetEnabled(control, out enabledInt);
            }
            catch
            {
                // Hack for v60 compatibility
                result = ovrAudio_ControlVolumeGetEnabled(control, out enabledInt);
            }
            enabled = enabledInt != 0;
            return result;
        }

        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlZoneSetTransform(IntPtr control, float* matrix4x4);
        [DllImport(binaryName)]
        private static extern unsafe int ovrAudio_ControlVolumeSetTransform(IntPtr control, float* matrix4x4);
        public int ControlZoneSetTransform(IntPtr control, in Matrix4x4 matrix)
        {
            unsafe
            {
                float* nativeMatrixCopy = stackalloc float[16];

                // Note: flip Z to convert from left-handed (+Z forward) to right-handed (+Z backward)
                nativeMatrixCopy[0] = matrix.m00;
                nativeMatrixCopy[1] = matrix.m10;
                nativeMatrixCopy[2] = -matrix.m20;
                nativeMatrixCopy[3] = matrix.m30;
                nativeMatrixCopy[4] = matrix.m01;
                nativeMatrixCopy[5] = matrix.m11;
                nativeMatrixCopy[6] = -matrix.m21;
                nativeMatrixCopy[7] = matrix.m31;
                nativeMatrixCopy[8] = matrix.m02;
                nativeMatrixCopy[9] = matrix.m12;
                nativeMatrixCopy[10] = -matrix.m22;
                nativeMatrixCopy[11] = matrix.m32;
                nativeMatrixCopy[12] = matrix.m03;
                nativeMatrixCopy[13] = matrix.m13;
                nativeMatrixCopy[14] = -matrix.m23;
                nativeMatrixCopy[15] = matrix.m33;
                try
                {
                    return ovrAudio_ControlZoneSetTransform(control, nativeMatrixCopy);
                }
                catch
                {
                    // Hack for v60 compatibility
                    return ovrAudio_ControlVolumeSetTransform(control, nativeMatrixCopy);
                }
            }
        }

        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetTransform(IntPtr control, out float[] matrix4x4);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetTransform(IntPtr control, out float[] matrix4x4);
        public int ControlZoneGetTransform(IntPtr control, out float[] matrix4x4)
        {
            try
            {
                return ovrAudio_ControlZoneGetTransform(control, out matrix4x4);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetTransform(control, out matrix4x4);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ);
        public int ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetBox(control, sizeX, sizeY, sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetBox(control, sizeX, sizeY, sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ);
        public int ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetBox(control, out sizeX, out sizeY, out sizeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ);
        public int ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFadeDistance(control, fadeX, fadeY, fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ);
        public int ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ)
        {
            try
            {
                return ovrAudio_ControlZoneGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeGetFadeDistance(control, out fadeX, out fadeY, out fadeZ);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value);
        public int ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value)
        {
            try
            {
                return ovrAudio_ControlZoneSetFrequency(control, property, frequency, value);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeSetFrequency(control, property, frequency, value);
            }
        }
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlZoneReset(IntPtr control, ControlZoneProperty property);
        [DllImport(binaryName)]
        private static extern int ovrAudio_ControlVolumeReset(IntPtr control, ControlZoneProperty property);
        public int ControlZoneReset(IntPtr control, ControlZoneProperty property)
        {
            try
            {
                return ovrAudio_ControlZoneReset(control, property);
            }
            catch
            {
                // Hack for v60 compatibility
                return ovrAudio_ControlVolumeReset(control, property);
            }
        }
    }
    public class DummyInterface : INativeInterface
    {
        /***********************************************************************************/
        // Settings API
        public int SetAcousticModel(AcousticModel model) => -1;
        public int SetEnabled(int feature, bool enabled) => -1;
        public int SetEnabled(EnableFlagInternal feature, bool enabled) => -1;

        /***********************************************************************************/
        // Geometry API
        public int CreateAudioGeometry(out IntPtr geometry) { geometry = IntPtr.Zero; return -1; }
        public int DestroyAudioGeometry(IntPtr geometry) => -1;
        public int AudioGeometrySetObjectFlag(IntPtr geometry, ObjectFlags flag, bool enabled) => -1;
        public int AudioGeometryUploadMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount) => -1;
        public int AudioGeometryUploadSimplifiedMeshArrays(IntPtr geometry,
                                                        float[] vertices, int vertexCount,
                                                        int[] indices, int indexCount,
                                                        MeshGroup[] groups, int groupCount,
                                                        ref MeshSimplification simplification) => -1;
        public int AudioGeometrySetTransform(IntPtr geometry, in Matrix4x4 matrix) => -1;
        public int AudioGeometryGetTransform(IntPtr geometry, out float[] matrix4x4) { matrix4x4 = null; return -1; }
        public int AudioGeometryWriteMeshFile(IntPtr geometry, string filePath) => -1;
        public int AudioGeometryReadMeshFile(IntPtr geometry, string filePath) => -1;
        public int AudioGeometryReadMeshMemory(IntPtr geometry, IntPtr data, UInt64 dataLength) => -1;
        public int AudioGeometryWriteMeshFileObj(IntPtr geometry, string filePath) => -1;

        public int AudioGeometryGetSimplifiedMesh(IntPtr geometry, out float[] vertices, out uint[] indices, out uint[] materialIndices) { vertices = null; indices = null; materialIndices = null; return -1; }

        /***********************************************************************************/
        // Material API
        public int AudioMaterialGetFrequency(IntPtr material, MaterialProperty property, float frequency, out float value) { value = 0.0f; return -1; }
        public int CreateAudioMaterial(out IntPtr material) { material = IntPtr.Zero; return -1; }
        public int DestroyAudioMaterial(IntPtr material) => -1;
        public int AudioMaterialSetFrequency(IntPtr material, MaterialProperty property, float frequency, float value) => -1;
        public int AudioMaterialReset(IntPtr material, MaterialProperty property) => -1;
        /***********************************************************************************/
        // Acoustic Map API
        public int CreateAudioSceneIR(out IntPtr sceneIR) { sceneIR = IntPtr.Zero; return -1; }
        public int DestroyAudioSceneIR(IntPtr sceneIR) => -1;
        public int AudioSceneIRSetEnabled(IntPtr sceneIR, bool enabled) => -1;
        public int AudioSceneIRGetEnabled(IntPtr sceneIR, out bool enabled) { enabled = false; return -1; }
        public int AudioSceneIRGetStatus(IntPtr sceneIR, out AcousticMapStatus status) { status = AcousticMapStatus.EMPTY; return -1; }
        public int InitializeAudioSceneIRParameters(out MapParameters parameters) { parameters = new MapParameters(); return -1; }
        public int AudioSceneIRCompute(IntPtr sceneIR, ref MapParameters parameters) => -1;
        public int AudioSceneIRComputeCustomPoints(IntPtr sceneIR,
            float[] points, UIntPtr pointCount, ref MapParameters parameters) => -1;
        public int AudioSceneIRGetPointCount(IntPtr sceneIR, out UIntPtr pointCount) { pointCount = UIntPtr.Zero; return -1; }
        public int AudioSceneIRGetPoints(IntPtr sceneIR, float[] points, UIntPtr maxPointCount) => -1;
        public int AudioSceneIRSetTransform(IntPtr sceneIR, in Matrix4x4 matrix) => -1;
        public int AudioSceneIRGetTransform(IntPtr sceneIR, out float[] matrix4x4) { matrix4x4 = new float[16]; return -1; }
        public int AudioSceneIRWriteFile(IntPtr sceneIR, string filePath) => -1;
        public int AudioSceneIRReadFile(IntPtr sceneIR, string filePath) => -1;
        public int AudioSceneIRReadMemory(IntPtr sceneIR, IntPtr data, UInt64 dataLength) => -1;

        /***********************************************************************************/
        // Control Zone API
        public int CreateControlZone(out IntPtr control) { control = IntPtr.Zero; return -1; }
        public int DestroyControlZone(IntPtr control) => -1;
        public int ControlZoneSetEnabled(IntPtr control, bool enabled) => -1;
        public int ControlZoneGetEnabled(IntPtr control, out bool enabled) { enabled = false; return -1; }
        public int ControlZoneSetTransform(IntPtr control, in Matrix4x4 matrix) => -1;
        public int ControlZoneGetTransform(IntPtr control, out float[] matrix4x4) { matrix4x4 = new float[16]; return -1; }
        public int ControlZoneSetBox(IntPtr control, float sizeX, float sizeY, float sizeZ) => -1;
        public int ControlZoneGetBox(IntPtr control, out float sizeX, out float sizeY, out float sizeZ) { sizeX = 0.0f; sizeY = 0.0f; sizeZ = 0.0f; return -1; }
        public int ControlZoneSetFadeDistance(IntPtr control, float fadeX, float fadeY, float fadeZ) => -1;
        public int ControlZoneGetFadeDistance(IntPtr control, out float fadeX, out float fadeY, out float fadeZ) { fadeX = 0.0f; fadeY = 0.0f; fadeZ = 0.0f; return -1; }
        public int ControlZoneSetFrequency(IntPtr control, ControlZoneProperty property, float frequency, float value) => -1;
        public int ControlZoneReset(IntPtr control, ControlZoneProperty property) => -1;
    }
}
