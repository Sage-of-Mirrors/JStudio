using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JStudio.OpenGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using JStudio.J3D;
using System.Runtime.InteropServices;
using WindEditor;

namespace JStudio.Framework
{
    public class DrawCall
    {
        public SortKey SortKey { get; set; }
        public VertexDescription VertexData { get; set; }
        public Material Material { get; set; }

        public int UBOId { get; set; }
        public int MBOId { get; set; }
        public IntPtr MaterialBlockOffset { get; set; }
        public IntPtr MatrixBlockOffset { get; set; }

        public DrawCall(VertexDescription vtx_info, Material material)
        {
            VertexData = vtx_info;
            Material = material;
        }

        public void GenerateMaterialBlock()
        {
            MaterialBlock block = UBOAllocator.AllocateSlice<MaterialBlock>(out IntPtr offset);
            MaterialBlockOffset = offset;

            block.AmbientColors = new Vector4[2] { LinearColorToVec4(Material.AmbientColors[0]), LinearColorToVec4(Material.AmbientColors[1]) };
            block.MaterialColors = new Vector4[2] { LinearColorToVec4(Material.MaterialColors[0]), LinearColorToVec4(Material.MaterialColors[1]) };
            block.KonstColors = new Vector4[4]
            {
                LinearColorToVec4(Material.KonstColors[0]),
                LinearColorToVec4(Material.KonstColors[1]),
                LinearColorToVec4(Material.KonstColors[2]),
                LinearColorToVec4(Material.KonstColors[3])
            };
            block.Colors = new Vector4[4]
            {
                LinearColorToVec4(Material.TevColorIndexes[0]),
                LinearColorToVec4(Material.TevColorIndexes[1]),
                LinearColorToVec4(Material.TevColorIndexes[2]),
                LinearColorToVec4(Material.TevColorIndexes[3])
            };

            block.TextureParams = new Vector4[8];
            block.TexMatrices = new Matrix4x3[10];
            block.IndirectTexMatrices = new Matrix4x2[3];

            block.LightParams = new GXLight[8];
            block.PostTexMtx = new Matrix4x3[20];

            // Upload!
            GL.BindBuffer(BufferTarget.UniformBuffer, UBOId);
            GL.BufferSubData(BufferTarget.UniformBuffer, MaterialBlockOffset, Marshal.SizeOf(typeof(MaterialBlock)), new MaterialBlock[] { block });
        }

        public void Draw()
        {
            BindBuffers();
            Material.Bind();

            GL.DrawElements(BeginMode.Triangles, VertexData.IndexCount, DrawElementsType.UnsignedInt, 0);

            UnbindBuffers();
        }

        private void BindBuffers()
        {
            GenerateMaterialBlock();

            foreach (ShaderAttributeIds id in VertexData.EnabledAttributes)
            {
                int buffer_id = VertexData.GetAttributeBufferId(id);

                GL.BindBuffer(BufferTarget.ArrayBuffer, buffer_id);
                GL.EnableVertexAttribArray((int)id);

                GL.VertexAttribPointer((int)id, VertexData.GetAttributeSize(id), VertexData.GetAttributePointerType(id), false, VertexData.GetStride(id), 0);
            }

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, VertexData.IndexBufferId);

            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 0, UBOId, MaterialBlockOffset, Marshal.SizeOf(typeof(MaterialBlock)));
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, MBOId, MatrixBlockOffset, Marshal.SizeOf(typeof(MatrixBlock)));
        }

        private void UnbindBuffers()
        {
            foreach (ShaderAttributeIds id in VertexData.EnabledAttributes)
            {
                GL.DisableVertexAttribArray((int)id);
            }
        }

        private Vector4 LinearColorToVec4(WLinearColor color)
        {
            return new Vector4(color.R, color.G, color.B, color.A).Normalized();
        }
    }
}
