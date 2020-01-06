using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace JStudio.Framework
{
    public static class UBOAllocator
    {
        private static IntPtr m_BufferCursor;

        static UBOAllocator()
        {
            Reset();
        }

        public static T AllocateSlice<T>(out IntPtr slice_offset) where T : new()
        {
            slice_offset = m_BufferCursor;
            m_BufferCursor += Marshal.SizeOf(typeof(T));

            return new T();
        }

        public static void Reset()
        {
            m_BufferCursor = IntPtr.Zero;
        }
    }
}
