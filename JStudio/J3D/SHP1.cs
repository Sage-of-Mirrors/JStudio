using GameFormatReader.Common;
using JStudio.OpenGL;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindEditor;
using System.Runtime.InteropServices;
using JStudio.Framework;

namespace JStudio.J3D
{
    public class ShapeVertexAttribute
    {
        public VertexArrayType ArrayType;
        public VertexDataType DataType;

        public ShapeVertexAttribute(VertexArrayType arrayType, VertexDataType dataType)
        {
            ArrayType = arrayType;
            DataType = dataType;
        }

        public override string ToString()
        {
            return string.Format("ArrayType: {0} DataType: {1}", ArrayType, DataType);
        }
    }

    public class SkinDataTable
    {
        public int Unknown0 { get; private set; }
        public List<ushort> MatrixTable { get; private set; }

        public SkinDataTable(int unknown0)
        {
            Unknown0 = unknown0;
            MatrixTable = new List<ushort>();
        }
    }

    public class Shape : IDisposable
    {
        private bool m_hasBeenDisposed = false;

        public byte MatrixType { get; set; }
        public float BoundingSphereDiameter { get; set; }
        public FAABox BoundingBox { get; set; }

        public VertexDescription VertexDescription { get; private set; }
        public Material ShapeMaterial { get; set; }

        public MeshVertexHolder VertexData { get; private set; }
        public List<int> Indices { get; private set; }

        public List<MatrixGroup> MatrixGroups { get; set; }

        public Shape()
        {
            MatrixGroups = new List<MatrixGroup>();

            VertexDescription = new VertexDescription();
            ShapeMaterial = null;

            VertexData = new MeshVertexHolder();
            Indices = new List<int>();
        }

        public DrawCall GenerateDrawCall()
        {
            return new DrawCall(VertexDescription, ShapeMaterial);
        }

        public void UploadBuffersToGPU()
        {
            VertexDescription.IndexCount = Indices.Count;

            // Upload the Indexes
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, VertexDescription.IndexBufferId);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(4 * Indices.Count), Indices.ToArray(), BufferUsageHint.StaticDraw);

