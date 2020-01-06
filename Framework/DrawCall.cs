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

namespace JStudio.Framework
{
    public class DrawCall
    {
        public SortKey SortKey { get; set; }
        public VertexDescription VertexData { get; set; }
        public Material Material { get; set; }
        public int UBOId { get; set; }
        public IntPtr UBOSliceOffset { get; set; }
        public IntPtr BonePaletteSliceOffset { get; set; }

        public DrawCall(VertexDescription vtx_info)
        {
            VertexData = vtx_info;
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
            foreach (ShaderAttributeIds id in VertexData.EnabledAttributes)
            {
                int buffer_id = VertexData.GetAttributeBufferId(id);

                GL.BindBuffer(BufferTarget.ArrayBuffer, buffer_id);
                GL.EnableVertexAttribArray((int)id);

                GL.VertexAttribPointer((int)id, VertexData.GetAttributeSize(id), VertexData.GetAttributePointerType(id), false, VertexData.GetStride(id), 0);
            }

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, VertexData.IndexBufferId);

            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 0, UBOId, UBOSliceOffset, Marshal.SizeOf(typeof(UniformBufferBlock)));
            GL.BindBufferRange(BufferRangeTarget.UniformBuffer, 1, UBOId, BonePaletteSliceOffset, Marshal.SizeOf(typeof(BonePaletteBlock)));
        }

        private void UnbindBuffers()
        {
            foreach (ShaderAttributeIds id in VertexData.EnabledAttributes)
            {
                GL.DisableVertexAttribArray((int)id);
            }
        }
    }
}
