using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering
{
    /// <summary>
    /// WPF HwndHost 内直接创建 WGL OpenGL 3.3 Core 上下文。
    /// 复用原成熟 GLSL Raymarch 和包围盒顶点；不引入 WinForms GLControl/适配器。
    /// 若显示驱动仅支持 OpenGL 1.1，明确报告不可用，由外层选择 WPF 空间切面。
    /// 此控件只接收体素字节与传递函数，不拥有 Foundation.Volume 或 CT3D 状态。
    /// </summary>
    public sealed class MPWVolumeOpenGlControl : HwndHost
    {
        private IntPtr _window, _dc, _context;
        private GlApi _gl;
        // glUniform* 的 location 属于当前激活的Program，不能沿用体绘Shader句柄。
        private uint _activeShaderProgram;
        private uint _shader, _vao, _vbo, _volumeTexture, _scalarTexture, _gradientTexture;
        private uint _sliceShader, _sliceVao, _sliceVbo, _lineShader, _lineVao, _lineVbo;
        private uint _obliqueTexture;
        private byte[] _obliquePixels;
        private int _obliqueWidth,_obliqueHeight;
        private float _sliceX=.5f, _sliceY=.5f, _sliceZ=.5f, _sliceOpacity=.35f;
        private MVISliceViewport _xyBounds=MVISliceViewport.Full;
        private MVISliceViewport _xzBounds=MVISliceViewport.Full;
        private MVISliceViewport _yzBounds=MVISliceViewport.Full;
        private bool _showSlices=true, _showSliceTexture=false, _showVolume=true, _showBounds=false;
        private float[] _obliqueCorners; // 4x3 normalized 3D纹理坐标，允许平面超出体数据边界。
        private byte[] _voxels, _scalar, _gradient;
        private int _width, _height, _depth;
        private float _sx = 1, _sy = 1, _sz = 1;
        private float _yaw = 0.62f, _pitch = 0.36f, _distance = 2.9f;
        private bool _mouseDown;
        private int _mouseX, _mouseY;
        private float _opacity = 1, _stepScale = 1, _referenceStep = 1f / 256f;
        private float _ambient = .34f, _diffuse = .62f, _specular = .16f, _shine = 24;
        private bool _jitter = true;
        public event EventHandler AvailabilityChanged;
        public bool IsAvailable { get; private set; }
        public string FailureReason { get; private set; }
        public string DriverVersion { get; private set; }

        public MPWVolumeOpenGlControl()
        {
            Focusable = true;
            SizeChanged += (s, e) => RenderFrame();
        }

        public void SetVolume(byte[] voxels, int width, int height, int depth,
            float spacingX, float spacingY, float spacingZ,
            byte[] rgbaTransfer, byte[] gradientOpacity)
        {
            if (voxels == null || rgbaTransfer == null || gradientOpacity == null)
                throw new ArgumentNullException("voxels/transfer");
            if (width <= 0 || height <= 0 || depth <= 0 ||
                spacingX <= 0 || spacingY <= 0 || spacingZ <= 0 ||
                float.IsNaN(spacingX) || float.IsNaN(spacingY) || float.IsNaN(spacingZ) ||
                float.IsInfinity(spacingX) || float.IsInfinity(spacingY) || float.IsInfinity(spacingZ))
                throw new ArgumentOutOfRangeException("width", "体素尺寸/Spacing必须是有限正数。");
            long count = checked((long)width * height * depth);
            if (voxels.LongLength != count || rgbaTransfer.Length != 1024 || gradientOpacity.Length != 256)
                throw new ArgumentException("体数据或传递函数LUT长度不正确。");
            _voxels = voxels; _width = width; _height = height; _depth = depth;
            float totalX = width * spacingX, totalY = height * spacingY, totalZ = depth * spacingZ;
            float largest = Math.Max(totalX, Math.Max(totalY, totalZ));
            _sx = totalX / largest; _sy = totalY / largest; _sz = totalZ / largest;
            _scalar = rgbaTransfer; _gradient = gradientOpacity;
            if (IsAvailable) { MakeCurrent(); UploadTextures(); RenderFrame(); }
        }

        public void UpdateTransfer(byte[] rgba, byte[] gradient)
        {
            if (rgba == null || rgba.Length != 1024 || gradient == null || gradient.Length != 256)
                throw new ArgumentException("需要256级RGBA8和R8曲线。");
            _scalar = rgba; _gradient = gradient;
            if (IsAvailable) { MakeCurrent(); UploadTransfers(); RenderFrame(); }
        }

        public void SetRendering(float opacity, float stepScale, float referenceStep,
            bool jitter, float ambient, float diffuse, float specular, float specularPower)
        {
            _opacity = Clamp(opacity, 0, 4);
            _stepScale = Clamp(stepScale, .1f, 4);
            _referenceStep = Clamp(referenceStep, .00001f, 1);
            _jitter = jitter; _ambient = Clamp(ambient,0,2);
            _diffuse = Clamp(diffuse,0,2); _specular = Clamp(specular,0,2);
            _shine = Clamp(specularPower,1,128);
            RenderFrame();
        }

        /// <summary>切面均使用0..1空间坐标，由MVI唯一三刀状态计算。</summary>
        public void SetSlicePositions(float x,float y,float z)
        {
            _sliceX=Clamp(x,0,1); _sliceY=Clamp(y,0,1); _sliceZ=Clamp(z,0,1);
            RenderFrame();
        }

        /// <summary>3D正交面随二维浏览器缩放/平移；复用现有MVI视口范围。</summary>
        public void SetSliceViewports(MVISliceViewport xy,MVISliceViewport xz,MVISliceViewport yz)
        {
            _xyBounds=xy;_xzBounds=xz;_yzBounds=yz;
            RenderFrame();
        }

        public void SetSceneOptions(bool volume,bool slices,bool sliceTexture,bool bounds,float sliceOpacity)
        {
            _showVolume=volume; _showSlices=slices; _showSliceTexture=sliceTexture;
            _showBounds=bounds; _sliceOpacity=Clamp(sliceOpacity,0,1);
            RenderFrame();
        }

        /// <summary>四角0..1纹理坐标来自FVOObliqueSliceGeometry，null关闭任意切。</summary>
        public void SetObliqueCorners(float[] corners)
        {
            if (corners != null)
            {
                if (corners.Length != 12)throw new ArgumentException("任意切四角应为12个坐标值。");
                foreach(float value in corners)
                    if(float.IsNaN(value)||float.IsInfinity(value))
                        throw new ArgumentOutOfRangeException("corners");
                _obliqueCorners=(float[])corners.Clone();
            }
            else _obliqueCorners=null;
            RenderFrame();
        }

        /// <summary>旧版斜切面专用2D纹理；高精度切片像素与三维空间四角分离。</summary>
        public void SetObliqueTexture(byte[] grayscale,int width,int height)
        {
            if(grayscale==null)
            {
                _obliquePixels=null;_obliqueWidth=_obliqueHeight=0;
            }
            else
            {
                if(width<=0||height<=0||grayscale.LongLength!=(long)width*height)
                    throw new ArgumentException("任意切纹理尺寸错误。");
                _obliquePixels=(byte[])grayscale.Clone();
                _obliqueWidth=width;_obliqueHeight=height;
            }
            if(IsAvailable)
            {
                MakeCurrent();
                UploadObliqueTexture();
                RenderFrame();
            }
        }

        private void UploadObliqueTexture()
        {
            if(_obliqueTexture!=0)
            {
                _gl.DeleteTextures(1,ref _obliqueTexture);
                _obliqueTexture=0;
            }
            if(_obliquePixels==null)return;
            _gl.GenTextures(1,out _obliqueTexture);
            _gl.ActiveTexture(0x84C1);
            _gl.BindTexture(0x0DE1,_obliqueTexture); // GL_TEXTURE_2D
            glPixelStorei(0x0CF5,1);
            PinBytes(_obliquePixels,p=>glTexImage2D(0x0DE1,0,0x8229,
                _obliqueWidth,_obliqueHeight,0,0x1903,0x1401,p));
            TextureParameters(0x0DE1);
            _gl.TexParameteri(0x0DE1,0x2803,0x812F);
        }

        public void ResetCamera()
        {
            _yaw = .62f; _pitch = .36f; _distance = 2.9f; RenderFrame();
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _window = CreateWindowEx(0, "STATIC", "", 0x50000000 | 0x02000000 | 0x04000000,
                0, 0, 100, 100, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (_window == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                _dc = GetDC(_window);
                if (_dc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
                PixelFormatDescriptor pfd = new PixelFormatDescriptor();
                pfd.Size = (ushort)Marshal.SizeOf(typeof(PixelFormatDescriptor));
                pfd.Version = 1;
                pfd.Flags = 0x00000004 | 0x00000020 | 0x00000001; // draw to window/OpenGL/double buffer
                pfd.PixelType = 0; pfd.ColorBits = 32; pfd.DepthBits = 24;
                int format = ChoosePixelFormat(_dc, ref pfd);
                if (format == 0 || !SetPixelFormat(_dc, format, ref pfd))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WGL像素格式设置失败。");
                IntPtr bootstrap = wglCreateContext(_dc);
                if (bootstrap == IntPtr.Zero || !wglMakeCurrent(_dc, bootstrap))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "WGL初始化失败。");
                try
                {
                    IntPtr extension = wglGetProcAddress("wglCreateContextAttribsARB");
                    if (!ValidProc(extension))
                        throw new NotSupportedException("显卡驱动不提供WGL_ARB_create_context（通常只有OpenGL 1.1）。");
                    WglCreateContextAttribs create = (WglCreateContextAttribs)
                        Marshal.GetDelegateForFunctionPointer(extension, typeof(WglCreateContextAttribs));
                    // 3.3 Core Profile; 不允许静默退回固定管线而伪称Raymarch成功。
                    int[] attributes = { 0x2091,3, 0x2092,3, 0x9126,0x00000001, 0 };
                    IntPtr modern = create(_dc, IntPtr.Zero, attributes);
                    if (modern == IntPtr.Zero || !wglMakeCurrent(_dc, modern))
                        throw new NotSupportedException("当前驱动未提供OpenGL 3.3 Core Profile。");
                    _context = modern;
                }
                finally
                {
                    if (_context == IntPtr.Zero) wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);
                    wglDeleteContext(bootstrap);
                }
                DriverVersion = Marshal.PtrToStringAnsi(glGetString(0x1F02));
                _gl = new GlApi();
                _shader = CreateProgram(MPWVolumeShaderSources.VolumeVertex, MPWVolumeShaderSources.VolumeFragment);
                _sliceShader = CreateProgram(MPWVolumeShaderSources.SliceVertex, MPWVolumeShaderSources.SliceFragment);
                _lineShader = CreateProgram(MPWVolumeShaderSources.LineVertex, MPWVolumeShaderSources.LineFragment);
                _gl.GenVertexArrays(1,out _vao);
                _gl.GenBuffers(1,out _vbo);
                _gl.BindVertexArray(_vao);
                _gl.BindBuffer(0x8892,_vbo); // GL_ARRAY_BUFFER
                UploadFloats(MPWVolumeGeometry.CubeVertices);
                _gl.VertexAttribPointer(0,3,0x1406,0,3*sizeof(float),IntPtr.Zero);
                _gl.EnableVertexAttribArray(0);
                _gl.GenVertexArrays(1,out _sliceVao);
                _gl.GenBuffers(1,out _sliceVbo);
                _gl.BindVertexArray(_sliceVao);
                _gl.BindBuffer(0x8892,_sliceVbo);
                _gl.VertexAttribPointer(0,3,0x1406,0,11*sizeof(float),IntPtr.Zero);
                _gl.EnableVertexAttribArray(0);
                _gl.VertexAttribPointer(1,3,0x1406,0,11*sizeof(float),new IntPtr(3*sizeof(float)));
                _gl.EnableVertexAttribArray(1);
                _gl.VertexAttribPointer(2,3,0x1406,0,11*sizeof(float),new IntPtr(6*sizeof(float)));
                _gl.EnableVertexAttribArray(2);
                _gl.VertexAttribPointer(3,2,0x1406,0,11*sizeof(float),new IntPtr(9*sizeof(float)));
                _gl.EnableVertexAttribArray(3);
                _gl.GenVertexArrays(1,out _lineVao);
                _gl.GenBuffers(1,out _lineVbo);
                _gl.BindVertexArray(_lineVao);
                _gl.BindBuffer(0x8892,_lineVbo);
                UploadFloats(MPWVolumeGeometry.CubeLines);
                _gl.VertexAttribPointer(0,3,0x1406,0,3*sizeof(float),IntPtr.Zero);
                _gl.EnableVertexAttribArray(0);
                glClearColor(.025f,.037f,.061f,1);
                glEnable(0x0BE2); // GL_BLEND
                glBlendFunc(0x0302,0x0303); // SRC_ALPHA, ONE_MINUS_SRC_ALPHA
                IsAvailable = true; FailureReason = null;
                if (_voxels != null) UploadTextures();
                if (_obliquePixels!=null) UploadObliqueTexture();
                RenderFrame();
            }
            catch (Exception exception)
            {
                FailureReason = exception.GetType().Name + ": " + exception.Message;
                IsAvailable = false;
                ReleaseResources();
            }
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(NotifyAvailability));
            return new HandleRef(this,_window);
        }

        private void NotifyAvailability()
        {
            EventHandler changed = AvailabilityChanged;
            if (changed != null) changed(this,EventArgs.Empty);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            ReleaseResources();
            if (_dc != IntPtr.Zero) { ReleaseDC(_window,_dc); _dc = IntPtr.Zero; }
            if (_window != IntPtr.Zero) { DestroyWindow(_window); _window = IntPtr.Zero; }
        }

        private void ReleaseResources()
        {
            if (_context == IntPtr.Zero) return;
            if (wglMakeCurrent(_dc,_context) && _gl != null)
            {
                if (_shader != 0) _gl.DeleteProgram(_shader);
                if (_sliceShader != 0) _gl.DeleteProgram(_sliceShader);
                if (_lineShader != 0) _gl.DeleteProgram(_lineShader);
                if (_volumeTexture != 0) _gl.DeleteTextures(1,ref _volumeTexture);
                if (_scalarTexture != 0) _gl.DeleteTextures(1,ref _scalarTexture);
                if (_gradientTexture != 0) _gl.DeleteTextures(1,ref _gradientTexture);
                if (_obliqueTexture != 0) _gl.DeleteTextures(1,ref _obliqueTexture);
                if (_vbo != 0) _gl.DeleteBuffers(1,ref _vbo);
                if (_sliceVbo != 0) _gl.DeleteBuffers(1,ref _sliceVbo);
                if (_lineVbo != 0) _gl.DeleteBuffers(1,ref _lineVbo);
                if (_vao != 0) _gl.DeleteVertexArrays(1,ref _vao);
                if (_sliceVao != 0) _gl.DeleteVertexArrays(1,ref _sliceVao);
                if (_lineVao != 0) _gl.DeleteVertexArrays(1,ref _lineVao);
            }
            wglMakeCurrent(IntPtr.Zero,IntPtr.Zero);
            wglDeleteContext(_context); _context = IntPtr.Zero; IsAvailable = false;
            _shader = _vao = _vbo = _volumeTexture = _scalarTexture = _gradientTexture = 0;
            _sliceShader = _lineShader = _sliceVao = _sliceVbo = _lineVao = _lineVbo = 0;
            _obliqueTexture=0;
            _gl = null;
        }

        private void MakeCurrent()
        {
            if (_context == IntPtr.Zero || !wglMakeCurrent(_dc,_context))
                throw new InvalidOperationException("OpenGL上下文已关闭或无法绑定到UI线程。");
        }

        private uint CreateProgram(string vertex, string fragment)
        {
            uint vs = Compile(0x8B31,vertex), fs = 0, program = 0;
            try
            {
                fs = Compile(0x8B30,fragment);
                program = _gl.CreateProgram();
                _gl.AttachShader(program,vs); _gl.AttachShader(program,fs);
                _gl.LinkProgram(program);
                int ok; _gl.GetProgramiv(program,0x8B82,out ok);
                if (ok == 0)
                {
                    StringBuilder log = new StringBuilder(8192);
                    int size; _gl.GetProgramInfoLog(program,log.Capacity,out size,log);
                    throw new InvalidOperationException("旧版GLSL链接失败: "+log);
                }
                return program;
            }
            catch
            {
                if(program != 0) _gl.DeleteProgram(program);
                throw;
            }
            finally { if(vs!=0)_gl.DeleteShader(vs); if(fs!=0)_gl.DeleteShader(fs); }
        }

        private uint Compile(uint type,string source)
        {
            uint shader = _gl.CreateShader(type);
            IntPtr text = Marshal.StringToHGlobalAnsi(source);
            try
            {
                _gl.ShaderSource(shader,1,ref text,IntPtr.Zero);
                _gl.CompileShader(shader);
                int success; _gl.GetShaderiv(shader,0x8B81,out success);
                if(success == 0)
                {
                    StringBuilder log = new StringBuilder(8192);
                    int length; _gl.GetShaderInfoLog(shader,log.Capacity,out length,log);
                    throw new InvalidOperationException("旧版GLSL编译失败: "+log);
                }
                return shader;
            }
            catch { _gl.DeleteShader(shader); throw; }
            finally { Marshal.FreeHGlobal(text); }
        }

        private void UploadFloats(float[] vertices)
        {
            GCHandle pinned = GCHandle.Alloc(vertices,GCHandleType.Pinned);
            try { _gl.BufferData(0x8892,new IntPtr(vertices.Length*sizeof(float)),
                pinned.AddrOfPinnedObject(),0x88E4); }
            finally { pinned.Free(); }
        }

        private void UploadTextures()
        {
            if (!IsAvailable || _voxels == null) return;
            glGetIntegerv(0x8073, out int maxSize); // GL_MAX_3D_TEXTURE_SIZE
            if (_width > maxSize || _height > maxSize || _depth > maxSize)
                throw new NotSupportedException("GPU三维纹理尺寸超过驱动限制: "+maxSize);
            if (_volumeTexture != 0) _gl.DeleteTextures(1,ref _volumeTexture);
            _gl.GenTextures(1,out _volumeTexture);
            _gl.ActiveTexture(0x84C0);
            _gl.BindTexture(0x806F,_volumeTexture); // GL_TEXTURE_3D
            glPixelStorei(0x0CF5,1); // GL_UNPACK_ALIGNMENT
            PinBytes(_voxels, p => _gl.TexImage3D(0x806F,0,0x8229,_width,_height,_depth,
                0,0x1903,0x1401,p)); // GL_R8, GL_RED, GL_UNSIGNED_BYTE
            TextureParameters(0x806F);
            UploadTransfers();
        }

        private void UploadTransfers()
        {
            if (!IsAvailable || _scalar == null || _gradient == null) return;
            if (_scalarTexture != 0) _gl.DeleteTextures(1,ref _scalarTexture);
            if (_gradientTexture != 0) _gl.DeleteTextures(1,ref _gradientTexture);
            _gl.GenTextures(1,out _scalarTexture);
            _gl.ActiveTexture(0x84C1);
            _gl.BindTexture(0x0DE0,_scalarTexture);
            PinBytes(_scalar,p => _gl.TexImage1D(0x0DE0,0,0x8058,256,0,0x1908,0x1401,p)); // RGBA8
            TextureParameters(0x0DE0);
            _gl.GenTextures(1,out _gradientTexture);
            _gl.ActiveTexture(0x84C2);
            _gl.BindTexture(0x0DE0,_gradientTexture);
            PinBytes(_gradient,p => _gl.TexImage1D(0x0DE0,0,0x8229,256,0,0x1903,0x1401,p)); // R8
            TextureParameters(0x0DE0);
        }

        private void TextureParameters(uint target)
        {
            _gl.TexParameteri(target,0x2801,0x2601); // GL_TEXTURE_MIN_FILTER LINEAR
            _gl.TexParameteri(target,0x2800,0x2601);
            _gl.TexParameteri(target,0x2802,0x812F); // WRAP_S CLAMP_TO_EDGE
            if(target==0x806F)
            {
                _gl.TexParameteri(target,0x2803,0x812F); // T
                _gl.TexParameteri(target,0x8072,0x812F); // R
            }
        }

        private static void PinBytes(byte[] data,Action<IntPtr> action)
        {
            GCHandle pinned = GCHandle.Alloc(data,GCHandleType.Pinned);
            try { action(pinned.AddrOfPinnedObject()); }
            finally { pinned.Free(); }
        }

        /// <summary>从真实OpenGL framebuffer读取GPU结果；不使用WPF RenderTargetBitmap截取HwndHost空白区域。</summary>
        public BitmapSource CaptureFrame()
        {
            if (!IsAvailable) throw new InvalidOperationException("当前驱动不支持真实GPU体绘。");
            RenderFrame();
            MakeCurrent();
            RectNative r;
            if (!GetClientRect(_window,out r)) throw new Win32Exception(Marshal.GetLastWin32Error());
            int width=Math.Max(1,r.Right-r.Left),height=Math.Max(1,r.Bottom-r.Top);
            int stride=checked(width*4);
            byte[] bottomUp=new byte[checked(stride*height)];
            glPixelStorei(0x0D05,1); // GL_PACK_ALIGNMENT
            glReadBuffer(0x0404); // GL_FRONT: SwapBuffers之后读取正在显示的帧，不读无效的Back buffer
            PinBytes(bottomUp,p=>glReadPixels(0,0,width,height,0x80E1,0x1401,p)); // BGRA/UNSIGNED_BYTE
            byte[] topDown=new byte[bottomUp.Length];
            for(int row=0;row<height;row++)
                Buffer.BlockCopy(bottomUp,(height-1-row)*stride,topDown,row*stride,stride);
            BitmapSource bmp=BitmapSource.Create(width,height,96,96,
                PixelFormats.Bgra32,null,topDown,stride);
            bmp.Freeze();
            return bmp;
        }

        public void RenderFrame()
        {
            if (!IsAvailable || _window==IntPtr.Zero || _voxels==null || _shader==0 || _volumeTexture==0) return;
            MakeCurrent();
            RectNative r; if(!GetClientRect(_window,out r))return;
            int w=Math.Max(1,r.Right-r.Left),h=Math.Max(1,r.Bottom-r.Top);
            glViewport(0,0,w,h);
            glClear(0x00004000 | 0x00000100);
            float cy=(float)Math.Cos(_yaw),sy=(float)Math.Sin(_yaw);
            float cp=(float)Math.Cos(_pitch),sp=(float)Math.Sin(_pitch);
            Vector3 camera = new Vector3(_distance*sy*cp,_distance*sp,_distance*cy*cp);
            Matrix4x4 model = Matrix4x4.CreateScale(_sx,_sy,_sz);
            Matrix4x4 view = Matrix4x4.CreateLookAt(camera,Vector3.Zero,Vector3.UnitY);
            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
                (float)(Math.PI/4),w/(float)h,.02f,30);
            if (_showVolume)
            {
            UseShaderProgram(_shader);
            Matrix("model",model); Matrix("view",view); Matrix("projection",projection);
            Vec3("cameraObject",camera.X/_sx,camera.Y/_sy,camera.Z/_sz);
            Vec3("voxelStep",1f/_width,1f/_height,1f/_depth);
            Vec3("physicalScale",_sx,_sy,_sz);
            Uniform1("volumeTex",0); Uniform1("scalarTransferTex",1); Uniform1("gradientOpacityTex",2);
            UniformF("rayStep",Clamp(_stepScale/Math.Max(_width,Math.Max(_height,_depth)),.0009f,.012f));
            UniformF("opacityReferenceStep",_referenceStep); UniformF("opacityScale",_opacity);
            Uniform1("enableJitter",_jitter?1:0);
            UniformF("materialAmbient",_ambient); UniformF("materialDiffuse",_diffuse);
            UniformF("materialSpecular",_specular); UniformF("materialSpecularPower",_shine);
            _gl.ActiveTexture(0x84C0); _gl.BindTexture(0x806F,_volumeTexture);
            _gl.ActiveTexture(0x84C1); _gl.BindTexture(0x0DE0,_scalarTexture);
            _gl.ActiveTexture(0x84C2); _gl.BindTexture(0x0DE0,_gradientTexture);
            _gl.BindVertexArray(_vao);
            _gl.DrawArrays(0x0004,0,MPWVolumeGeometry.CubeVertices.Length/3);
            }
            Matrix4x4 mvp = model * view * projection;
            if (_showSlices || _showSliceTexture || _obliqueCorners != null)
            {
                UseShaderProgram(_sliceShader);
                Matrix("mvp",mvp);
                Uniform1("volumeTex",0);
                Uniform1("obliqueTex",1);
                Uniform1("showSliceTexture",_showSliceTexture?1:0);
                Uniform1("showSlicePlanes",_showSlices?1:0);
                Uniform1("useObliqueTexture",0);
                UniformF("sliceOpacity",_sliceOpacity);
                _gl.ActiveTexture(0x84C0);
                _gl.BindTexture(0x806F,_volumeTexture);
                _gl.BindVertexArray(_sliceVao);
                _gl.BindBuffer(0x8892,_sliceVbo);
                UploadFloats(BuildSliceVertices());
                if (_showSlices || _showSliceTexture)
                    _gl.DrawArrays(0x0004,0,18);
                if (_obliqueCorners != null)
                {
                    Uniform1("showSliceTexture",1);
                    Uniform1("useObliqueTexture",_obliqueTexture!=0?1:0);
                    if(_obliqueTexture!=0)
                    {
                        _gl.ActiveTexture(0x84C1);
                        _gl.BindTexture(0x0DE1,_obliqueTexture);
                    }
                    Uniform1("showSlicePlanes",1);
                    _gl.DrawArrays(0x0004,18,6);
                    Uniform1("useObliqueTexture",0);
                }
            }
            if (_showBounds)
            {
                UseShaderProgram(_lineShader);
                Matrix("mvp",mvp);
                Vec3("lineColor",.82f,.88f,.95f);
                _gl.BindVertexArray(_lineVao);
                _gl.DrawArrays(0x0001,0,MPWVolumeGeometry.CubeLines.Length/3);
            }
            SwapBuffers(_dc);
        }

        /// <summary>直接沿用原版11浮点布局：位置/3D纹理/颜色/任意切UV，三刀单一坐标源。</summary>
        private float[] BuildSliceVertices()
        {
            float[] vertices=new float[(_obliqueCorners==null ? 18 : 24)*11];
            int index=0;
            // 红色YZ切面固定X，绿色XZ固定Y，蓝色XY固定Z。
            AddQuad(vertices,ref index,new float[]
            {
                _sliceX,_yzBounds.MinA,_yzBounds.MinB, _sliceX,_yzBounds.MaxA,_yzBounds.MinB,
                _sliceX,_yzBounds.MaxA,_yzBounds.MaxB, _sliceX,_yzBounds.MinA,_yzBounds.MaxB
            },1f,.22f,.2f);
            AddQuad(vertices,ref index,new float[]
            {
                _xzBounds.MinA,_sliceY,_xzBounds.MinB, _xzBounds.MaxA,_sliceY,_xzBounds.MinB,
                _xzBounds.MaxA,_sliceY,_xzBounds.MaxB, _xzBounds.MinA,_sliceY,_xzBounds.MaxB
            },.28f,.85f,.32f);
            AddQuad(vertices,ref index,new float[]
            {
                _xyBounds.MinA,_xyBounds.MinB,_sliceZ, _xyBounds.MaxA,_xyBounds.MinB,_sliceZ,
                _xyBounds.MaxA,_xyBounds.MaxB,_sliceZ, _xyBounds.MinA,_xyBounds.MaxB,_sliceZ
            },.2f,.48f,1f);
            if (_obliqueCorners!=null)
                AddQuad(vertices,ref index,_obliqueCorners,1f,.76f,.18f);
            return vertices;
        }
        private static void AddQuad(float[] output,ref int offset,float[] coords,float red,float green,float blue)
        {
            int[] triangles={0,1,2,0,2,3};
            foreach(int vertex in triangles)
            {
                int i=vertex*3;
                float x=coords[i],y=coords[i+1],z=coords[i+2];
                output[offset++]=x-.5f; output[offset++]=y-.5f; output[offset++]=z-.5f;
                output[offset++]=x; output[offset++]=y; output[offset++]=z;
                output[offset++]=red; output[offset++]=green; output[offset++]=blue;
                // 正交面无需额外UV；任意切按四角顺序提供真正的2D纹理坐标。
                output[offset++]=coords.Length==12 ? (vertex==1||vertex==2?1:0) : 0;
                output[offset++]=coords.Length==12 ? (vertex>=2?1:0) : 0;
            }
        }

        private void UseShaderProgram(uint program)
        {
            _gl.UseProgram(program);
            _activeShaderProgram=program;
        }

        private int UniformLocation(string name)
        {
            if(_activeShaderProgram==0)
                throw new InvalidOperationException("必须先绑定OpenGL Program，才能设置Uniform："+name);
            return _gl.GetUniformLocation(_activeShaderProgram,name);
        }

        private void Matrix(string name,Matrix4x4 m)
        {
            float[] a = {m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
                m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44};
            _gl.UniformMatrix4fv(UniformLocation(name),1,1,a);
        }
        private void Vec3(string name,float x,float y,float z)
        { _gl.Uniform3f(UniformLocation(name),x,y,z); }
        private void Uniform1(string name,int value)
        { _gl.Uniform1i(UniformLocation(name),value); }
        private void UniformF(string name,float value)
        { _gl.Uniform1f(UniformLocation(name),value); }
        private static float Clamp(float v,float min,float max)
        { return Math.Max(min,Math.Min(max,v)); }

        protected override IntPtr WndProc(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            int x=unchecked((short)((long)lParam & 0xffff));
            int y=unchecked((short)(((long)lParam>>16)&0xffff));
            if(msg==0x0201) { _mouseDown=true; _mouseX=x; _mouseY=y; SetCapture(hwnd); handled=true; }
            else if(msg==0x0202) { _mouseDown=false; ReleaseCapture(); handled=true; }
            else if(msg==0x0200 && _mouseDown)
            {
                _yaw+=(x-_mouseX)*.007f;
                _pitch=Clamp(_pitch- (y-_mouseY)*.007f,-1.47f,1.47f);
                _mouseX=x; _mouseY=y; RenderFrame(); handled=true;
            }
            else if(msg==0x020A)
            {
                short delta=unchecked((short)(((long)wParam>>16)&0xffff));
                _distance=Clamp(_distance*(delta>0?.87f:1.15f),1.3f,12f);
                RenderFrame(); handled=true;
            }
            else if(msg==0x0005) { RenderFrame(); } // WM_SIZE
            else if(msg==0x000F && IsAvailable)
            {
                RenderFrame();
                ValidateRect(hwnd,IntPtr.Zero);
                handled=true;
            }
            return IntPtr.Zero;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PixelFormatDescriptor
        {
            public ushort Size,Version; public uint Flags;
            public byte PixelType,ColorBits,RedBits,RedShift,GreenBits,GreenShift,BlueBits,
                BlueShift,AlphaBits,AlphaShift,AccumBits,AccumRedBits,AccumGreenBits,
                AccumBlueBits,AccumAlphaBits,DepthBits,StencilBits,AuxBuffers,LayerType,Reserved;
            public uint LayerMask,VisibleMask,DamageMask;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct RectNative { public int Left,Top,Right,Bottom; }
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WglCreateContextAttribs(IntPtr dc,IntPtr share,[In]int[] attributes);
        [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        private static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,
            int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd,out RectNative rect);
        [DllImport("user32.dll")] private static extern bool ValidateRect(IntPtr hwnd,IntPtr rectangle);
        [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("gdi32.dll")] private static extern int ChoosePixelFormat(IntPtr dc,ref PixelFormatDescriptor fmt);
        [DllImport("gdi32.dll")] private static extern bool SetPixelFormat(IntPtr dc,int fmt,ref PixelFormatDescriptor pfd);
        [DllImport("gdi32.dll")] private static extern bool SwapBuffers(IntPtr dc);
        [DllImport("opengl32.dll")] private static extern IntPtr wglCreateContext(IntPtr dc);
        [DllImport("opengl32.dll")] private static extern bool wglMakeCurrent(IntPtr dc,IntPtr context);
        [DllImport("opengl32.dll")] private static extern bool wglDeleteContext(IntPtr context);
        [DllImport("opengl32.dll",CharSet=CharSet.Ansi)]
        private static extern IntPtr wglGetProcAddress(string name);
        [DllImport("opengl32.dll")] private static extern IntPtr glGetString(uint name);
        [DllImport("opengl32.dll")] private static extern void glClearColor(float r,float g,float b,float a);
        [DllImport("opengl32.dll")] private static extern void glClear(uint flags);
        [DllImport("opengl32.dll")] private static extern void glViewport(int x,int y,int w,int h);
        [DllImport("opengl32.dll")] private static extern void glGetIntegerv(uint name,out int value);
        [DllImport("opengl32.dll")] private static extern void glReadPixels(int x,int y,int width,int height,uint format,uint pixelType,IntPtr output);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] private static extern IntPtr GetProcAddress(IntPtr module,string name);
        [DllImport("opengl32.dll")] private static extern void glPixelStorei(uint name,int value);
        [DllImport("opengl32.dll")] private static extern void glReadBuffer(uint mode);
        [DllImport("opengl32.dll")] private static extern void glTexImage2D(uint target,int level,int internalFormat,
            int width,int height,int border,uint pixelFormat,uint pixelType,IntPtr pixels);
        [DllImport("opengl32.dll")] private static extern void glEnable(uint flag);
        [DllImport("opengl32.dll")] private static extern void glBlendFunc(uint source,uint destination);
        private static bool ValidProc(IntPtr value)
        { long x=value.ToInt64(); return x!=0&&x!=1&&x!=2&&x!=3&&x!=-1; }

        /// <summary>所有函数指针仅在所属WGL context建立后加载，禁止全局复用另一设备的地址。</summary>
        private sealed class GlApi
        {
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint CreateShaderProc(uint type);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void ShaderSourceProc(uint id,int count,ref IntPtr sources,IntPtr lengths);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CompileShaderProc(uint shader);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GetShaderivProc(uint id,uint p,out int v);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GetShaderInfoLogProc(uint id,int capacity,out int length,StringBuilder log);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DeleteShaderProc(uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate uint CreateProgramProc();
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void AttachShaderProc(uint program,uint shader);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void LinkProgramProc(uint program);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GetProgramivProc(uint id,uint p,out int result);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GetProgramInfoLogProc(uint id,int capacity,out int length,StringBuilder log);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DeleteProgramProc(uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UseProgramProc(uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GenTexturesProc(int count,out uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DeleteTexturesProc(int count,ref uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void BindTextureProc(uint target,uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void ActiveTextureProc(uint unit);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void TexImage3DProc(uint target,int level,int format,int w,int h,int d,int border,uint pixFormat,uint type,IntPtr ptr);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void TexImage1DProc(uint target,int level,int format,int width,int border,uint pixFormat,uint type,IntPtr ptr);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void TexParameteriProc(uint target,uint param,int value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GenVertexArraysProc(int count,out uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DeleteVertexArraysProc(int count,ref uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void BindVertexArrayProc(uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void GenBuffersProc(int count,out uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DeleteBuffersProc(int count,ref uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void BindBufferProc(uint target,uint id);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void BufferDataProc(uint target,IntPtr bytes,IntPtr data,uint usage);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void VertexAttribPointerProc(uint index,int size,uint type,byte normalized,int stride,IntPtr offset);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void EnableVertexAttribArrayProc(uint index);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void DrawArraysProc(uint type,int start,int count);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetUniformLocationProc(uint program,[MarshalAs(UnmanagedType.LPStr)]string name);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UniformMatrix4fvProc(int location,int count,byte transpose,[In]float[] data);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Uniform3fProc(int location,float x,float y,float z);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Uniform1iProc(int location,int value);
            [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void Uniform1fProc(int location,float value);
            public readonly CreateShaderProc CreateShader=Load<CreateShaderProc>("glCreateShader");
            public readonly ShaderSourceProc ShaderSource=Load<ShaderSourceProc>("glShaderSource");
            public readonly CompileShaderProc CompileShader=Load<CompileShaderProc>("glCompileShader");
            public readonly GetShaderivProc GetShaderiv=Load<GetShaderivProc>("glGetShaderiv");
            public readonly GetShaderInfoLogProc GetShaderInfoLog=Load<GetShaderInfoLogProc>("glGetShaderInfoLog");
            public readonly DeleteShaderProc DeleteShader=Load<DeleteShaderProc>("glDeleteShader");
            public readonly CreateProgramProc CreateProgram=Load<CreateProgramProc>("glCreateProgram");
            public readonly AttachShaderProc AttachShader=Load<AttachShaderProc>("glAttachShader");
            public readonly LinkProgramProc LinkProgram=Load<LinkProgramProc>("glLinkProgram");
            public readonly GetProgramivProc GetProgramiv=Load<GetProgramivProc>("glGetProgramiv");
            public readonly GetProgramInfoLogProc GetProgramInfoLog=Load<GetProgramInfoLogProc>("glGetProgramInfoLog");
            public readonly DeleteProgramProc DeleteProgram=Load<DeleteProgramProc>("glDeleteProgram");
            public readonly UseProgramProc UseProgram=Load<UseProgramProc>("glUseProgram");
            public readonly GenTexturesProc GenTextures=Load<GenTexturesProc>("glGenTextures");
            public readonly DeleteTexturesProc DeleteTextures=Load<DeleteTexturesProc>("glDeleteTextures");
            public readonly BindTextureProc BindTexture=Load<BindTextureProc>("glBindTexture");
            public readonly ActiveTextureProc ActiveTexture=Load<ActiveTextureProc>("glActiveTexture");
            public readonly TexImage3DProc TexImage3D=Load<TexImage3DProc>("glTexImage3D");
            public readonly TexImage1DProc TexImage1D=Load<TexImage1DProc>("glTexImage1D");
            public readonly TexParameteriProc TexParameteri=Load<TexParameteriProc>("glTexParameteri");
            public readonly GenVertexArraysProc GenVertexArrays=Load<GenVertexArraysProc>("glGenVertexArrays");
            public readonly DeleteVertexArraysProc DeleteVertexArrays=Load<DeleteVertexArraysProc>("glDeleteVertexArrays");
            public readonly BindVertexArrayProc BindVertexArray=Load<BindVertexArrayProc>("glBindVertexArray");
            public readonly GenBuffersProc GenBuffers=Load<GenBuffersProc>("glGenBuffers");
            public readonly DeleteBuffersProc DeleteBuffers=Load<DeleteBuffersProc>("glDeleteBuffers");
            public readonly BindBufferProc BindBuffer=Load<BindBufferProc>("glBindBuffer");
            public readonly BufferDataProc BufferData=Load<BufferDataProc>("glBufferData");
            public readonly VertexAttribPointerProc VertexAttribPointer=Load<VertexAttribPointerProc>("glVertexAttribPointer");
            public readonly EnableVertexAttribArrayProc EnableVertexAttribArray=Load<EnableVertexAttribArrayProc>("glEnableVertexAttribArray");
            public readonly DrawArraysProc DrawArrays=Load<DrawArraysProc>("glDrawArrays");
            public readonly GetUniformLocationProc GetUniformLocation=Load<GetUniformLocationProc>("glGetUniformLocation");
            public readonly UniformMatrix4fvProc UniformMatrix4fv=Load<UniformMatrix4fvProc>("glUniformMatrix4fv");
            public readonly Uniform3fProc Uniform3f=Load<Uniform3fProc>("glUniform3f");
            public readonly Uniform1iProc Uniform1i=Load<Uniform1iProc>("glUniform1i");
            public readonly Uniform1fProc Uniform1f=Load<Uniform1fProc>("glUniform1f");
            private static T Load<T>(string name) where T:class
            {
                IntPtr addr=wglGetProcAddress(name);
                // Windows OpenGL 1.1核心函数必须从 opengl32.dll 导出表加载，驱动的WGL入口不保证返回它们。
                if(!ValidProc(addr))
                {
                    IntPtr module=GetModuleHandle("opengl32.dll");
                    if(module!=IntPtr.Zero)addr=GetProcAddress(module,name);
                }
                if(!ValidProc(addr))throw new NotSupportedException("OpenGL 3.3函数不可用: "+name);
                return (T)(object)Marshal.GetDelegateForFunctionPointer(addr,typeof(T));
            }
        }
    }
}