            if (VertexData.Position.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Position, VertexData.Position.ToArray());
            if (VertexData.Normal.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Normal, VertexData.Normal.ToArray());
            if (VertexData.Color0.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Color0, VertexData.Color0.ToArray());
            if (VertexData.Color1.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Color1, VertexData.Color1.ToArray());
            if (VertexData.Tex0.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex0, VertexData.Tex0.ToArray());
            if (VertexData.Tex1.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex1, VertexData.Tex1.ToArray());
            if (VertexData.Tex2.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex2, VertexData.Tex2.ToArray());
            if (VertexData.Tex3.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex3, VertexData.Tex3.ToArray());
            if (VertexData.Tex4.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex4, VertexData.Tex4.ToArray());
            if (VertexData.Tex5.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex5, VertexData.Tex5.ToArray());
            if (VertexData.Tex6.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex6, VertexData.Tex6.ToArray());
            if (VertexData.Tex7.Count > 0) UploadBufferToGPU(ShaderAttributeIds.Tex7, VertexData.Tex7.ToArray());
            if (VertexData.SkinIndices.Count > 0) UploadBufferToGPU(ShaderAttributeIds.SkinIndices, VertexData.SkinIndices.ToArray());
            if (VertexData.SkinWeights.Count > 0) UploadBufferToGPU(ShaderAttributeIds.SkinWeights, VertexData.SkinWeights.ToArray());
        }

        private void UploadBufferToGPU<T>(ShaderAttributeIds attribute, T[] data) where T : struct
        {
            // See if this attribute is already enabled. If it's not already enabled, we need to generate a buffer for it.
            if (!VertexDescription.AttributeIsEnabled(attribute))
            {
                VertexDescription.EnableAttribute(attribute);
            }

            // Bind the buffer before updating the data.
            GL.BindBuffer(BufferTarget.ArrayBuffer, VertexDescription.GetAttributeBufferId(attribute));

            // Finally, update the data.
            int stride = VertexDescription.GetStride(attribute);
            GL.BufferData<T>(BufferTarget.ArrayBuffer, (IntPtr)(data.Length * stride), data, BufferUsageHint.StaticDraw);
        }

        #region IDisposable Support
        ~Shape()
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

                // Set large fields to null.
                VertexData = null;

                VertexDescription.Dispose();
                VertexDescription = null;

                Indices = null;

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

    public struct MatrixGroup
    {
        public List<MeshVertexIndex> Indices;

        // This is a list of all Matrix Table entries for all sub-primitives. 
        public SkinDataTable MatrixDataTable;

        public MatrixGroup(List<MeshVertexIndex> empty_list, SkinDataTable default_table)
        {
            Indices = empty_list;
            MatrixDataTable = default_table;
        }
    }

    public class SHP1
    {
        public List<Shape> Shapes { get; private set; }

        public SHP1()
        {
            Shapes = new List<Shape>();
        }

        public void ReadSHP1FromStream(EndianBinaryReader reader, long tagStart)
        {
            #region Load data from SHP1 header
            short shapeCount = reader.ReadInt16();
            Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding
            int shapeOffset = reader.ReadInt32();

            // Another index remap table.
            int remapTableOffset = reader.ReadInt32();

            Trace.Assert(reader.ReadInt32() == 0);
            int attributeOffset = reader.ReadInt32();
            
            // Offset to the Matrix Table which holds a list of ushorts used for ??
            int matrixTableOffset = reader.ReadInt32();

            // Offset to the array of primitive's data.
            int primitiveDataOffset = reader.ReadInt32();
            int matrixDataOffset = reader.ReadInt32();
            int packetLocationOffset = reader.ReadInt32();
            #endregion

            for (int s = 0; s < shapeCount; s++)
            {
                // Shapes can have different attributes for each shape. (ie: Some have only Position, while others have Pos & TexCoord, etc.) Each 
                // shape (which has a consistent number of attributes) it is split into individual packets, which are a collection of geometric primitives.
                // Each packet can have individual unique skinning data.

                reader.BaseStream.Position = tagStart + shapeOffset + (0x28 * s); /* 0x28 is the size of one Shape entry*/

                Shape shape = new Shape();

                #region Load data from shape struct
                shape.MatrixType = reader.ReadByte();
                Trace.Assert(reader.ReadByte() == 0xFF); // Padding

                // Number of Packets (of data) contained in this Shape
                ushort grp_count = reader.ReadUInt16();

                // Offset from the start of the Attribute List to the attributes this particular batch uses.
                ushort batchAttributeOffset = reader.ReadUInt16();

                ushort firstMatrixIndex = reader.ReadUInt16();
                ushort firstGrpIndex = reader.ReadUInt16();
                Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding

                float boundingSphereDiameter = reader.ReadSingle();
                Vector3 bboxMin = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 bboxMax = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                shape.BoundingSphereDiameter = boundingSphereDiameter;
                shape.BoundingBox = new FAABox(bboxMin, bboxMax);
                #endregion

                Shapes.Add(shape);

                // Determine which Attributes this particular shape uses.
                reader.BaseStream.Position = tagStart + attributeOffset + batchAttributeOffset;

                List<ShapeVertexAttribute> attributes = new List<ShapeVertexAttribute>();

                while (true)
                {
                    ShapeVertexAttribute attribute = new ShapeVertexAttribute((VertexArrayType)reader.ReadInt32(), (VertexDataType)reader.ReadInt32());

                    if (attribute.ArrayType == VertexArrayType.NullAttr)
                    {
                        break;
                    }

                    attributes.Add(attribute);

                    // We'll enable the attributes for the shape here, but we'll skip PositionMatrixIndex.
                    // We're going to use our own attributes for skinning - SkinIndices and SkinWeights.
                    if (attribute.ArrayType != VertexArrayType.PositionMatrixIndex)
                    {
                        shape.VertexDescription.EnableAttribute(ArrayTypeToShader(attribute.ArrayType));
                    }
                }

                for (ushort p = 0; p < grp_count; p++)
                {
                    MatrixGroup grp = new MatrixGroup(new List<MeshVertexIndex>(), new SkinDataTable(0));

                    // The packets are all stored linearly and then they point to the specific size and offset of the data for this particular packet.
                    reader.BaseStream.Position = tagStart + packetLocationOffset + ((firstGrpIndex + p) * 0x8); /* 0x8 is the size of one Packet entry */

                    int packetSize = reader.ReadInt32();
                    int packetOffset = reader.ReadInt32();

                    // Read Matrix Data for Packet
                    reader.BaseStream.Position = tagStart + matrixDataOffset + (firstMatrixIndex + p) * 0x08; /* 0x8 is the size of one Matrix Data */
                    ushort matrixUnknown0 = reader.ReadUInt16();
                    ushort matrixCount = reader.ReadUInt16();
                    uint matrixFirstIndex = reader.ReadUInt32();

                    SkinDataTable matrixData = new SkinDataTable(matrixUnknown0);
                    grp.MatrixDataTable = matrixData;

                    // Read Matrix Table data. The Matrix Table is skinning information for the packet which indexes into the DRW1 section for more info.
                    reader.BaseStream.Position = tagStart + matrixTableOffset + (matrixFirstIndex * 0x2); /* 0x2 is the size of one Matrix Table entry */
                    for (int m = 0; m < matrixCount; m++)
                        matrixData.MatrixTable.Add(reader.ReadUInt16());

                    // Read the Primitive Data
                    reader.BaseStream.Position = tagStart + primitiveDataOffset + packetOffset;

                    uint numPrimitiveBytesRead = 0;
                    while(numPrimitiveBytesRead < packetSize)
                    {
                        // The game pads the chunk out with zeros, so if there's a primitive with type zero (invalid) then we early out of the loop.
                        GXPrimitiveType type = (GXPrimitiveType)reader.ReadByte();
                        if (type == 0 || numPrimitiveBytesRead >= packetSize)
                            break;

                        // The number of vertices this primitive has indexes for
                        ushort vertexCount = reader.ReadUInt16();
                        numPrimitiveBytesRead += 0x3; // 2 bytes for vertex count, one byte for GXPrimitiveType.

                        List<MeshVertexIndex> primitiveVertices = new List<MeshVertexIndex>();

                        for(int v = 0; v < vertexCount; v++)
                        {
                            MeshVertexIndex newVert = new MeshVertexIndex();
                            primitiveVertices.Add(newVert);

                            // Each vertex has an index for each ShapeAttribute specified by the Shape that we belong to. So we'll loop through
                            // each index and load it appropriately (as vertices can have different data sizes).
                            foreach (ShapeVertexAttribute curAttribute in attributes)
                            {
                                int index = 0;
                                uint numBytesRead = 0;

                                switch (curAttribute.DataType)
                                {
                                    case VertexDataType.Unsigned8:
                                    case VertexDataType.Signed8:
                                        index = reader.ReadByte();
                                        numBytesRead = 1;
                                        break;
                                    case VertexDataType.Unsigned16:
                                    case VertexDataType.Signed16:
                                        index = reader.ReadUInt16();
                                        numBytesRead = 2;
                                        break;
                                    case VertexDataType.Float32:
                                    case VertexDataType.None:
                                    default:
                                        System.Console.WriteLine("Unknown Data Type {0} for ShapeAttribute!", curAttribute.DataType);
                                        break;
                                }

                                // We now have the index into the datatype this array points to. We can now inspect the array type of the 
                                // attribute to get the value out of the correct source array.
                                switch (curAttribute.ArrayType)
                                {
                                    case VertexArrayType.Position: newVert.Position = index; break;
                                    case VertexArrayType.PositionMatrixIndex: newVert.PosMtxIndex = index / 3; break;
                                    case VertexArrayType.Normal: newVert.Normal = index; break;
                                    case VertexArrayType.Color0: newVert.Color0 = index; break;
                                    case VertexArrayType.Color1: newVert.Color1 = index; break;
                                    case VertexArrayType.Tex0:  newVert.Tex0 = index; break;
                                    case VertexArrayType.Tex1:  newVert.Tex1 = index; break;
                                    case VertexArrayType.Tex2:  newVert.Tex2 = index; break;
                                    case VertexArrayType.Tex3:  newVert.Tex3 = index; break;
                                    case VertexArrayType.Tex4:  newVert.Tex4 = index; break;
                                    case VertexArrayType.Tex5:  newVert.Tex5 = index; break;
                                    case VertexArrayType.Tex6:  newVert.Tex6 = index; break;
                                    case VertexArrayType.Tex7:  newVert.Tex7 = index; break;
                                    default:
                                        System.Console.WriteLine("Unsupported ArrayType {0} for ShapeAttribute!", curAttribute.ArrayType);
                                        break;
                                }

                                numPrimitiveBytesRead += numBytesRead;
                            }
                        }

                        // All vertices have now been loaded into the primitiveIndexes array. We can now convert them if needed
                        // to triangle lists, instead of triangle fans, strips, etc.
                        grp.Indices.AddRange(ConvertTopologyToTriangles(type, primitiveVertices));
                    }

                    shape.MatrixGroups.Add(grp);
                }
            }

            FixGroupSkinningIndices();
        }

        private ShaderAttributeIds ArrayTypeToShader(VertexArrayType type)
        {
            switch (type)
            {
                case VertexArrayType.Position:
                    return ShaderAttributeIds.Position;
                case VertexArrayType.Normal:
                    return ShaderAttributeIds.Normal;
                case VertexArrayType.Color0:
                    return ShaderAttributeIds.Color0;
                case VertexArrayType.Color1:
                    return ShaderAttributeIds.Color1;
                case VertexArrayType.Tex0:
                    return ShaderAttributeIds.Tex0;
                case VertexArrayType.Tex1:
                    return ShaderAttributeIds.Tex1;
                case VertexArrayType.Tex2:
                    return ShaderAttributeIds.Tex2;
                case VertexArrayType.Tex3:
                    return ShaderAttributeIds.Tex3;
                case VertexArrayType.Tex4:
                    return ShaderAttributeIds.Tex4;
                case VertexArrayType.Tex5:
                    return ShaderAttributeIds.Tex5;
                case VertexArrayType.Tex6:
                    return ShaderAttributeIds.Tex6;
                case VertexArrayType.Tex7:
                    return ShaderAttributeIds.Tex7;
                default:
                    return ShaderAttributeIds.Position;
            }
        }

        /// <summary>
        /// Replaces skinning indices of 65535 (ushort.max) with the proper indices from previous groups.
        /// </summary>
        private void FixGroupSkinningIndices()
        {
            foreach (Shape s in Shapes)
            {
                for (int i = 0; i < s.MatrixGroups.Count; i++)
                {
                    MatrixGroup cur_group = s.MatrixGroups[i];

                    for (int j = 0; j < cur_group.MatrixDataTable.MatrixTable.Count; j++)
                    {
                        ushort cur_index = cur_group.MatrixDataTable.MatrixTable[j];

                        if (cur_index == ushort.MaxValue)
                        {
                            for (int k = i - 1; k > -1; k--)
                            {
                                ushort last_index = s.MatrixGroups[k].MatrixDataTable.MatrixTable[j];

                                if (last_index != ushort.MaxValue)
                                {
                                    cur_group.MatrixDataTable.MatrixTable[j] = last_index;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public List<MeshVertexIndex> ConvertTopologyToTriangles(GXPrimitiveType fromType, List<MeshVertexIndex> indexes)
        {
            List<MeshVertexIndex> sortedIndexes = new List<MeshVertexIndex>();
            if(fromType == GXPrimitiveType.TriangleStrip)
            {
                for (int v = 2; v < indexes.Count; v++)
                {
                    bool isEven = v % 2 != 0;
                    MeshVertexIndex[] newTri = new MeshVertexIndex[3];

                    newTri[0] = indexes[v - 2];
                    newTri[1] = isEven ? indexes[v] : indexes[v - 1];
                    newTri[2] = isEven ? indexes[v - 1] : indexes[v];

                    // Check against degenerate triangles (a triangle which shares indexes)
                    if (newTri[0] != newTri[1] && newTri[1] != newTri[2] && newTri[2] != newTri[0])
                        sortedIndexes.AddRange(newTri);
                    else
                        System.Console.WriteLine("Degenerate triangle detected, skipping TriangleStrip conversion to triangle.");
                }
            }
            else if(fromType == GXPrimitiveType.TriangleFan)
            {
                for(int v = 1; v < indexes.Count-1; v++)
                {
                    // Triangle is always, v, v+1, and index[0]?
                    MeshVertexIndex[] newTri = new MeshVertexIndex[3];
                    newTri[0] = indexes[v];
                    newTri[1] = indexes[v + 1];
                    newTri[2] = indexes[0];

                    // Check against degenerate triangles (a triangle which shares indexes)
                    if (newTri[0] != newTri[1] && newTri[1] != newTri[2] && newTri[2] != newTri[0])
                        sortedIndexes.AddRange(newTri);
                    else
                        System.Console.WriteLine("Degenerate triangle detected, skipping TriangleFan conversion to triangle.");
                }
            }
            else if(fromType == GXPrimitiveType.Triangles)
            {
                // The good news is, Triangles just go straight though!
                sortedIndexes.AddRange(indexes);
            }
            else
            {
                System.Console.WriteLine("Unsupported GXPrimitiveType: {0} in conversion to Triangle List.", fromType);
            }

            return sortedIndexes;
        }

        public void LinkData(VTX1 vertex_data, DRW1 draw_matrix_data, EVP1 envelope_data)
        {
            LinkVertexData(vertex_data);
            LinkSkinningData(draw_matrix_data, envelope_data);
        }

        private void LinkVertexData(VTX1 vertex_data)
        {
            foreach (Shape shape in Shapes)
            {
                int vertex_index = 0;

                foreach (MatrixGroup group in shape.MatrixGroups)
                {
                    foreach (MeshVertexIndex vertex in group.Indices)
                    {
                        shape.Indices.Add(vertex_index++);

                        if (vertex.Position >= 0) shape.VertexData.Position.Add(vertex_data.VertexData.Position[vertex.Position]);
                        if (vertex.Normal >= 0) shape.VertexData.Normal.Add(vertex_data.VertexData.Normal[vertex.Normal]);
                        if (vertex.Binormal >= 0) shape.VertexData.Binormal.Add(vertex_data.VertexData.Binormal[vertex.Binormal]);
                        if (vertex.Color0 >= 0) shape.VertexData.Color0.Add(vertex_data.VertexData.Color0[vertex.Color0]);
                        if (vertex.Color1 >= 0) shape.VertexData.Color1.Add(vertex_data.VertexData.Color1[vertex.Color1]);
                        if (vertex.Tex0 >= 0) shape.VertexData.Tex0.Add(vertex_data.VertexData.Tex0[vertex.Tex0]);
                        if (vertex.Tex1 >= 0) shape.VertexData.Tex1.Add(vertex_data.VertexData.Tex1[vertex.Tex1]);
                        if (vertex.Tex2 >= 0) shape.VertexData.Tex2.Add(vertex_data.VertexData.Tex2[vertex.Tex2]);
                        if (vertex.Tex3 >= 0) shape.VertexData.Tex3.Add(vertex_data.VertexData.Tex3[vertex.Tex3]);
                        if (vertex.Tex4 >= 0) shape.VertexData.Tex4.Add(vertex_data.VertexData.Tex4[vertex.Tex4]);
                        if (vertex.Tex5 >= 0) shape.VertexData.Tex5.Add(vertex_data.VertexData.Tex5[vertex.Tex5]);
                        if (vertex.Tex6 >= 0) shape.VertexData.Tex6.Add(vertex_data.VertexData.Tex6[vertex.Tex6]);
                        if (vertex.Tex7 >= 0) shape.VertexData.Tex7.Add(vertex_data.VertexData.Tex7[vertex.Tex7]);
                    }
                }
            }
        }

        private void LinkSkinningData(DRW1 draw_matrix_data, EVP1 envelope_data)
        {
            foreach (Shape shape in Shapes)
            {
                shape.VertexDescription.EnableAttribute(ShaderAttributeIds.SkinIndices);
                shape.VertexDescription.EnableAttribute(ShaderAttributeIds.SkinWeights);

                foreach (MatrixGroup group in shape.MatrixGroups)
                {
                    foreach (MeshVertexIndex vertex in group.Indices)
                    {
                        int draw_mat_index = vertex.PosMtxIndex >= 0 ? group.MatrixDataTable.MatrixTable[vertex.PosMtxIndex] : 0;

                        if (draw_matrix_data.IsPartiallyWeighted[draw_mat_index])
                        {
                            EVP1.Envelope envelope = envelope_data.Envelopes[draw_matrix_data.TransformIndexTable[draw_mat_index]];
                            
                            // Because we're using vec4s for the skinning data, any vertex with more than 4 bones influencing it
                            // is invalid to us. We're going to assume this never happens, but in the event that it does, this assert
                            // will tell us.
                            Trace.Assert(envelope.NumBones <= 4);

                            Vector4 indices = new Vector4(0, 0, 0, 0);
                            Vector4 weights = new Vector4(0, 0, 0, 0);

                            for (int i = 0; i < envelope.NumBones; i++)
                            {
                                indices[i] = envelope.BoneIndexes[i];
                                weights[i] = envelope.BoneWeights[i];
                            }

                            shape.VertexData.SkinIndices.Add(indices);
                            shape.VertexData.SkinWeights.Add(weights);
                        }
                        else
                        {
                            shape.VertexData.SkinIndices.Add(new Vector4(draw_matrix_data.TransformIndexTable[draw_mat_index], -1, -1, -1));
                            shape.VertexData.SkinWeights.Add(new Vector4(1.0f, 0, 0, 0));
                        }
                    }
                }
            }
        }

        public void UploadShapesToGPU()
        {
            foreach (Shape shape in Shapes)
            {
                shape.UploadBuffersToGPU();
            }
        }
    }
}
