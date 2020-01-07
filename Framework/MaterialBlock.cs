using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using JStudio.J3D;
using JStudio.OpenGL;

namespace JStudio.Framework
{
    [StructLayout(LayoutKind.Explicit, Size = 2560)]
    public struct MaterialBlock
    {
        // Colors
        [FieldOffset(0)] public Vector4[] AmbientColors; // 2 of these
        [FieldOffset(32)] public Vector4[] MaterialColors; // 2 of these
        [FieldOffset(64)] public Vector4[] KonstColors; // 4 of these
        [FieldOffset(128)] public Vector4[] Colors; // 4 of these

        // Texture parameters; X/Y scale, 0, LOD bias
        [FieldOffset(192)] public Vector4[] TextureParams; // 8 of these

        // Texture matrices
        [FieldOffset(320)] public Matrix4x3[] TexMatrices; // 10 slots for these
        [FieldOffset(800)] public Matrix4x2[] IndirectTexMatrices; // 3 slots for these

        // Optional stuff
        [FieldOffset(896)] public GXLight[] LightParams;
        [FieldOffset(1536)] public Matrix4x3[] PostTexMtx;
    }

    [StructLayout(LayoutKind.Explicit, Size = 6400)]
    public struct MatrixBlock
    {
        // Main matrices
        [FieldOffset(0)] public Matrix4x3 ProjectionMat;
        [FieldOffset(48)] public Matrix4x3 ViewMat;

        [FieldOffset(96)] public Matrix4x3[] BoneMatrices; // There's space for a max of 128 here
    }
}
