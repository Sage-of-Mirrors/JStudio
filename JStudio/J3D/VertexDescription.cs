using JStudio.OpenGL;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace JStudio.J3D
{
    public class VertexDescription : IDisposable
    {
        private int[] m_BufferIds;

        public List<ShaderAttributeIds> EnabledAttributes { get; set; }
        public int IndexBufferId { get; }
        public int IndexCount { get; }

        public VertexDescription()
        {
            EnabledAttributes = new List<ShaderAttributeIds>();

            m_BufferIds = new int[16];
            for (int i = 0; i < m_BufferIds.Length; i++)
            {
                m_BufferIds[i] = -1;
            }

            IndexBufferId = GL.GenBuffer();
        }

        public bool AttributeIsEnabled(ShaderAttributeIds attribute)
        {
            return EnabledAttributes.Contains(attribute);
        }

        public int GetAttributeSize(ShaderAttributeIds attribute)
        {
            switch (attribute)
            {
                case ShaderAttributeIds.Position:
                case ShaderAttributeIds.Normal:
                    return 3;
                case ShaderAttributeIds.Color0:
                case ShaderAttributeIds.Color1:
                    return 4;
                case ShaderAttributeIds.Tex0:
                case ShaderAttributeIds.Tex1:
                case ShaderAttributeIds.Tex2:
                case ShaderAttributeIds.Tex3:
                case ShaderAttributeIds.Tex4:
                case ShaderAttributeIds.Tex5:
                case ShaderAttributeIds.Tex6:
                case ShaderAttributeIds.Tex7:
                    return 2;
                case ShaderAttributeIds.PosMtxIndex:
                    return 1;
                default:
                    Console.WriteLine($"Unsupported attribute: {attribute} in GetAttributeSize!");
                    return 0;
            }
        }

        public VertexAttribPointerType GetAttributePointerType(ShaderAttributeIds attribute)
        {
            switch (attribute)
            {
                case ShaderAttributeIds.Position:
                case ShaderAttributeIds.Normal:
                case ShaderAttributeIds.Binormal:
                case ShaderAttributeIds.Color0:
                case ShaderAttributeIds.Color1:
                case ShaderAttributeIds.Tex0:
                case ShaderAttributeIds.Tex1:
                case ShaderAttributeIds.Tex2:
                case ShaderAttributeIds.Tex3:
                case ShaderAttributeIds.Tex4:
                case ShaderAttributeIds.Tex5:
                case ShaderAttributeIds.Tex6:
                case ShaderAttributeIds.Tex7:
                    return VertexAttribPointerType.Float;
                case ShaderAttributeIds.PosMtxIndex:
                    return VertexAttribPointerType.Int;

                default:
                    Console.WriteLine("Unsupported ShaderAttributeId: {0}", attribute);
                    return VertexAttribPointerType.Float;
            }
        }

        public int GetStride(ShaderAttributeIds attribute)
        {
            switch (attribute)
            {
                case ShaderAttributeIds.Position:
                case ShaderAttributeIds.Normal:
                case ShaderAttributeIds.Binormal:
                    return 4 * 3;
                case ShaderAttributeIds.Color0:
                case ShaderAttributeIds.Color1:
                    return 4 * 4;
                case ShaderAttributeIds.Tex0:
                case ShaderAttributeIds.Tex1:
                case ShaderAttributeIds.Tex2:
                case ShaderAttributeIds.Tex3:
                case ShaderAttributeIds.Tex4:
                case ShaderAttributeIds.Tex5:
                case ShaderAttributeIds.Tex6:
                case ShaderAttributeIds.Tex7:
                    return 4 * 2;
                case ShaderAttributeIds.PosMtxIndex:
                    return 4 * 1;
                default:
                    Console.WriteLine("Unsupported ShaderAttributeId: {0}", attribute);
                    return 0;
            }
        }

        public int GetAttributeBufferId(ShaderAttributeIds attribute)
        {
            if (!AttributeIsEnabled(attribute))
            {
                return -1;
            }

            return m_BufferIds[(int)attribute];
        }

        public void EnableAttribute(ShaderAttributeIds attribute)
        {
            m_BufferIds[(int)attribute] = GL.GenBuffer();
            EnabledAttributes.Add(attribute);
        }

        #region IDisposable Support
        private bool m_hasBeenDisposed;

        ~VertexDescription()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        protected virtual void Dispose(bool manualDispose)
        {
            if (!m_hasBeenDisposed)
            {
                if (manualDispose)
                {
                    // TODO: dispose managed state (managed objects).
                }

                GL.DeleteBuffer(IndexBufferId);

                for (int i = 0; i < m_BufferIds.Length; i++)
                    if (m_BufferIds[i] >= 0)
                        GL.DeleteBuffer(m_BufferIds[i]);

                m_hasBeenDisposed = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
