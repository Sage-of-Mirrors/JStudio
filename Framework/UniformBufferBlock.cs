using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK;

namespace JStudio.Framework
{
    [StructLayout(LayoutKind.Explicit, Size = 768)]
    public struct UniformBufferBlock
    {
        // Main matrices
        [FieldOffset(0)] public Matrix4x3 ProjectionMat;
        [FieldOffset(48)] public Matrix4x3 ViewMat;

        // Colors
        [FieldOffset(144)] public Vector4[] AmbientColors; // 2 of these
        [FieldOffset(176)] public Vector4[] MaterialColors; // 2 of these
        [FieldOffset(208)] public Vector4[] KonstColors; // 4 of these
        [FieldOffset(272)] public Vector4[] Colors; // 4 of these

        // Texture matrices
        [FieldOffset(336)] public Matrix4x3[] TexMatrices; // 10 slots for these
        [FieldOffset(576)] public Matrix4x2[] IndirectTexMatrices; // 3 slots for these
    }

    [StructLayout(LayoutKind.Explicit, Size = 6144)]
    public struct BonePaletteBlock
    {
        [FieldOffset(0)] public Matrix4x3[] BoneMatrices; // There's space for a max of 128 here
    }
}
