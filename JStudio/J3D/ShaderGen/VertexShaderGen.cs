using JStudio.OpenGL;
using System;
using System.Text;

namespace JStudio.J3D.ShaderGen
{
    public static class VertexShaderGen
    {
        public static string GenerateVertexShader(Material mat)
        {
            StringBuilder stream = new StringBuilder();

            TEVShaderGenerator.GenerateCommonCode(stream, mat);

            GenerateVertexAttributes(stream);

            stream.AppendLine("mat4x4 ApplySkin(int index) {");
            stream.AppendLine("\treturn BoneMatrices[uint(a_SkinIndices[index])] * a_SkinWeights[index];");
            stream.AppendLine("}\n");

            stream.AppendLine("float ApplyAttenuation(vec3 t_Coeff, float t_Value) {");
            stream.AppendLine("\treturn max(dot(t_Coeff, vec3(1.0, t_Value, t_Value*t_Value)), 0.0);");
            stream.AppendLine("}\n");

            GenerateVertexShaderMain(stream, mat);

            return stream.ToString();
        }

        private static void GenerateVertexAttributes(StringBuilder stream)
        {
            stream.AppendLine("// Per-Vertex Input");

            string[] attr_names = Enum.GetNames(typeof(ShaderAttributeIds));

            // Input attributes.
            for (int i = 0; i < attr_names.Length; i++)
            {
                stream.AppendLine($"layout(location = { i }) in { GetVertexAttributeType((ShaderAttributeIds)i) } a_{ attr_names[i] };");
            }

            stream.AppendLine();

            // Output attributes; stop before the skinning data, we don't need to output that.
            for (int i = 0; i < attr_names.Length - 2; i++)
            {
                stream.AppendLine($"out { GetVertexAttributeType((ShaderAttributeIds)i) } v_{ attr_names[i] };");
            }

            stream.AppendLine();
        }

        private static void GenerateVertexShaderMain(StringBuilder stream, Material mat)
        {
            stream.AppendLine("void main() {");

            stream.AppendLine("\tmat4x4 skin = ApplySkin(0) + ApplySkin(1) + ApplySkin(2) + ApplySkin(3);");
            stream.AppendLine($"\tvec3 t_Position = (skin * vec4(a_Position, 1.0)).xyz;");
            stream.AppendLine("\tv_Position = t_Position;");
            stream.AppendLine($"\tvec3 t_Normal = (skin * vec4(a_Normal, 1.0)).xyz;");

            stream.AppendLine();

            stream.AppendLine("\t// Variables for the upcoming lighting calculations.");
            stream.AppendLine("\tvec4 t_LightAccum;");
            stream.AppendLine("\tvec3 t_LightDelta, t_LightDeltaDir;");
            stream.AppendLine("\tfloat t_LightDeltaDist2, t_LightDeltaDist, angle_attenuation, dist_attenuation;");
            stream.AppendLine("\tvec4 t_ColorChanTemp;");
            stream.AppendLine("\tfloat diffuse_coeff;");

            stream.AppendLine();

            GenerateLightChannels(stream, mat);
            GenerateTexGens(stream, mat);

            stream.AppendLine("\tmat4x4 pv = ProjectionMatrix * ViewMatrix;");
            stream.AppendLine("\tgl_Position = pv * vec4(t_Position, 1.0);");

            stream.AppendLine("}");
        }

        private static void GenerateLightChannels(StringBuilder stream, Material mat)
        {
            stream.AppendLine($"\t// Color channels");

            for (int i = 0; i < mat.NumChannelControls; i++)
            {
                ColorChannelControl color = mat.ColorChannelControls[i * 2];
                ColorChannelControl alpha = mat.ColorChannelControls[(i * 2) + 1];

                if (color.Equals(alpha))
                {
                    stream.AppendLine($"\t// Color and Alpha for control { i } were the same! Optimizing...");
                    stream.AppendLine(GenerateColorChannel(color, i, $"v_Color{ i }"));
                }
                else
                {
                    stream.AppendLine($"\t// Color { i }");
                    stream.Append(GenerateColorChannel(color, i, "t_ColorChanTemp"));
                    stream.AppendLine($"\tv_Color{ i }.rgb = t_ColorChanTemp.rgb;");

                    stream.AppendLine();

                    stream.AppendLine($"\t// Alpha { i }");
                    stream.Append(GenerateColorChannel(alpha, i, "t_ColorChanTemp"));
                    stream.AppendLine($"\tv_Color{ i }.a = t_ColorChanTemp.a;");

                    stream.AppendLine();
                }
            }
        }

