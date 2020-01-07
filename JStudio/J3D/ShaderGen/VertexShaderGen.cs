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

            stream.AppendLine("mat4x3 GetPosTexMatrix(uint t_MtxIdx) {");
            stream.AppendLine($"\tif (t_MtxIdx == { (int)GXTexMatrix.Identity }u)");
            stream.AppendLine("\t\treturn _Mat4x3(1.0);");
            stream.AppendLine($"\telse if (t_MtxIdx >= { (int)GXTexMatrix.TexMtx0 }u)");
            stream.AppendLine($"\t\treturn u_TexMtx[(t_MtxIdx - { (int)GXTexMatrix.TexMtx0 }u) / 3u];");
            stream.AppendLine("\telse");
            stream.AppendLine("\t\treturn u_PosMtx[t_MtxIdx / 3u];");
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

            stream.AppendLine($"\tvec3 t_position = { "a" };");
            stream.AppendLine("\tv_Position = t_Position;");
            stream.AppendLine($"\tvec3 t_Normal = { "a" };");

            stream.AppendLine();

            stream.AppendLine("\t// Variables for the upcoming lighting calculations.");
            stream.AppendLine("\tvec4 t_LightAccum;");
            stream.AppendLine("\tvec3 t_LightDelta, t_LightDeltaDir;");
            stream.AppendLine("\tfloat t_LightDeltaDist2, t_LightDeltaDist;");
            stream.AppendLine("\tvec4 t_ColorChanTemp;");

            stream.AppendLine();

            GenerateLightChannels(stream, mat);

            stream.AppendLine("\tgl_Position = ProjectionMatrix * ViewMatrix * vec4(t_Position, 1.0);");

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
                    generateLightAccum += $"\tfloat diffuse_coeff = { GetDiffFn(chan) };\n\n";

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
            output += $"\tfloat angle_attenuation = { cosAttn };\n\n";

            output += "\t// This is the falloff behavior of the light as an object moves *away from* it.\n";
            output += "\tfloat dist_attenuation = ";

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

        private static string GetVertexAttributeType(ShaderAttributeIds id)
        {
            switch (id)
            {
                case ShaderAttributeIds.Position:
                case ShaderAttributeIds.Normal:
                    return "vec3";
                case ShaderAttributeIds.Color0:
                case ShaderAttributeIds.Color1:
                case ShaderAttributeIds.SkinIndices:
                case ShaderAttributeIds.SkinWeights:
                    return "vec4";
                case ShaderAttributeIds.Tex0:
                case ShaderAttributeIds.Tex1:
                case ShaderAttributeIds.Tex2:
                case ShaderAttributeIds.Tex3:
                case ShaderAttributeIds.Tex4:
                case ShaderAttributeIds.Tex5:
                case ShaderAttributeIds.Tex6:
                case ShaderAttributeIds.Tex7:
                    return "vec2";
                default:
                    return "";
            }
        }
    }
}
