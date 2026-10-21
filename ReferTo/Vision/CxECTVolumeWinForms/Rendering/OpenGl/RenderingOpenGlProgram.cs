using System;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace CxECTVolumeWinForms.Rendering
{
    // GPU 程序对象的创建和销毁集中在此处。
    /// <summary>封装 OpenGL shader 的编译、链接、uniform 设置与资源释放。</summary>
    internal sealed class RenderingOpenGlProgram : IDisposable
    {
        private readonly int _handle;

        public RenderingOpenGlProgram(string vertexSource, string fragmentSource)
        {
            // shader 编译失败时直接抛出 OpenGL 日志；这比黑屏后再猜原因更容易定位问题。
            var vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            var fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            _handle = GL.CreateProgram();
            GL.AttachShader(_handle, vertexShader);
            GL.AttachShader(_handle, fragmentShader);
            GL.LinkProgram(_handle);

            int status;
            GL.GetProgram(_handle, GetProgramParameterName.LinkStatus, out status);
            if (status == 0)
            {
                throw new InvalidOperationException(GL.GetProgramInfoLog(_handle));
            }

            GL.DetachShader(_handle, vertexShader);
            GL.DetachShader(_handle, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);
        }

        public void Use()
        {
            GL.UseProgram(_handle);
        }

        public void SetInt(string name, int value)
        {
            GL.Uniform1(GL.GetUniformLocation(_handle, name), value);
        }

        public void SetFloat(string name, float value)
        {
            GL.Uniform1(GL.GetUniformLocation(_handle, name), value);
        }

        public void SetVector3(string name, Vector3 value)
        {
            GL.Uniform3(GL.GetUniformLocation(_handle, name), value);
        }

        public void SetMatrix4(string name, Matrix4 value)
        {
            // 当前 shader 使用 vec4 * matrix 的写法，矩阵上传时转置后才能和 OpenTK 的 Matrix4 乘法结果一致。
            GL.UniformMatrix4(GL.GetUniformLocation(_handle, name), true, ref value);
        }

        public void Dispose()
        {
            GL.DeleteProgram(_handle);
        }

        private static int CompileShader(ShaderType type, string source)
        {
            var shader = GL.CreateShader(type);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);

            int status;
            GL.GetShader(shader, ShaderParameter.CompileStatus, out status);
            if (status == 0)
            {
                throw new InvalidOperationException(GL.GetShaderInfoLog(shader));
            }

            return shader;
        }
    }
}