        private static string GenerateColorChannel(ColorChannelControl chan, int index, string output_name)
        {
            StringBuilder stream = new StringBuilder();

            string matColorSource = chan.MaterialSrc == GXColorSrc.Vertex ? $"a_Color{ index }" : $"MaterialColors[{ index }]";
            string ambColorSource = chan.AmbientSrc == GXColorSrc.Vertex ? $"a_Color{ index }" : $"AmbientColors[{ index }]";

            string generateLightAccum = "";

            if (chan.LightingEnabled)
            {
                generateLightAccum = $"\tt_LightAccum = { ambColorSource };\n";
                generateLightAccum += "\n\t// Lighting calculations! Trig ahoy!\n\n";

                for (int i = 0; i < 8; i++)
                {
                    GXLightMask cur_mask = GXLightMask.Light0 + i;

                    if (!chan.LitMask.HasFlag(cur_mask))
                        continue;

                    string light_name = $"LightParams[{ i }]";

                    generateLightAccum += "\t// t_LightDelta is a vector pointing from the light to the vertex.\n";
                    generateLightAccum += "\t// Because the dot product of a vector with itself is the vector's length squared,\n";
                    generateLightAccum += "\t// we can square root the dot product to get the distance between the light and the vertex.\n";
                    generateLightAccum += $"\tt_LightDelta = { light_name }.Position.xyz - v_Position.xyz;\n";
                    generateLightAccum += "\tt_LightDeltaDist2 = dot(t_LightDelta, t_LightDelta);\n";
                    generateLightAccum += "\tt_LightDeltaDist = sqrt(t_LightDeltaDist2);\n\n";

                    generateLightAccum += "\t// Dividing a vector by its length normalizes it.\n";
                    generateLightAccum += "\t// Doing this with our difference vector gives us the direction of the vertex from the light, without the magnitude.\n";
                    generateLightAccum += "\tt_LightDeltaDir = t_LightDelta / t_LightDeltaDist;\n\n";

                    generateLightAccum += "\t// This is how much light is hitting the vertex - the cosine of the angle of incidence between the vertex and the light.\n";
                    generateLightAccum += $"\tdiffuse_coeff = { GetDiffFn(chan) };\n\n";

                    generateLightAccum += GenerateAttnFn(chan, light_name);

                    generateLightAccum += "\t// The final color is the ambient color (the base or 'black') with the light's color added to it.\n";
                    generateLightAccum += "\t// But the color of the light is dampened by the angle at which the light is hitting our vertex (diffuse_coeff)\n";
                    generateLightAccum += "\t// and the ratio of the angle and distance attenuation behaviors (angle / dist).\n";
                    generateLightAccum += $"\tt_LightAccum += diffuse_coeff * (angle_attenuation / dist_attenuation) * { light_name }.Color;\n";
                }
            }
            else
            {
                generateLightAccum = "\t// Lighting isn't being applied, so instead of calculating it we'll say the vertex is getting a full blast of white light.\n";
                generateLightAccum += "\tt_LightAccum = vec4(1.0);";
            }

            stream.AppendLine(generateLightAccum);
            stream.AppendLine($"\t{ output_name } = { matColorSource } * clamp(t_LightAccum, 0.0, 1.0);");

            return stream.ToString();
        }

        private static string GetDiffFn(ColorChannelControl chan)
        {
            string dot = "dot(t_Normal, t_LightDeltaDir)";

            switch (chan.DiffuseFunction)
            {
                case GXDiffuseFunction.None:
                    return "1.0";
                case GXDiffuseFunction.Clamp:
                    return $"max({ dot }, 0.0)";
                case GXDiffuseFunction.Signed:
                    return dot;
                default:
                    return "";
            }
        }

        private static string GenerateAttnFn(ColorChannelControl chan, string light_name)
        {
            string attn = $"max(0.0, dot(t_LightDeltaDir, {light_name}.Direction.xyz))";
            string cosAttn = $"max(0.0, ApplyAttenuation({ light_name }.CosAtten.xyz, { attn }))";

            string output = "\t// This is the falloff behavior of the light as an object moves *around* it.\n";
            output += $"\tangle_attenuation = { cosAttn };\n\n";

            output += "\t// This is the falloff behavior of the light as an object moves *away from* it.\n";
            output += "\tdist_attenuation = ";

            switch (chan.AttenuationFunction)
            {
                case GXAttenuationFunction.None:
                    output += "1.0;\n\n";
                    break;
                case GXAttenuationFunction.Spot:
                    output += $"dot({ light_name }.DistAtten.xyz, vec3(1.0, t_LightDeltaDist, t_LightDeltaDist2));\n\n";
                    break;
                case GXAttenuationFunction.Spec:
                    output += $"ApplyAttenuation({ light_name }.DistAtten.xyz, { attn });\n\n";
                    break;
                default:
                    return "";
            }

            return output;
        }

