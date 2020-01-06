using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JStudio.Framework
{
    public enum RenderType
    {
        Translucent = 0,
        Opaque = 1
    }

    public class SortKey
    {
        public uint Key
        {
            get
            {
                return (uint)((int)m_Type << 24 | m_Distance << 8 | m_Bias);
            }
        }

        private RenderType m_Type;
        private ushort m_Distance;
        private byte m_Bias;

        public SortKey()
        {
            SetLayer(RenderType.Opaque);
            SetDistance(0);
            SetBias(0);
        }

        public SortKey(RenderType layer, ushort dist, byte bias)
        {
            SetLayer(layer);
            SetDistance(dist);
            SetBias(bias);
        }

        public SortKey(RenderType layer, float dist, byte bias)
        {
            SetLayer(layer);
            SetDistance(dist);
            SetBias(bias);
        }

        public void SetLayer(RenderType layer)
        {
            m_Type = layer;
        }

        public void SetDistance(ushort dist)
        {
            m_Distance = dist;
        }

        public void SetDistance(float dist)
        {
            m_Distance = (ushort)MathUtil.Clamp(dist, 0, ushort.MaxValue);
        }

        public void SetBias(byte bias)
        {
            m_Bias = bias;
        }

        public override string ToString()
        {
            return $"{ Key } (Type: { m_Type.ToString() }, Distance: { m_Distance }, Bias: { m_Bias })";
        }
    }
}
