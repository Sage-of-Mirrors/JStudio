using JStudio.OpenGL;
using OpenTK.Graphics.OpenGL;
using System;
using System.IO;
using System.Text;

namespace JStudio.J3D.ShaderGen
{
    public static class TEVShaderGenerator
    {
        private static bool m_allowCachedOverride = false;

        public static Shader GenerateShader(Material fromMat, MAT3 data, bool dumpShaders)
        {
            Directory.CreateDirectory("ShaderDump");

            Shader shader = new Shader(fromMat.Name);
            bool success = false;
            {
                // Load it from the shader dump if it already exists, which allows us to hand-modify shaders.
                string filenameHash = string.Format("ShaderDump/{0}.vert", fromMat.Name);

                bool loadedFromDisk = File.Exists(filenameHash) && m_allowCachedOverride;
                string vertexShader = loadedFromDisk ? File.ReadAllText(filenameHash) : VertexShaderGen.GenerateVertexShader(fromMat);
                
                success = shader.CompileSource(vertexShader, ShaderType.VertexShader);

                if(!loadedFromDisk && dumpShaders)
                    File.WriteAllText(filenameHash, vertexShader);
            }
            if (success)
            {
                // Load it from the shader dump if it already exists, which allows us to hand-modify shaders.
                string filenameHash = string.Format("ShaderDump/{0}.frag", fromMat.Name);

                bool loadedFromDisk = File.Exists(filenameHash) && m_allowCachedOverride;
                string fragmentShader =  loadedFromDisk ? File.ReadAllText(filenameHash) : FragmentShaderGen.GenerateFragmentShader(fromMat, data);

                success = shader.CompileSource(fragmentShader, ShaderType.FragmentShader);

                if(!loadedFromDisk && dumpShaders)
                    File.WriteAllText(filenameHash, fragmentShader);
            }

            if (success)
                // Well, we compiled both the Vertex and the Fragment shader succesfully, let's try to link them together now.
                success = shader.LinkShader();

            //success = false;
            if (!success)
            {
                Console.WriteLine("Failed to generate shader for material: {0}", fromMat.Name);
                shader.Dispose();

                // Generate a Fallback Shader for rendering
                shader = new Shader("Debug_NormalColors");
                shader.CompileSource(File.ReadAllText("resources/shaders/Debug_NormalColors.vert"), ShaderType.VertexShader);
                shader.CompileSource(File.ReadAllText("resources/shaders/Debug_NormalColors.frag"), ShaderType.FragmentShader);
                shader.LinkShader();

                return shader;
            }

            return shader;
        }

        public static void GenerateCommonCode(StringBuilder stream, Material mat)
        {
            // Shader Header
            stream.AppendLine("// Automatically Generated File. All changes will be lost.");
            stream.AppendLine("// Special thanks to Jasper for all shader gen code. Based on the implementation for https://noclip.website/.");
            stream.AppendLine("#version 330 core");
            stream.AppendLine();

            stream.AppendLine("precision mediump float;\n");

            stream.AppendLine("layout(row_major, std140) uniform MatrixBlock {");
            stream.AppendLine("\tmat4x4 ProjectionMatrix;");
            stream.AppendLine("\tmat4x3 ViewMatrix;");
            stream.AppendLine("\tmat4x3 BoneMatrices[128];");
            stream.AppendLine("};\n");

            stream.AppendLine("struct Light {");
            stream.AppendLine("\tvec4 Position;");
            stream.AppendLine("\tvec4 Direction;");
            stream.AppendLine("\tvec4 Color;");
            stream.AppendLine("\tvec4 CosAtten;");
            stream.AppendLine("\tvec4 DistAtten;");
            stream.AppendLine("};\n");

            stream.AppendLine("layout(row_major, std140) uniform MaterialBlock {");
            stream.AppendLine("\tvec4 AmbientColors[2];");
            stream.AppendLine("\tvec4 MaterialColors[2];");
            stream.AppendLine("\tvec4 KonstColors[4];");
            stream.AppendLine("\tvec4 Colors[4];");
            stream.AppendLine("\tvec4 TextureParams[8];");
            stream.AppendLine("\tmat4x3 TexMatrices[10];");
            stream.AppendLine("\tmat4x2 IndirectTexMatrices[3];");

            if (mat.NumChannelControls > 0)
            {
                stream.AppendLine("\n\tLight LightParams[8];");
            }

            if (mat.PostTexMatrices.Length > 0)
            {
                stream.Append("\n\tmat4x3 PostTexMtx[20];");
            }

            stream.AppendLine("};\n");

            stream.AppendLine("uniform sampler2D Texture[8];");
            stream.AppendLine();
        }
    }
}