        private static string GenerateTexGens(StringBuilder stream, Material mat)
        {
            for (int i = 0; i < mat.NumTexGens; i++)
            {
                TexCoordGen gen = mat.TexGens[i];

                stream.AppendLine($"\t// TexGen { i } - Type: { gen.Type }, Source: { gen.Source }, Matrix: { gen.TexMatrixSource }");
                stream.AppendLine($"\tv_Tex{ i } = { GenerateTexGenPost(gen) };\n");
            }

            return stream.ToString();
        }

        private static string GenerateTexGenPost(TexCoordGen gen)
        {
            StringBuilder stream = new StringBuilder();

            string gen_source = GenerateTexGenSource(gen.Source);

            if (gen.Type == GXTexGenType.SRTG)
            {
                return $"vec3({ gen_source }.xy, 1.0)";
            }
            else if (gen.Type == GXTexGenType.Matrix2x4)
            {
                return $"vec3({ GenerateTexGenMatrixMult(gen, gen_source) }.xy, 1.0)";
            }
            else if (gen.Type == GXTexGenType.Matrix3x4)
            {
                return GenerateTexGenMatrixMult(gen, gen_source);
            }

            return stream.ToString();
        }

        private static string GenerateTexGenSource(GXTexGenSrc src)
        {
            switch (src)
            {
                case GXTexGenSrc.Position:
                    return "vec4(a_Position, 1.0)";
                case GXTexGenSrc.Normal:
                    return "vec4(a_Normal, 1.0)";
                case GXTexGenSrc.Color0:
                    return "v_Color0";
                case GXTexGenSrc.Color1:
                    return "v_Color1";
                case GXTexGenSrc.Tex0:
                    return "vec4(a_Tex0, 1.0)";
                case GXTexGenSrc.Tex1:
                    return "vec4(a_Tex1, 1.0)";
                case GXTexGenSrc.Tex2:
                    return "vec4(a_Tex2, 1.0)";
                case GXTexGenSrc.Tex3:
                    return "vec4(a_Tex3, 1.0)";
                case GXTexGenSrc.Tex4:
                    return "vec4(a_Tex4, 1.0)";
                case GXTexGenSrc.Tex5:
                    return "vec4(a_Tex5, 1.0)";
                case GXTexGenSrc.Tex6:
                    return "vec4(a_Tex6, 1.0)";
                case GXTexGenSrc.Tex7:
                    return "vec4(a_Tex7, 1.0)";

                case GXTexGenSrc.TexCoord0:
                    return "vec4(v_Tex0, 1.0)";
                case GXTexGenSrc.TexCoord1:
                    return "vec4(v_Tex1, 1.0)";
                case GXTexGenSrc.TexCoord2:
                    return "vec4(v_Tex2, 1.0)";
                case GXTexGenSrc.TexCoord3:
                    return "vec4(v_Tex3, 1.0)";
                case GXTexGenSrc.TexCoord4:
                    return "vec4(v_Tex4, 1.0)";
                case GXTexGenSrc.TexCoord5:
                    return "vec4(v_Tex5, 1.0)";
                case GXTexGenSrc.TexCoord6:
                    return "vec4(v_Tex6, 1.0)";

                default:
                    return "";
            }
        }

        private static string GenerateTexGenMatrixMult(TexCoordGen gen, string src)
        {
            if (false)
            {
            }
            else
            {
                if (gen.TexMatrixSource == GXTexMatrix.Identity)
                {
                    return $"{ src }.xyz";
                }
                else if (gen.TexMatrixSource >= GXTexMatrix.TexMtx0)
                {
                    int id = (gen.TexMatrixSource - GXTexMatrix.TexMtx0) / 3;
                    return $"(mat4x4(TexMatrices[{ id }]) * { src })";
                }
                else
                {
                    return src;
                }
            }
        }

        private static string GetVertexAttributeType(ShaderAttributeIds id)
        {
            switch (id)
            {
                case ShaderAttributeIds.Position:
                case ShaderAttributeIds.Normal:
                case ShaderAttributeIds.Tex0:
                case ShaderAttributeIds.Tex1:
                case ShaderAttributeIds.Tex2:
                case ShaderAttributeIds.Tex3:
                case ShaderAttributeIds.Tex4:
                case ShaderAttributeIds.Tex5:
                case ShaderAttributeIds.Tex6:
                case ShaderAttributeIds.Tex7:
                    return "vec3";
                case ShaderAttributeIds.Color0:
                case ShaderAttributeIds.Color1:
                case ShaderAttributeIds.SkinIndices:
                case ShaderAttributeIds.SkinWeights:
                    return "vec4";
                default:
                    return "";
            }
        }
    }
}
