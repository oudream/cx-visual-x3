using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using CxECTVolumeCore.Rendering;
using CxECTVolumeCore.Slicing;
using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace CxECTVolumeWinForms.Rendering
{
    // 控制器只负责编排相机、GPU 资源和各绘制通道。
    /// <summary>承载 OpenGL 体渲染、三维切面、轨迹球相机及其方向/传递函数叠加控件。</summary>
    public sealed class RenderingVolumeControl : UserControl
    {
        // OpenTK 3.3.3 的 GLControl 在 OpenTK 包内，命名空间是 OpenTK，不再使用 OpenTK.GLControl 包。
        private readonly GLControl _glControl;
        private readonly RenderingOrientationIndicatorControl _orientationIndicator;
        private readonly RenderingCamera _camera = new RenderingCamera();
        private readonly VolumeState _state;
        private VolumeData _volume;
        private RenderingIntensityProfile _intensityProfile;
        private readonly RenderingSettings _settings = new RenderingSettings();

        private RenderingOpenGlProgram _volumeShader;
        private RenderingOpenGlProgram _sliceShader;
        private RenderingOpenGlProgram _lineShader;
        private int _volumeTexture;
        private int _obliqueSliceTexture;
        private int _scalarTransferTexture;
        private int _gradientOpacityTexture;
        private int _cubeVao;
        private int _cubeVbo;
        private int _sliceVao;
        private int _sliceVbo;
        private int _lineVao;
        private int _lineVbo;
        // 相机用简单 yaw/pitch/distance 控制，足够覆盖当前查看器的旋转和缩放需求。
        // 虚拟轨迹球用四元数累积旋转，可跨越顶部和底部连续翻转，避免 yaw/pitch 在 ±90° 的万向节问题。
        private Point _lastMouse;
        private bool _rotating;
        private int _obliqueTextureWidth;
        private int _obliqueTextureHeight;

        public RenderingVolumeControl(VolumeData volume, VolumeState state)
        {
            _volume = volume;
            _state = state;
            _intensityProfile = RenderingIntensityProfile.FromVolume(volume, _settings.Intensity);

            _glControl = new GLControl(new GraphicsMode(32, 24, 0, 4), 3, 3, GraphicsContextFlags.ForwardCompatible)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };

            Controls.Add(_glControl);

            _orientationIndicator = new RenderingOrientationIndicatorControl(
                GetRotationMatrix,
                ResetCameraView)
            {
                // 保留原有箭头长度，仅收紧黑色画布的无效留白。
                Size = new Size(72, 72),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            };
            Controls.Add(_orientationIndicator);
            _orientationIndicator.BringToFront();

            Resize += delegate { PositionOverlayControls(); };
            PositionOverlayControls();

            _glControl.Load += OnGlLoad;
            _glControl.Paint += OnGlPaint;
            _glControl.Resize += OnGlResize;
            _glControl.MouseDown += OnMouseDown;
            _glControl.MouseMove += OnMouseMove;
            _glControl.MouseUp += OnMouseUp;
            _glControl.MouseWheel += OnMouseWheel;
            _state.PropertyChanged += OnVolumeStateChanged;
            _camera.Reset(GetPhysicalVolumeScale(), 1.0f);
        }

        public RenderingTransferFunctionSet TransferFunctions
        {
            get { return _settings.TransferFunctions; }
        }

        public void LoadVolume(VolumeData volume)
        {
            // --- 3D 路径入口：右下角视图收到新 VolumeData 后，只需要重建 3D 体纹理并触发重绘。
            Vector3 previousScale = GetPhysicalVolumeScale();
            _volume = volume;
            _intensityProfile = RenderingIntensityProfile.FromVolume(volume, _settings.Intensity);
            Vector3 currentScale = GetPhysicalVolumeScale();
            float aspect = Math.Max(1, _glControl.Width) / (float)Math.Max(1, _glControl.Height);
            // 渐进式 128→384 只提升纹理清晰度，不应把用户刚调整好的相机重置。
            if ((previousScale - currentScale).LengthSquared > 0.0001f)
            {
                _camera.Reset(currentScale, aspect);
                _orientationIndicator.Invalidate();
            }

            if (_glControl.Context == null)
            {
                return;
            }

            _glControl.MakeCurrent();
            if (_volumeTexture != 0)
            {
                GL.DeleteTexture(_volumeTexture);
                _volumeTexture = 0;
            }

            // 换数据后重建 3D 纹理，并按新数据分布刷新默认传递函数；shader 和几何资源继续复用。
            CreateVolumeTexture();
            UploadTransferTextures();
            _glControl.Invalidate();
        }

        public void UpdateRenderSettings(Action<RenderingSettings> update)
        {
            bool previousAutoRange = _settings.Intensity.AutoRange;
            int previousBackground = _settings.Intensity.BackgroundGrayUpper;
            float previousLowerPercentile = _settings.Intensity.LowerPercentile;
            float previousUpperPercentile = _settings.Intensity.UpperPercentile;
            update(_settings);
            if (previousAutoRange != _settings.Intensity.AutoRange ||
                previousBackground != _settings.Intensity.BackgroundGrayUpper ||
                Math.Abs(previousLowerPercentile - _settings.Intensity.LowerPercentile) > 0.0001f ||
                Math.Abs(previousUpperPercentile - _settings.Intensity.UpperPercentile) > 0.0001f)
            {
                _intensityProfile = RenderingIntensityProfile.FromVolume(_volume, _settings.Intensity);
            }

            if (_glControl.Context != null)
            {
                _glControl.MakeCurrent();
                UploadTransferTextures();
            }

            _glControl.Invalidate();
        }

        public Bitmap CaptureImage()
        {
            if (_glControl.Context == null ||
                _volumeShader == null ||
                _sliceShader == null ||
                _lineShader == null ||
                _glControl.Width <= 0 ||
                _glControl.Height <= 0)
            {
                return null;
            }

            _glControl.MakeCurrent();
            Render();
            var bitmap = new Bitmap(_glControl.Width, _glControl.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                GL.ReadPixels(0, 0, bitmap.Width, bitmap.Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            return bitmap;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _state.PropertyChanged -= OnVolumeStateChanged;

                if (_glControl.Context != null)
                {
                    _glControl.MakeCurrent();
                    DeleteGlResources();
                }

                _glControl.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnGlLoad(object sender, EventArgs e)
        {
            // --- OpenGL 控件创建好上下文后，才能创建 GPU 资源：3D 纹理、transfer 纹理、几何 VAO/VBO 和 shader。
            _glControl.MakeCurrent();
            // 纯黑背景提高半透明交截面和体渲染轮廓的对比度，接近工业 CT 查看器的视觉基调。
            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // OpenGL 资源必须在上下文创建之后初始化，不能放在 WinForms 构造函数里。
            CreateVolumeTexture();
            CreateTransferTextures();
            CreateGeometry();
            CreateShaders();
        }

        private void OnGlPaint(object sender, PaintEventArgs e)
        {
            if (_volumeShader == null || _sliceShader == null || _lineShader == null)
            {
                return;
            }

            // --- WinForms Paint 触发真正绘制：清屏后按各自开关绘制体、切面和可选外接框。
            _glControl.MakeCurrent();
            Render();
            _glControl.SwapBuffers();
        }

        private void OnGlResize(object sender, EventArgs e)
        {
            if (!_glControl.IsHandleCreated)
            {
                return;
            }

            _glControl.MakeCurrent();
            GL.Viewport(0, 0, Math.Max(1, _glControl.Width), Math.Max(1, _glControl.Height));
            _glControl.Invalidate();
            PositionOverlayControls();
        }

        private void PositionOverlayControls()
        {
            _orientationIndicator.Location = new Point(16, Math.Max(16, Height - _orientationIndicator.Height - 16));
            _orientationIndicator.BringToFront();
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _rotating = true;
            _lastMouse = e.Location;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_rotating)
            {
                return;
            }

            Point previous = _lastMouse;
            _lastMouse = e.Location;
            _camera.Rotate(previous, e.Location, _glControl.ClientSize);

            // 轨迹球基于每一段拖拽的相对旋转累积，因此上下没有角度上限，也不会跨越顶部后反向。
            _glControl.Invalidate();
            _orientationIndicator.Invalidate();
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _rotating = false;
            }
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            _camera.Zoom(e.Delta);
            _glControl.Invalidate();
        }

        public void SetObliqueSliceTexture(byte[] grayPixels, int width, int height)
        {
            if (grayPixels == null || grayPixels.Length != checked(width * height))
            {
                throw new ArgumentException("任意切纹理尺寸与像素数据不匹配。", nameof(grayPixels));
            }

            if (_glControl.Context == null)
            {
                return;
            }

            _glControl.MakeCurrent();
            if (_obliqueSliceTexture == 0)
            {
                _obliqueSliceTexture = GL.GenTexture();
            }

            GL.BindTexture(TextureTarget.Texture2D, _obliqueSliceTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, width, height, 0,
                PixelFormat.Red, PixelType.UnsignedByte, grayPixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _obliqueTextureWidth = width;
            _obliqueTextureHeight = height;
            _glControl.Invalidate();
        }

        public void ClearObliqueSliceTexture()
        {
            _obliqueTextureWidth = 0;
            _obliqueTextureHeight = 0;
            _glControl.Invalidate();
        }

        public void ResetCameraView()
        {
            float aspect = Math.Max(1, _glControl.Width) / (float)Math.Max(1, _glControl.Height);
            _camera.Reset(GetPhysicalVolumeScale(), aspect);
            _glControl.Invalidate();
            _orientationIndicator.Invalidate();
        }

        private Matrix4 GetRotationMatrix()
        {
            return _camera.RotationMatrix;
        }

        private void OnVolumeStateChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_glControl.IsDisposed || !_glControl.IsHandleCreated)
            {
                return;
            }

            _glControl.Invalidate();

            // HALCON 的滚轮消息结束后可能延后处理 Paint；切片位置变化时立即提交重绘，
            // 使 XY(SliceZ)、XZ(SliceY)、YZ(SliceX) 三个平面同步刷新到 3D 视图。
            if (e.PropertyName == "SliceX" ||
                e.PropertyName == "SliceY" ||
                e.PropertyName == "SliceZ" ||
                e.PropertyName == "ObliqueSlice" ||
                e.PropertyName == "All")
            {
                _glControl.Update();
            }
        }

        private void Render()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // --- model/view/projection 决定体数据如何投影到 3D 窗口；scale 使用原始尺寸和 spacing 保持真实比例。
            var aspect = Math.Max(1, _glControl.Width) / (float)Math.Max(1, _glControl.Height);
            var volumeScale = GetPhysicalVolumeScale();
            // 模型矩阵先按真实物理比例缩放，再应用用户旋转；这样降采样不会把薄板显示成正方体。
            var model = Matrix4.CreateScale(volumeScale.X, volumeScale.Y, volumeScale.Z) * GetRotationMatrix();
            var view = _camera.ViewMatrix;
            var projection = _camera.CreateProjection(aspect);
            var mvp = model * view * projection;
            var inverseModel = Matrix4.Invert(model);
            var cameraObject = TransformPosition(_camera.WorldPosition, inverseModel);

            if (_settings.ShowVolume)
            {
                DrawVolume(model, view, projection, cameraObject);
            }
            // 正交切面和任意切面独立显示；任意切开启时不能受正交开关影响。
            if (_settings.ShowSlices || _settings.ShowSliceTexture || _state.ObliqueSlice.Visible)
            {
                DrawSlices(mvp);
            }

            if (_settings.ShowVolumeBounds)
            {
                DrawCubeFrame(mvp);
            }
        }

        private Vector3 GetPhysicalVolumeScale()
        {
            var physicalWidth = Math.Max(1.0f, _volume.OriginalWidth * _volume.SpacingX);
            var physicalHeight = Math.Max(1.0f, _volume.OriginalHeight * _volume.SpacingY);
            var physicalDepth = Math.Max(1.0f, _volume.OriginalDepth * _volume.SpacingZ);
            var maxPhysicalSize = Math.Max(physicalWidth, Math.Max(physicalHeight, physicalDepth));

            // 降采样只改变纹理分辨率，不改变物体外形；这里按原始物理尺寸归一化显示。
            return new Vector3(
                physicalWidth / maxPhysicalSize,
                physicalHeight / maxPhysicalSize,
                physicalDepth / maxPhysicalSize);
        }

        private void DrawVolume(Matrix4 model, Matrix4 view, Matrix4 projection, Vector3 cameraObject)
        {
            // 体渲染本身是半透明累积，不能写入深度，否则内部切面会被包围盒前表面挡住。
            GL.DepthMask(false);
            // --- 体渲染绘制：shader 从相机方向穿过包围盒采样 volumeTex，并用 transfer texture 累积颜色和透明度。
            _volumeShader.Use();
            _volumeShader.SetMatrix4("model", model);
            _volumeShader.SetMatrix4("view", view);
            _volumeShader.SetMatrix4("projection", projection);
            _volumeShader.SetVector3("cameraObject", cameraObject);
            Vector3 voxelStep = new Vector3(1.0f / _volume.Width, 1.0f / _volume.Height, 1.0f / _volume.Depth);
            _volumeShader.SetVector3("voxelStep", voxelStep);
            _volumeShader.SetVector3("physicalScale", GetPhysicalVolumeScale());
            _volumeShader.SetInt("volumeTex", 0);
            _volumeShader.SetInt("scalarTransferTex", 1);
            _volumeShader.SetInt("gradientOpacityTex", 2);
            _volumeShader.SetFloat("opacityScale", _settings.VolumeOpacity);
            float maximumDimension = Math.Max(_volume.Width, Math.Max(_volume.Height, _volume.Depth));
            float rayStep = FrameworkCompat.Clamp(_settings.Sampling.StepScale / maximumDimension, 0.0009f, 0.012f);
            _volumeShader.SetFloat("rayStep", rayStep);
            _volumeShader.SetFloat("opacityReferenceStep", Math.Max(0.00001f, _settings.Sampling.ReferenceStep));
            _volumeShader.SetInt("enableJitter", _settings.Sampling.EnableJitter ? 1 : 0);
            _volumeShader.SetFloat("materialAmbient", FrameworkCompat.Clamp(_settings.Material.Ambient, 0.0f, 2.0f));
            _volumeShader.SetFloat("materialDiffuse", FrameworkCompat.Clamp(_settings.Material.Diffuse, 0.0f, 2.0f));
            _volumeShader.SetFloat("materialSpecular", FrameworkCompat.Clamp(_settings.Material.Specular, 0.0f, 2.0f));
            _volumeShader.SetFloat("materialSpecularPower", FrameworkCompat.Clamp(_settings.Material.SpecularPower, 1.0f, 128.0f));

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture3D, _volumeTexture);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture1D, _scalarTransferTexture);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture1D, _gradientOpacityTexture);
            GL.BindVertexArray(_cubeVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, RenderingGeometry.CubeVertices.Length / 3);
            GL.DepthMask(true);
        }

        private void DrawSlices(Matrix4 mvp)
        {
            // 正交三切面直接采样 3D 纹理；任意切在生成高清结果后可覆盖为独立 2D 纹理。
            var sliceVertices = BuildSliceVertices();
            var obliqueVisible = _state.ObliqueSlice.Visible;
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sliceVbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, new IntPtr(sliceVertices.Length * sizeof(float)), sliceVertices);

            // 切面是观察辅助面，始终显示在体数据中，便于确认 X/Y/Z 三方向的空间关系。
            GL.Disable(EnableCap.DepthTest);
            GL.DepthMask(false);
            _sliceShader.Use();
            _sliceShader.SetMatrix4("mvp", mvp);
            _sliceShader.SetInt("volumeTex", 0);
            _sliceShader.SetInt("obliqueTex", 1);
            _sliceShader.SetFloat("sliceOpacity", _settings.SliceOpacity);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture3D, _volumeTexture);
            GL.BindVertexArray(_sliceVao);

            // 原显示设置仅管理 XY/YZ/XZ 三张正交切面。
            if (_settings.ShowSlices || _settings.ShowSliceTexture)
            {
                _sliceShader.SetInt("showSliceTexture", _settings.ShowSliceTexture ? 1 : 0);
                _sliceShader.SetInt("showSlicePlanes", _settings.ShowSlices ? 1 : 0);
                _sliceShader.SetInt("useObliqueTexture", 0);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 18);
            }

            if (obliqueVisible)
            {
                bool hasObliqueTexture = _obliqueSliceTexture != 0 && _obliqueTextureWidth > 0 && _obliqueTextureHeight > 0;
                // 任意切的可见性由“显示任意切面”管理，始终显示实时 3D 预览或其高清纹理。
                _sliceShader.SetInt("showSliceTexture", 1);
                _sliceShader.SetInt("showSlicePlanes", 1);
                _sliceShader.SetInt("useObliqueTexture", hasObliqueTexture ? 1 : 0);
                if (hasObliqueTexture)
                {
                    GL.ActiveTexture(TextureUnit.Texture1);
                    GL.BindTexture(TextureTarget.Texture2D, _obliqueSliceTexture);
                }

                GL.DrawArrays(PrimitiveType.Triangles, 18, 6);
            }
            GL.DepthMask(true);
            GL.Enable(EnableCap.DepthTest);
        }

        private void DrawCubeFrame(Matrix4 mvp)
        {
            GL.Disable(EnableCap.DepthTest);
            _lineShader.Use();
            _lineShader.SetMatrix4("mvp", mvp);
            _lineShader.SetVector3("lineColor", new Vector3(0.82f, 0.88f, 0.95f));
            GL.BindVertexArray(_lineVao);
            GL.DrawArrays(PrimitiveType.Lines, 0, RenderingGeometry.CubeLines.Length / 3);
            GL.Enable(EnableCap.DepthTest);
        }

        private void CreateVolumeTexture()
        {
            // --- GPU 数据上传点：VolumeData.Voxels 是 byte[]，这里上传成单通道 R8 3D texture 供体渲染和切面 shader 采样。
            _volumeTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, _volumeTexture);
            // 体数据只上传一个灰度通道；颜色和透明度由 transfer texture 在 shader 中决定。
            GL.TexImage3D(
                TextureTarget.Texture3D,
                0,
                PixelInternalFormat.R8,
                _volume.Width,
                _volume.Height,
                _volume.Depth,
                0,
                PixelFormat.Red,
                PixelType.UnsignedByte,
                _volume.Voxels);

            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        }

        private void CreateTransferTextures()
        {
            _scalarTransferTexture = GL.GenTexture();
            _gradientOpacityTexture = GL.GenTexture();
            UploadTransferTextures();
        }

        private void UploadTransferTextures()
        {
            if (_scalarTransferTexture == 0 || _gradientOpacityTexture == 0)
            {
                return;
            }

            var scalarPixels = _settings.TransferFunctions.BuildScalarTransferTexture(
                _settings.Preset,
                _intensityProfile,
                _settings.Intensity.PresetOpacityScale);
            GL.BindTexture(TextureTarget.Texture1D, _scalarTransferTexture);
            // scalarTransferTex: 输入密度 0..1，输出 RGBA，A 已经包含阈值和标量不透明度。
            GL.TexImage1D(TextureTarget.Texture1D, 0, PixelInternalFormat.Rgba8, 256, 0, PixelFormat.Rgba, PixelType.UnsignedByte, scalarPixels);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);

            var gradientPixels = _settings.TransferFunctions.BuildGradientOpacityTexture();
            GL.BindTexture(TextureTarget.Texture1D, _gradientOpacityTexture);
            // gradientOpacityTex: 输入梯度强度 0..1，输出额外透明度，用于突出边缘结构。
            GL.TexImage1D(TextureTarget.Texture1D, 0, PixelInternalFormat.R8, 256, 0, PixelFormat.Red, PixelType.UnsignedByte, gradientPixels);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture1D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        }

        private void CreateGeometry()
        {
            _cubeVao = GL.GenVertexArray();
            _cubeVbo = GL.GenBuffer();
            GL.BindVertexArray(_cubeVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _cubeVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(RenderingGeometry.CubeVertices.Length * sizeof(float)), RenderingGeometry.CubeVertices, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);

            _sliceVao = GL.GenVertexArray();
            _sliceVbo = GL.GenBuffer();
            GL.BindVertexArray(_sliceVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _sliceVbo);
            // 三张正交切面加一张任意切面，最多 24 个顶点。
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(24 * 11 * sizeof(float)), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 11 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 11 * sizeof(float), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 11 * sizeof(float), 6 * sizeof(float));
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, 11 * sizeof(float), 9 * sizeof(float));
            GL.EnableVertexAttribArray(3);

            _lineVao = GL.GenVertexArray();
            _lineVbo = GL.GenBuffer();
            GL.BindVertexArray(_lineVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _lineVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, new IntPtr(RenderingGeometry.CubeLines.Length * sizeof(float)), RenderingGeometry.CubeLines, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
        }

        private void CreateShaders()
        {
            _volumeShader = new RenderingOpenGlProgram(RenderingShaderSources.VolumeVertex, RenderingShaderSources.VolumeFragment);
            _sliceShader = new RenderingOpenGlProgram(RenderingShaderSources.SliceVertex, RenderingShaderSources.SliceFragment);
            _lineShader = new RenderingOpenGlProgram(RenderingShaderSources.LineVertex, RenderingShaderSources.LineFragment);
        }

        private void DeleteGlResources()
        {
            if (_volumeShader != null) _volumeShader.Dispose();
            if (_sliceShader != null) _sliceShader.Dispose();
            if (_lineShader != null) _lineShader.Dispose();

            if (_volumeTexture != 0) GL.DeleteTexture(_volumeTexture);
            if (_obliqueSliceTexture != 0) GL.DeleteTexture(_obliqueSliceTexture);
            if (_scalarTransferTexture != 0) GL.DeleteTexture(_scalarTransferTexture);
            if (_gradientOpacityTexture != 0) GL.DeleteTexture(_gradientOpacityTexture);
            if (_cubeVao != 0) GL.DeleteVertexArray(_cubeVao);
            if (_cubeVbo != 0) GL.DeleteBuffer(_cubeVbo);
            if (_sliceVao != 0) GL.DeleteVertexArray(_sliceVao);
            if (_sliceVbo != 0) GL.DeleteBuffer(_sliceVbo);
            if (_lineVao != 0) GL.DeleteVertexArray(_lineVao);
            if (_lineVbo != 0) GL.DeleteBuffer(_lineVbo);
        }

        private float[] BuildSliceVertices()
        {
            // 顶点格式为 position.xyz + volumeTexCoord.xyz + planeColor.rgb + obliqueUv.xy。
            var x = _volume.OriginalToNormalizedX(_state.SliceX);
            var y = _volume.OriginalToNormalizedY(_state.SliceY);
            var z = _volume.OriginalToNormalizedZ(_state.SliceZ);
            var xyView = _state.GetSliceViewport(SlicePlane.XY);
            var xzView = _state.GetSliceViewport(SlicePlane.XZ);
            var yzView = _state.GetSliceViewport(SlicePlane.YZ);
            // --- xyView/xzView/yzView 是 2D 当前可视范围；范围可以超出 0..1，所以 3D 平面可大于体积外框。
            var px = x - 0.5f;
            var py = y - 0.5f;
            var pz = z - 0.5f;
            ObliqueSliceDefinition obliqueSlice = _state.ObliqueSlice;
            var vertices = new float[(18 + (obliqueSlice.Visible ? 6 : 0)) * 11];
            var offset = 0;

            // YZ 平面固定 X，用红色对应 X 切面。
            AddSliceVertex(vertices, ref offset, px, yzView.MinA - 0.5f, yzView.MinB - 0.5f, x, yzView.MinA, yzView.MinB, 1.0f, 0.22f, 0.20f);
            AddSliceVertex(vertices, ref offset, px, yzView.MaxA - 0.5f, yzView.MinB - 0.5f, x, yzView.MaxA, yzView.MinB, 1.0f, 0.22f, 0.20f);
            AddSliceVertex(vertices, ref offset, px, yzView.MaxA - 0.5f, yzView.MaxB - 0.5f, x, yzView.MaxA, yzView.MaxB, 1.0f, 0.22f, 0.20f);
            AddSliceVertex(vertices, ref offset, px, yzView.MinA - 0.5f, yzView.MinB - 0.5f, x, yzView.MinA, yzView.MinB, 1.0f, 0.22f, 0.20f);
            AddSliceVertex(vertices, ref offset, px, yzView.MaxA - 0.5f, yzView.MaxB - 0.5f, x, yzView.MaxA, yzView.MaxB, 1.0f, 0.22f, 0.20f);
            AddSliceVertex(vertices, ref offset, px, yzView.MinA - 0.5f, yzView.MaxB - 0.5f, x, yzView.MinA, yzView.MaxB, 1.0f, 0.22f, 0.20f);

            // XZ 平面固定 Y，用绿色对应 Y 切面。
            AddSliceVertex(vertices, ref offset, xzView.MinA - 0.5f, py, xzView.MinB - 0.5f, xzView.MinA, y, xzView.MinB, 0.28f, 0.85f, 0.32f);
            AddSliceVertex(vertices, ref offset, xzView.MaxA - 0.5f, py, xzView.MinB - 0.5f, xzView.MaxA, y, xzView.MinB, 0.28f, 0.85f, 0.32f);
            AddSliceVertex(vertices, ref offset, xzView.MaxA - 0.5f, py, xzView.MaxB - 0.5f, xzView.MaxA, y, xzView.MaxB, 0.28f, 0.85f, 0.32f);
            AddSliceVertex(vertices, ref offset, xzView.MinA - 0.5f, py, xzView.MinB - 0.5f, xzView.MinA, y, xzView.MinB, 0.28f, 0.85f, 0.32f);
            AddSliceVertex(vertices, ref offset, xzView.MaxA - 0.5f, py, xzView.MaxB - 0.5f, xzView.MaxA, y, xzView.MaxB, 0.28f, 0.85f, 0.32f);
            AddSliceVertex(vertices, ref offset, xzView.MinA - 0.5f, py, xzView.MaxB - 0.5f, xzView.MinA, y, xzView.MaxB, 0.28f, 0.85f, 0.32f);

            // XY 平面固定 Z，用蓝色对应 Z 切面。
            AddSliceVertex(vertices, ref offset, xyView.MinA - 0.5f, xyView.MinB - 0.5f, pz, xyView.MinA, xyView.MinB, z, 0.20f, 0.48f, 1.0f);
            AddSliceVertex(vertices, ref offset, xyView.MaxA - 0.5f, xyView.MinB - 0.5f, pz, xyView.MaxA, xyView.MinB, z, 0.20f, 0.48f, 1.0f);
            AddSliceVertex(vertices, ref offset, xyView.MaxA - 0.5f, xyView.MaxB - 0.5f, pz, xyView.MaxA, xyView.MaxB, z, 0.20f, 0.48f, 1.0f);
            AddSliceVertex(vertices, ref offset, xyView.MinA - 0.5f, xyView.MinB - 0.5f, pz, xyView.MinA, xyView.MinB, z, 0.20f, 0.48f, 1.0f);
            AddSliceVertex(vertices, ref offset, xyView.MaxA - 0.5f, xyView.MaxB - 0.5f, pz, xyView.MaxA, xyView.MaxB, z, 0.20f, 0.48f, 1.0f);
            AddSliceVertex(vertices, ref offset, xyView.MinA - 0.5f, xyView.MaxB - 0.5f, pz, xyView.MinA, xyView.MaxB, z, 0.20f, 0.48f, 1.0f);

            if (obliqueSlice.Visible)
            {
                // 任意切使用与 RAW 采样完全相同的旋转顺序和体素单位，避免平面位置与导出的切片不一致。
                ObliqueSliceGeometry.GetAxes(obliqueSlice, out ObliqueSliceVector axisU, out ObliqueSliceVector axisV);
                ObliqueSliceVector p00 = ObliqueSliceGeometry.GetCorner(obliqueSlice, axisU, axisV, -1.0, -1.0);
                ObliqueSliceVector p10 = ObliqueSliceGeometry.GetCorner(obliqueSlice, axisU, axisV, 1.0, -1.0);
                ObliqueSliceVector p11 = ObliqueSliceGeometry.GetCorner(obliqueSlice, axisU, axisV, 1.0, 1.0);
                ObliqueSliceVector p01 = ObliqueSliceGeometry.GetCorner(obliqueSlice, axisU, axisV, -1.0, 1.0);
                AddObliqueSliceVertex(vertices, ref offset, p00, 0f, 0f);
                AddObliqueSliceVertex(vertices, ref offset, p10, 1f, 0f);
                AddObliqueSliceVertex(vertices, ref offset, p11, 1f, 1f);
                AddObliqueSliceVertex(vertices, ref offset, p00, 0f, 0f);
                AddObliqueSliceVertex(vertices, ref offset, p11, 1f, 1f);
                AddObliqueSliceVertex(vertices, ref offset, p01, 0f, 1f);
            }

            return vertices;
        }

        private void AddObliqueSliceVertex(float[] vertices, ref int offset, ObliqueSliceVector voxelPosition, float u, float v)
        {
            float tx = ToUnboundedTextureCoordinate(voxelPosition.X, _volume.OriginalWidth);
            float ty = ToUnboundedTextureCoordinate(voxelPosition.Y, _volume.OriginalHeight);
            float tz = ToUnboundedTextureCoordinate(voxelPosition.Z, _volume.OriginalDepth);
            AddSliceVertex(vertices, ref offset, tx - 0.5f, ty - 0.5f, tz - 0.5f, tx, ty, tz, 1.0f, 0.76f, 0.18f, u, v);
        }

        private static float ToUnboundedTextureCoordinate(double coordinate, int count)
        {
            return (float)(coordinate / Math.Max(1, count - 1));
        }

        private static void AddSliceVertex(
            float[] vertices,
            ref int offset,
            float px,
            float py,
            float pz,
            float tx,
            float ty,
            float tz,
            float r,
            float g,
            float b,
            float u = 0f,
            float v = 0f)
        {
            vertices[offset++] = px;
            vertices[offset++] = py;
            vertices[offset++] = pz;
            vertices[offset++] = tx;
            vertices[offset++] = ty;
            vertices[offset++] = tz;
            vertices[offset++] = r;
            vertices[offset++] = g;
            vertices[offset++] = b;
            vertices[offset++] = u;
            vertices[offset++] = v;
        }

        private static Vector3 TransformPosition(Vector3 position, Matrix4 matrix)
        {
            var x = position.X * matrix.M11 + position.Y * matrix.M21 + position.Z * matrix.M31 + matrix.M41;
            var y = position.X * matrix.M12 + position.Y * matrix.M22 + position.Z * matrix.M32 + matrix.M42;
            var z = position.X * matrix.M13 + position.Y * matrix.M23 + position.Z * matrix.M33 + matrix.M43;
            var w = position.X * matrix.M14 + position.Y * matrix.M24 + position.Z * matrix.M34 + matrix.M44;
            if (Math.Abs(w) > 0.00001f)
            {
                x /= w;
                y /= w;
                z /= w;
            }

            return new Vector3(x, y, z);
        }

    }
}
