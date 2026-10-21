namespace CxECTImageWinForms.Controls
{
    partial class ImageViewerControl : System.Windows.Forms.UserControl
    {
        private System.Windows.Forms.TableLayoutPanel _rootLayout;
        private System.Windows.Forms.ToolStrip _toolbar;
        private System.Windows.Forms.ToolStripButton _openImageButton;
        private System.Windows.Forms.ToolStripButton _reopenImageButton;
        private System.Windows.Forms.ToolStripButton _fitButton;
        private System.Windows.Forms.ToolStripButton _actualSizeButton;
        private System.Windows.Forms.ToolStripButton _zoomInButton;
        private System.Windows.Forms.ToolStripButton _zoomOutButton;
        private System.Windows.Forms.ToolStripButton _resetPanButton;
        private System.Windows.Forms.ToolStripButton _closeImageButton;
        private System.Windows.Forms.ToolStripSeparator _viewToolbarSeparator;
        private System.Windows.Forms.ToolStripButton _editGeometryToolButton;
        private System.Windows.Forms.ToolStripButton _moveObjectToolButton;
        private System.Windows.Forms.ToolStripButton _deleteObjectToolButton;
        private System.Windows.Forms.ToolStripButton _navigateToolButton;
        private System.Windows.Forms.ToolStripButton _resetImageButton;
        private System.Windows.Forms.SplitContainer _contentSplit;
        private System.Windows.Forms.TableLayoutPanel _viewerRulerLayout;
        private System.Windows.Forms.Panel _viewerHost;
        private System.Windows.Forms.TextBox _annotationInputBox;
        private CxECTImageWinForms.Controls.Visualization.ImageRulerControl _topPixelRuler;
        private CxECTImageWinForms.Controls.Visualization.ImageRulerControl _leftPixelRuler;
        private CxECTImageWinForms.Controls.Visualization.ImageRulerControl _bottomMillimeterRuler;
        private CxECTImageWinForms.Controls.Visualization.ImageRulerControl _rightMillimeterRuler;
        private System.Windows.Forms.Label _designPlaceholder;
        private System.Windows.Forms.StatusStrip _statusBar;
        private System.Windows.Forms.ToolStripStatusLabel _zoomStatus;
        private System.Windows.Forms.ToolStripStatusLabel _imageStatus;
        private System.Windows.Forms.ToolStripStatusLabel _pixelStatus;
        private System.Windows.Forms.ToolStripStatusLabel _operationStatus;

        // 右侧面板是固定界面，必须在本 Designer 文件内声明和初始化，
        // 这样打开 WinForms 设计器即可完整查看和维护布局。
        private System.Windows.Forms.SplitContainer _toolPanelSplit;
        private System.Windows.Forms.Panel _displaySection;
        private System.Windows.Forms.Panel _displayHeader;
        private System.Windows.Forms.Panel _displayAccent;
        private System.Windows.Forms.Button _displaySettingsTabButton;
        private System.Windows.Forms.Label _displayTabSeparator;
        private System.Windows.Forms.Button _windowLevelTabButton;
        private System.Windows.Forms.Label _windowLevelTabSeparator;
        private System.Windows.Forms.Button _toneTabButton;
        private System.Windows.Forms.Label _toneTabSeparator;
        private System.Windows.Forms.Panel _displayPageHost;
        private System.Windows.Forms.Panel _displaySettingsPage;
        private System.Windows.Forms.Panel _windowLevelPage;
        private System.Windows.Forms.Panel _tonePage;
        private System.Windows.Forms.TableLayoutPanel _layerVisibilityLayout;
        private System.Windows.Forms.TableLayoutPanel _overlayColorLayout;
        private System.Windows.Forms.Label _overlayColorTitleLabel;
        private System.Windows.Forms.Button _roiColorButton;
        private System.Windows.Forms.Button _measurementColorButton;
        private System.Windows.Forms.Button _annotationColorButton;
        private System.Windows.Forms.Button _defectColorButton;
        private System.Windows.Forms.Button _selectedColorButton;
        private System.Windows.Forms.CheckBox _applyWindowLevelCheckBox;
        private System.Windows.Forms.CheckBox _autoWindowLevelRoiCheckBox;
        private System.Windows.Forms.Button _refreshWindowLevelButton;
        private System.Windows.Forms.Panel _windowLevelPanel;
        private System.Windows.Forms.TableLayoutPanel _windowLevelLayout;
        private System.Windows.Forms.Label _windowValueLabel;
        private System.Windows.Forms.NumericUpDown _windowValueEditor;
        private System.Windows.Forms.Label _windowMinimumLabel;
        private System.Windows.Forms.TrackBar _windowTrackBar;
        private System.Windows.Forms.Label _windowMaximumLabel;
        private System.Windows.Forms.Label _levelValueLabel;
        private System.Windows.Forms.NumericUpDown _levelValueEditor;
        private System.Windows.Forms.Label _levelMinimumLabel;
        private System.Windows.Forms.TrackBar _levelTrackBar;
        private System.Windows.Forms.Label _levelMaximumLabel;
        private System.Windows.Forms.CheckBox _autoContrastCheckBox;
        private System.Windows.Forms.CheckBox _invertCheckBox;
        private System.Windows.Forms.Panel _tonePanel;
        private System.Windows.Forms.TableLayoutPanel _toneLayout;
        private System.Windows.Forms.Label _gammaValueLabel;
        private System.Windows.Forms.Label _gammaMinimumLabel;
        private System.Windows.Forms.TrackBar _gammaTrackBar;
        private System.Windows.Forms.Label _gammaMaximumLabel;
        private System.Windows.Forms.Label _brightnessValueLabel;
        private System.Windows.Forms.Label _brightnessMinimumLabel;
        private System.Windows.Forms.TrackBar _brightnessTrackBar;
        private System.Windows.Forms.Label _brightnessMaximumLabel;
        private System.Windows.Forms.Label _contrastValueLabel;
        private System.Windows.Forms.Label _contrastMinimumLabel;
        private System.Windows.Forms.TrackBar _contrastTrackBar;
        private System.Windows.Forms.Label _contrastMaximumLabel;
        private System.Windows.Forms.Panel _transformPanel;
        private System.Windows.Forms.TableLayoutPanel _rotationCommandPanel;
        private System.Windows.Forms.Label _rotationAngleLabel;
        private System.Windows.Forms.NumericUpDown _rotationAngleInput;
        private System.Windows.Forms.Button _applyRotationButton;
        private System.Windows.Forms.TableLayoutPanel _transformCommandPanel;
        private System.Windows.Forms.Button _rotate90Button;
        private System.Windows.Forms.Button _rotateCounterClockwiseButton;
        private System.Windows.Forms.Button _flipHorizontalButton;
        private System.Windows.Forms.Button _flipVerticalButton;
        private System.Windows.Forms.Button _resetTransformButton;
        private System.Windows.Forms.Button _cropToRoiButton;
        private System.Windows.Forms.Panel _pixelAnalysisPage;
        private System.Windows.Forms.Panel _profileAnalysisPage;
        private System.Windows.Forms.Panel _statisticsAnalysisPanel;
        private System.Windows.Forms.Panel _profileAnalysisPanel;
        private System.Windows.Forms.Panel _pixelAnalysisActionHost;
        private System.Windows.Forms.FlowLayoutPanel _pixelAnalysisActionPanel;
        private System.Windows.Forms.Label _statisticsSummaryLabel;
        private CxECTImageWinForms.Controls.Visualization.GrayLevelScaleControl _grayLevelScale;
        private CxECTImageWinForms.Controls.Visualization.HistogramPlotControl _histogramPlot;
        private CxECTImageWinForms.Controls.Visualization.LineProfilePlotControl _lineProfilePlot;
        private System.Windows.Forms.Button _pixelStatisticsButton;
        private System.Windows.Forms.Button _histogramButton;
        private System.Windows.Forms.Button _lineProfileButton;
        private System.Windows.Forms.Panel _operationSection;
        private System.Windows.Forms.Panel _operationHeader;
        private System.Windows.Forms.Panel _operationAccent;
        private System.Windows.Forms.Button _roiTabButton;
        private System.Windows.Forms.Label _operationSeparator1;
        private System.Windows.Forms.Button _pixelAnalysisTabButton;
        private System.Windows.Forms.Label _operationSeparator2;
        private System.Windows.Forms.Button _profileAnalysisTabButton;
        private System.Windows.Forms.Label _operationSeparator3;
        private System.Windows.Forms.Button _transformTabButton;
        private System.Windows.Forms.Label _operationSeparator4;
        private System.Windows.Forms.Button _outputTabButton;
        private System.Windows.Forms.Button _measurementTabButton;
        private System.Windows.Forms.Label _operationSeparator5;
        private System.Windows.Forms.Button _annotationTabButton;
        private System.Windows.Forms.Panel _operationPageHost;
        private System.Windows.Forms.Panel _roiPage;
        private System.Windows.Forms.Panel _roiActionHost;
        private System.Windows.Forms.FlowLayoutPanel _roiActionPanel;
        private System.Windows.Forms.Button _rectangleRoiButton;
        private System.Windows.Forms.Button _clearRoiButton;
        private System.Windows.Forms.Panel _measurementPage;
        private System.Windows.Forms.Panel _measurementActionHost;
        private System.Windows.Forms.FlowLayoutPanel _measurementActionPanel;
        private System.Windows.Forms.Button _distanceButton;
        private System.Windows.Forms.Button _horizontalDistanceButton;
        private System.Windows.Forms.Button _verticalDistanceButton;
        private System.Windows.Forms.Button _angleButton;
        private System.Windows.Forms.Button _diameterButton;
        private System.Windows.Forms.Button _areaButton;
        private System.Windows.Forms.Button _clearMeasurementButton;
        private System.Windows.Forms.Panel _annotationPage;
        private System.Windows.Forms.Panel _annotationActionHost;
        private System.Windows.Forms.FlowLayoutPanel _annotationActionPanel;
        private System.Windows.Forms.Button _pointAnnotationButton;
        private System.Windows.Forms.Button _rectangleAnnotationButton;
        private System.Windows.Forms.Button _polygonAnnotationButton;
        private System.Windows.Forms.Button _textAnnotationButton;
        private System.Windows.Forms.Button _arrowAnnotationButton;
        private System.Windows.Forms.Button _defectBoxButton;
        private System.Windows.Forms.Button _clearAnnotationButton;
        private System.Windows.Forms.CheckBox _showImageRulerCheckBox;
        private System.Windows.Forms.CheckBox _showRoiCheckBox;
        private System.Windows.Forms.CheckBox _showMeasurementCheckBox;
        private System.Windows.Forms.CheckBox _showAnnotationCheckBox;
        private System.Windows.Forms.CheckBox _showDefectCheckBox;
        private System.Windows.Forms.Panel _outputPage;
        private System.Windows.Forms.Panel _outputActionHost;
        private System.Windows.Forms.FlowLayoutPanel _outputActionPanel;
        private System.Windows.Forms.Button _toggleImageButton;
        private System.Windows.Forms.Button _syncViewButton;
        private System.Windows.Forms.Button _blendImageButton;
        private System.Windows.Forms.Button _saveImageButton;
        private System.Windows.Forms.Button _copyClipboardButton;

        private void InitializeComponent()
        {
            this._rootLayout = new System.Windows.Forms.TableLayoutPanel();
            this._toolbar = new System.Windows.Forms.ToolStrip();
            this._openImageButton = new System.Windows.Forms.ToolStripButton();
            this._reopenImageButton = new System.Windows.Forms.ToolStripButton();
            this._fitButton = new System.Windows.Forms.ToolStripButton();
            this._actualSizeButton = new System.Windows.Forms.ToolStripButton();
            this._zoomInButton = new System.Windows.Forms.ToolStripButton();
            this._zoomOutButton = new System.Windows.Forms.ToolStripButton();
            this._resetPanButton = new System.Windows.Forms.ToolStripButton();
            this._closeImageButton = new System.Windows.Forms.ToolStripButton();
            this._viewToolbarSeparator = new System.Windows.Forms.ToolStripSeparator();
            this._editGeometryToolButton = new System.Windows.Forms.ToolStripButton();
            this._moveObjectToolButton = new System.Windows.Forms.ToolStripButton();
            this._deleteObjectToolButton = new System.Windows.Forms.ToolStripButton();
            this._navigateToolButton = new System.Windows.Forms.ToolStripButton();
            this._resetImageButton = new System.Windows.Forms.ToolStripButton();
            this._contentSplit = new System.Windows.Forms.SplitContainer();
            this._viewerRulerLayout = new System.Windows.Forms.TableLayoutPanel();
            this._topPixelRuler = new CxECTImageWinForms.Controls.Visualization.ImageRulerControl();
            this._leftPixelRuler = new CxECTImageWinForms.Controls.Visualization.ImageRulerControl();
            this._viewerHost = new System.Windows.Forms.Panel();
            this._annotationInputBox = new System.Windows.Forms.TextBox();
            this._designPlaceholder = new System.Windows.Forms.Label();
            this._rightMillimeterRuler = new CxECTImageWinForms.Controls.Visualization.ImageRulerControl();
            this._bottomMillimeterRuler = new CxECTImageWinForms.Controls.Visualization.ImageRulerControl();
            this._toolPanelSplit = new System.Windows.Forms.SplitContainer();
            this._displaySection = new System.Windows.Forms.Panel();
            this._displayPageHost = new System.Windows.Forms.Panel();
            this._windowLevelPage = new System.Windows.Forms.Panel();
            this._windowLevelPanel = new System.Windows.Forms.Panel();
            this._windowLevelLayout = new System.Windows.Forms.TableLayoutPanel();
            this._applyWindowLevelCheckBox = new System.Windows.Forms.CheckBox();
            this._autoWindowLevelRoiCheckBox = new System.Windows.Forms.CheckBox();
            this._windowValueLabel = new System.Windows.Forms.Label();
            this._windowValueEditor = new System.Windows.Forms.NumericUpDown();
            this._windowMinimumLabel = new System.Windows.Forms.Label();
            this._windowTrackBar = new System.Windows.Forms.TrackBar();
            this._windowMaximumLabel = new System.Windows.Forms.Label();
            this._levelValueLabel = new System.Windows.Forms.Label();
            this._levelValueEditor = new System.Windows.Forms.NumericUpDown();
            this._levelMinimumLabel = new System.Windows.Forms.Label();
            this._levelTrackBar = new System.Windows.Forms.TrackBar();
            this._levelMaximumLabel = new System.Windows.Forms.Label();
            this._refreshWindowLevelButton = new System.Windows.Forms.Button();
            this._measurementPage = new System.Windows.Forms.Panel();
            this._measurementActionHost = new System.Windows.Forms.Panel();
            this._measurementActionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._distanceButton = new System.Windows.Forms.Button();
            this._horizontalDistanceButton = new System.Windows.Forms.Button();
            this._verticalDistanceButton = new System.Windows.Forms.Button();
            this._angleButton = new System.Windows.Forms.Button();
            this._diameterButton = new System.Windows.Forms.Button();
            this._areaButton = new System.Windows.Forms.Button();
            this._clearMeasurementButton = new System.Windows.Forms.Button();
            this._tonePage = new System.Windows.Forms.Panel();
            this._tonePanel = new System.Windows.Forms.Panel();
            this._toneLayout = new System.Windows.Forms.TableLayoutPanel();
            this._autoContrastCheckBox = new System.Windows.Forms.CheckBox();
            this._invertCheckBox = new System.Windows.Forms.CheckBox();
            this._gammaValueLabel = new System.Windows.Forms.Label();
            this._gammaMinimumLabel = new System.Windows.Forms.Label();
            this._gammaTrackBar = new System.Windows.Forms.TrackBar();
            this._gammaMaximumLabel = new System.Windows.Forms.Label();
            this._brightnessValueLabel = new System.Windows.Forms.Label();
            this._brightnessMinimumLabel = new System.Windows.Forms.Label();
            this._brightnessTrackBar = new System.Windows.Forms.TrackBar();
            this._brightnessMaximumLabel = new System.Windows.Forms.Label();
            this._contrastValueLabel = new System.Windows.Forms.Label();
            this._contrastMinimumLabel = new System.Windows.Forms.Label();
            this._contrastTrackBar = new System.Windows.Forms.TrackBar();
            this._contrastMaximumLabel = new System.Windows.Forms.Label();
            this._annotationPage = new System.Windows.Forms.Panel();
            this._annotationActionHost = new System.Windows.Forms.Panel();
            this._annotationActionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._pointAnnotationButton = new System.Windows.Forms.Button();
            this._rectangleAnnotationButton = new System.Windows.Forms.Button();
            this._polygonAnnotationButton = new System.Windows.Forms.Button();
            this._textAnnotationButton = new System.Windows.Forms.Button();
            this._arrowAnnotationButton = new System.Windows.Forms.Button();
            this._defectBoxButton = new System.Windows.Forms.Button();
            this._clearAnnotationButton = new System.Windows.Forms.Button();
            this._displaySettingsPage = new System.Windows.Forms.Panel();
            this._layerVisibilityLayout = new System.Windows.Forms.TableLayoutPanel();
            this._showImageRulerCheckBox = new System.Windows.Forms.CheckBox();
            this._showRoiCheckBox = new System.Windows.Forms.CheckBox();
            this._showMeasurementCheckBox = new System.Windows.Forms.CheckBox();
            this._showAnnotationCheckBox = new System.Windows.Forms.CheckBox();
            this._showDefectCheckBox = new System.Windows.Forms.CheckBox();
            this._overlayColorLayout = new System.Windows.Forms.TableLayoutPanel();
            this._overlayColorTitleLabel = new System.Windows.Forms.Label();
            this._roiColorButton = new System.Windows.Forms.Button();
            this._measurementColorButton = new System.Windows.Forms.Button();
            this._annotationColorButton = new System.Windows.Forms.Button();
            this._defectColorButton = new System.Windows.Forms.Button();
            this._selectedColorButton = new System.Windows.Forms.Button();
            this._displayHeader = new System.Windows.Forms.Panel();
            this._displayAccent = new System.Windows.Forms.Panel();
            this._displaySettingsTabButton = new System.Windows.Forms.Button();
            this._displayTabSeparator = new System.Windows.Forms.Label();
            this._windowLevelTabButton = new System.Windows.Forms.Button();
            this._windowLevelTabSeparator = new System.Windows.Forms.Label();
            this._toneTabButton = new System.Windows.Forms.Button();
            this._toneTabSeparator = new System.Windows.Forms.Label();
            this._measurementTabButton = new System.Windows.Forms.Button();
            this._operationSeparator1 = new System.Windows.Forms.Label();
            this._annotationTabButton = new System.Windows.Forms.Button();
            this._operationSection = new System.Windows.Forms.Panel();
            this._operationPageHost = new System.Windows.Forms.Panel();
            this._transformPanel = new System.Windows.Forms.Panel();
            this._rotationCommandPanel = new System.Windows.Forms.TableLayoutPanel();
            this._rotationAngleLabel = new System.Windows.Forms.Label();
            this._applyRotationButton = new System.Windows.Forms.Button();
            this._rotationAngleInputPanel = new System.Windows.Forms.Panel();
            this._rotationAngleInput = new System.Windows.Forms.NumericUpDown();
            this._transformCommandPanel = new System.Windows.Forms.TableLayoutPanel();
            this._rotate90Button = new System.Windows.Forms.Button();
            this._flipHorizontalButton = new System.Windows.Forms.Button();
            this._flipVerticalButton = new System.Windows.Forms.Button();
            this._rotateCounterClockwiseButton = new System.Windows.Forms.Button();
            this._resetTransformButton = new System.Windows.Forms.Button();
            this._cropToRoiButton = new System.Windows.Forms.Button();
            this._outputPage = new System.Windows.Forms.Panel();
            this._outputActionHost = new System.Windows.Forms.Panel();
            this._outputActionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._toggleImageButton = new System.Windows.Forms.Button();
            this._syncViewButton = new System.Windows.Forms.Button();
            this._blendImageButton = new System.Windows.Forms.Button();
            this._saveImageButton = new System.Windows.Forms.Button();
            this._copyClipboardButton = new System.Windows.Forms.Button();
            this._pixelAnalysisPage = new System.Windows.Forms.Panel();
            this._statisticsAnalysisPanel = new System.Windows.Forms.Panel();
            this._pixelAnalysisActionHost = new System.Windows.Forms.Panel();
            this._histogramPlot = new CxECTImageWinForms.Controls.Visualization.HistogramPlotControl();
            this._grayLevelScale = new CxECTImageWinForms.Controls.Visualization.GrayLevelScaleControl();
            this._statisticsSummaryLabel = new System.Windows.Forms.Label();
            this._pixelAnalysisActionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._pixelStatisticsButton = new System.Windows.Forms.Button();
            this._histogramButton = new System.Windows.Forms.Button();
            this._roiPage = new System.Windows.Forms.Panel();
            this._roiActionHost = new System.Windows.Forms.Panel();
            this._roiActionPanel = new System.Windows.Forms.FlowLayoutPanel();
            this._rectangleRoiButton = new System.Windows.Forms.Button();
            this._clearRoiButton = new System.Windows.Forms.Button();
            this._profileAnalysisPage = new System.Windows.Forms.Panel();
            this._profileAnalysisPanel = new System.Windows.Forms.Panel();
            this._lineProfilePlot = new CxECTImageWinForms.Controls.Visualization.LineProfilePlotControl();
            this._lineProfileButton = new System.Windows.Forms.Button();
            this._operationHeader = new System.Windows.Forms.Panel();
            this._operationAccent = new System.Windows.Forms.Panel();
            this._roiTabButton = new System.Windows.Forms.Button();
            this._operationSeparator2 = new System.Windows.Forms.Label();
            this._pixelAnalysisTabButton = new System.Windows.Forms.Button();
            this._operationSeparator3 = new System.Windows.Forms.Label();
            this._profileAnalysisTabButton = new System.Windows.Forms.Button();
            this._operationSeparator4 = new System.Windows.Forms.Label();
            this._transformTabButton = new System.Windows.Forms.Button();
            this._operationSeparator5 = new System.Windows.Forms.Label();
            this._outputTabButton = new System.Windows.Forms.Button();
            this._statusBar = new System.Windows.Forms.StatusStrip();
            this._zoomStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._imageStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._pixelStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._operationStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this._rootLayout.SuspendLayout();
            this._toolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._contentSplit)).BeginInit();
            this._contentSplit.Panel1.SuspendLayout();
            this._contentSplit.Panel2.SuspendLayout();
            this._contentSplit.SuspendLayout();
            this._viewerRulerLayout.SuspendLayout();
            this._viewerHost.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._toolPanelSplit)).BeginInit();
            this._toolPanelSplit.Panel1.SuspendLayout();
            this._toolPanelSplit.Panel2.SuspendLayout();
            this._toolPanelSplit.SuspendLayout();
            this._displaySection.SuspendLayout();
            this._displayPageHost.SuspendLayout();
            this._windowLevelPage.SuspendLayout();
            this._windowLevelPanel.SuspendLayout();
            this._windowLevelLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._windowValueEditor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._windowTrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._levelValueEditor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._levelTrackBar)).BeginInit();
            this._measurementPage.SuspendLayout();
            this._measurementActionHost.SuspendLayout();
            this._measurementActionPanel.SuspendLayout();
            this._tonePage.SuspendLayout();
            this._tonePanel.SuspendLayout();
            this._toneLayout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._gammaTrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._brightnessTrackBar)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._contrastTrackBar)).BeginInit();
            this._annotationPage.SuspendLayout();
            this._annotationActionHost.SuspendLayout();
            this._annotationActionPanel.SuspendLayout();
            this._displaySettingsPage.SuspendLayout();
            this._layerVisibilityLayout.SuspendLayout();
            this._overlayColorLayout.SuspendLayout();
            this._displayHeader.SuspendLayout();
            this._operationSection.SuspendLayout();
            this._operationPageHost.SuspendLayout();
            this._transformPanel.SuspendLayout();
            this._rotationCommandPanel.SuspendLayout();
            this._rotationAngleInputPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._rotationAngleInput)).BeginInit();
            this._transformCommandPanel.SuspendLayout();
            this._outputPage.SuspendLayout();
            this._outputActionHost.SuspendLayout();
            this._outputActionPanel.SuspendLayout();
            this._pixelAnalysisPage.SuspendLayout();
            this._statisticsAnalysisPanel.SuspendLayout();
            this._pixelAnalysisActionHost.SuspendLayout();
            this._pixelAnalysisActionPanel.SuspendLayout();
            this._roiPage.SuspendLayout();
            this._roiActionHost.SuspendLayout();
            this._roiActionPanel.SuspendLayout();
            this._profileAnalysisPage.SuspendLayout();
            this._profileAnalysisPanel.SuspendLayout();
            this._operationHeader.SuspendLayout();
            this._statusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // _rootLayout
            // 
            this._rootLayout.ColumnCount = 1;
            this._rootLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._rootLayout.Controls.Add(this._toolbar, 0, 0);
            this._rootLayout.Controls.Add(this._contentSplit, 0, 1);
            this._rootLayout.Controls.Add(this._statusBar, 0, 2);
            this._rootLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rootLayout.Location = new System.Drawing.Point(0, 0);
            this._rootLayout.Margin = new System.Windows.Forms.Padding(0);
            this._rootLayout.Name = "_rootLayout";
            this._rootLayout.RowCount = 3;
            this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._rootLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this._rootLayout.Size = new System.Drawing.Size(1580, 1038);
            this._rootLayout.TabIndex = 0;
            // 
            // _toolbar
            // 
            this._toolbar.AutoSize = false;
            this._toolbar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._toolbar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._toolbar.Font = new System.Drawing.Font("Microsoft YaHei UI", 8F);
            this._toolbar.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this._toolbar.ImageScalingSize = new System.Drawing.Size(28, 28);
            this._toolbar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._openImageButton,
            this._reopenImageButton,
            this._fitButton,
            this._actualSizeButton,
            this._zoomInButton,
            this._zoomOutButton,
            this._resetPanButton,
            this._closeImageButton,
            this._viewToolbarSeparator,
            this._editGeometryToolButton,
            this._moveObjectToolButton,
            this._deleteObjectToolButton,
            this._navigateToolButton,
            this._resetImageButton});
            this._toolbar.Location = new System.Drawing.Point(0, 0);
            this._toolbar.Name = "_toolbar";
            this._toolbar.Padding = new System.Windows.Forms.Padding(8, 2, 8, 2);
            this._toolbar.Size = new System.Drawing.Size(1580, 64);
            this._toolbar.TabIndex = 0;
            // 
            // _openImageButton
            // 
            this._openImageButton.Image = global::CxECTImageWinForms.Properties.Resources._01_打开图像_文件夹;
            this._openImageButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._openImageButton.Name = "_openImageButton";
            this._openImageButton.Size = new System.Drawing.Size(55, 57);
            this._openImageButton.Text = "打开图像";
            this._openImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._openImageButton.ToolTipText = "打开 BMP、PNG、JPEG 或 TIFF 图像";
            // 
            // _reopenImageButton
            // 
            this._reopenImageButton.Enabled = false;
            this._reopenImageButton.Image = global::CxECTImageWinForms.Properties.Resources._06_刷新图像_循环箭头;
            this._reopenImageButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._reopenImageButton.Name = "_reopenImageButton";
            this._reopenImageButton.Size = new System.Drawing.Size(55, 57);
            this._reopenImageButton.Text = "重新打开";
            this._reopenImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._reopenImageButton.ToolTipText = "重新读取上次成功打开的图像文件";
            // 
            // _fitButton
            // 
            this._fitButton.Image = global::CxECTImageWinForms.Properties.Resources._02_适应窗口_四角;
            this._fitButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._fitButton.Name = "_fitButton";
            this._fitButton.Size = new System.Drawing.Size(55, 57);
            this._fitButton.Text = "适应窗口";
            this._fitButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._fitButton.ToolTipText = "适应窗口（完整显示图像）";
            // 
            // _actualSizeButton
            // 
            this._actualSizeButton.Image = global::CxECTImageWinForms.Properties.Resources._03_原始比例_一比一;
            this._actualSizeButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._actualSizeButton.Name = "_actualSizeButton";
            this._actualSizeButton.Size = new System.Drawing.Size(55, 57);
            this._actualSizeButton.Text = "实际大小";
            this._actualSizeButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._actualSizeButton.ToolTipText = "实际大小（1:1 像素显示）";
            // 
            // _zoomInButton
            // 
            this._zoomInButton.Image = global::CxECTImageWinForms.Properties.Resources._04_放大图像_放大镜加;
            this._zoomInButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._zoomInButton.Name = "_zoomInButton";
            this._zoomInButton.Size = new System.Drawing.Size(55, 57);
            this._zoomInButton.Text = "放大图像";
            this._zoomInButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._zoomInButton.ToolTipText = "放大图像";
            // 
            // _zoomOutButton
            // 
            this._zoomOutButton.Image = global::CxECTImageWinForms.Properties.Resources._05_缩小图像_放大镜减;
            this._zoomOutButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._zoomOutButton.Name = "_zoomOutButton";
            this._zoomOutButton.Size = new System.Drawing.Size(55, 57);
            this._zoomOutButton.Text = "缩小图像";
            this._zoomOutButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._zoomOutButton.ToolTipText = "缩小图像";
            // 
            // _resetPanButton
            // 
            this._resetPanButton.Image = global::CxECTImageWinForms.Properties.Resources._07_位置复位_居中十字;
            this._resetPanButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._resetPanButton.Name = "_resetPanButton";
            this._resetPanButton.Size = new System.Drawing.Size(55, 57);
            this._resetPanButton.Text = "位置复位";
            this._resetPanButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._resetPanButton.ToolTipText = "复位图像位置";
            // 
            // _closeImageButton
            // 
            this._closeImageButton.Image = global::CxECTImageWinForms.Properties.Resources._08_清空图像_图像清除;
            this._closeImageButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._closeImageButton.Name = "_closeImageButton";
            this._closeImageButton.Size = new System.Drawing.Size(55, 57);
            this._closeImageButton.Text = "关闭图像";
            this._closeImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._closeImageButton.ToolTipText = "关闭当前图像（将要求确认）";
            // 
            // _viewToolbarSeparator
            // 
            this._viewToolbarSeparator.Name = "_viewToolbarSeparator";
            this._viewToolbarSeparator.Size = new System.Drawing.Size(6, 60);
            // 
            // _editGeometryToolButton
            // 
            this._editGeometryToolButton.Image = global::CxECTImageWinForms.Properties.Resources._03_缩放ROI_对角缩放;
            this._editGeometryToolButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._editGeometryToolButton.Name = "_editGeometryToolButton";
            this._editGeometryToolButton.Size = new System.Drawing.Size(55, 57);
            this._editGeometryToolButton.Text = "编辑几何";
            this._editGeometryToolButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._editGeometryToolButton.ToolTipText = "编辑几何（Ctrl+E）";
            // 
            // _moveObjectToolButton
            // 
            this._moveObjectToolButton.Image = global::CxECTImageWinForms.Properties.Resources._02_移动ROI_四向拖拽;
            this._moveObjectToolButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._moveObjectToolButton.Name = "_moveObjectToolButton";
            this._moveObjectToolButton.Size = new System.Drawing.Size(55, 57);
            this._moveObjectToolButton.Text = "移动对象";
            this._moveObjectToolButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._moveObjectToolButton.ToolTipText = "移动对象（Ctrl+M）";
            // 
            // _deleteObjectToolButton
            // 
            this._deleteObjectToolButton.Image = global::CxECTImageWinForms.Properties.Resources._04_删除ROI;
            this._deleteObjectToolButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._deleteObjectToolButton.Name = "_deleteObjectToolButton";
            this._deleteObjectToolButton.Size = new System.Drawing.Size(55, 57);
            this._deleteObjectToolButton.Text = "删除对象";
            this._deleteObjectToolButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._deleteObjectToolButton.ToolTipText = "删除对象（Ctrl+D）";
            // 
            // _navigateToolButton
            // 
            this._navigateToolButton.Image = global::CxECTImageWinForms.Properties.Resources._01_恢复浏览_拖动图像;
            this._navigateToolButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._navigateToolButton.Name = "_navigateToolButton";
            this._navigateToolButton.Size = new System.Drawing.Size(55, 57);
            this._navigateToolButton.Text = "浏览图像";
            this._navigateToolButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._navigateToolButton.ToolTipText = "恢复浏览、拖动图像（Ctrl+R）";
            // 
            // _resetImageButton
            // 
            this._resetImageButton.Image = global::CxECTImageWinForms.Properties.Resources._04_重置变换;
            this._resetImageButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this._resetImageButton.Name = "_resetImageButton";
            this._resetImageButton.Size = new System.Drawing.Size(55, 57);
            this._resetImageButton.Text = "重置图像";
            this._resetImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._resetImageButton.ToolTipText = "切回原图，并复位视图、显示与图像变换（保留标注）";
            // 
            // _contentSplit
            // 
            this._contentSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contentSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this._contentSplit.Location = new System.Drawing.Point(3, 67);
            this._contentSplit.Name = "_contentSplit";
            // 
            // _contentSplit.Panel1
            // 
            this._contentSplit.Panel1.Controls.Add(this._viewerRulerLayout);
            // 
            // _contentSplit.Panel2
            // 
            this._contentSplit.Panel2.Controls.Add(this._toolPanelSplit);
            this._contentSplit.Panel2MinSize = 360;
            this._contentSplit.Size = new System.Drawing.Size(1574, 944);
            this._contentSplit.SplitterDistance = 1181;
            this._contentSplit.SplitterWidth = 5;
            this._contentSplit.TabIndex = 1;
            // 
            // _viewerRulerLayout
            // 
            this._viewerRulerLayout.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(230)))), ((int)(((byte)(238)))));
            this._viewerRulerLayout.ColumnCount = 3;
            this._viewerRulerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this._viewerRulerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._viewerRulerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this._viewerRulerLayout.Controls.Add(this._topPixelRuler, 1, 0);
            this._viewerRulerLayout.Controls.Add(this._leftPixelRuler, 0, 1);
            this._viewerRulerLayout.Controls.Add(this._viewerHost, 1, 1);
            this._viewerRulerLayout.Controls.Add(this._rightMillimeterRuler, 2, 1);
            this._viewerRulerLayout.Controls.Add(this._bottomMillimeterRuler, 1, 2);
            this._viewerRulerLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._viewerRulerLayout.Location = new System.Drawing.Point(0, 0);
            this._viewerRulerLayout.Name = "_viewerRulerLayout";
            this._viewerRulerLayout.RowCount = 3;
            this._viewerRulerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this._viewerRulerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._viewerRulerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this._viewerRulerLayout.Size = new System.Drawing.Size(1181, 944);
            this._viewerRulerLayout.TabIndex = 0;
            // 
            // _topPixelRuler
            // 
            this._topPixelRuler.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this._topPixelRuler.Dock = System.Windows.Forms.DockStyle.Fill;
            this._topPixelRuler.Font = new System.Drawing.Font("Microsoft YaHei UI", 7F);
            this._topPixelRuler.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(96)))), ((int)(((byte)(112)))));
            this._topPixelRuler.Location = new System.Drawing.Point(22, 0);
            this._topPixelRuler.Margin = new System.Windows.Forms.Padding(0);
            this._topPixelRuler.Name = "_topPixelRuler";
            this._topPixelRuler.Orientation = CxECTImageWinForms.Controls.Visualization.ImageRulerOrientation.Horizontal;
            this._topPixelRuler.Size = new System.Drawing.Size(1137, 22);
            this._topPixelRuler.TabIndex = 0;
            // 
            // _leftPixelRuler
            // 
            this._leftPixelRuler.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this._leftPixelRuler.Dock = System.Windows.Forms.DockStyle.Fill;
            this._leftPixelRuler.Font = new System.Drawing.Font("Microsoft YaHei UI", 7F);
            this._leftPixelRuler.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(96)))), ((int)(((byte)(112)))));
            this._leftPixelRuler.Location = new System.Drawing.Point(0, 22);
            this._leftPixelRuler.Margin = new System.Windows.Forms.Padding(0);
            this._leftPixelRuler.Name = "_leftPixelRuler";
            this._leftPixelRuler.Orientation = CxECTImageWinForms.Controls.Visualization.ImageRulerOrientation.Vertical;
            this._leftPixelRuler.Size = new System.Drawing.Size(22, 900);
            this._leftPixelRuler.TabIndex = 1;
            // 
            // _viewerHost
            // 
            this._viewerHost.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(13)))), ((int)(((byte)(15)))), ((int)(((byte)(18)))));
            this._viewerHost.Controls.Add(this._annotationInputBox);
            this._viewerHost.Controls.Add(this._designPlaceholder);
            this._viewerHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._viewerHost.Location = new System.Drawing.Point(22, 22);
            this._viewerHost.Margin = new System.Windows.Forms.Padding(0);
            this._viewerHost.Name = "_viewerHost";
            this._viewerHost.Size = new System.Drawing.Size(1137, 900);
            this._viewerHost.TabIndex = 0;
            // 
            // _annotationInputBox
            // 
            this._annotationInputBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._annotationInputBox.Location = new System.Drawing.Point(0, 0);
            this._annotationInputBox.Name = "_annotationInputBox";
            this._annotationInputBox.Size = new System.Drawing.Size(180, 23);
            this._annotationInputBox.TabIndex = 1;
            this._annotationInputBox.Visible = false;
            // 
            // _designPlaceholder
            // 
            this._designPlaceholder.Dock = System.Windows.Forms.DockStyle.Fill;
            this._designPlaceholder.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(190)))), ((int)(((byte)(200)))), ((int)(((byte)(210)))));
            this._designPlaceholder.Location = new System.Drawing.Point(0, 0);
            this._designPlaceholder.Name = "_designPlaceholder";
            this._designPlaceholder.Size = new System.Drawing.Size(1137, 900);
            this._designPlaceholder.TabIndex = 0;
            this._designPlaceholder.Text = "ImageViewerControl 2D 图像窗口（运行时初始化 HALCON）";
            this._designPlaceholder.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _rightMillimeterRuler
            // 
            this._rightMillimeterRuler.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this._rightMillimeterRuler.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rightMillimeterRuler.Font = new System.Drawing.Font("Microsoft YaHei UI", 7F);
            this._rightMillimeterRuler.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(96)))), ((int)(((byte)(112)))));
            this._rightMillimeterRuler.Location = new System.Drawing.Point(1159, 22);
            this._rightMillimeterRuler.Margin = new System.Windows.Forms.Padding(0);
            this._rightMillimeterRuler.Name = "_rightMillimeterRuler";
            this._rightMillimeterRuler.Orientation = CxECTImageWinForms.Controls.Visualization.ImageRulerOrientation.Vertical;
            this._rightMillimeterRuler.Size = new System.Drawing.Size(22, 900);
            this._rightMillimeterRuler.TabIndex = 2;
            // 
            // _bottomMillimeterRuler
            // 
            this._bottomMillimeterRuler.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this._bottomMillimeterRuler.Dock = System.Windows.Forms.DockStyle.Fill;
            this._bottomMillimeterRuler.Font = new System.Drawing.Font("Microsoft YaHei UI", 7F);
            this._bottomMillimeterRuler.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(86)))), ((int)(((byte)(96)))), ((int)(((byte)(112)))));
            this._bottomMillimeterRuler.Location = new System.Drawing.Point(22, 922);
            this._bottomMillimeterRuler.Margin = new System.Windows.Forms.Padding(0);
            this._bottomMillimeterRuler.Name = "_bottomMillimeterRuler";
            this._bottomMillimeterRuler.Orientation = CxECTImageWinForms.Controls.Visualization.ImageRulerOrientation.Horizontal;
            this._bottomMillimeterRuler.Size = new System.Drawing.Size(1137, 22);
            this._bottomMillimeterRuler.TabIndex = 3;
            // 
            // _toolPanelSplit
            // 
            this._toolPanelSplit.Dock = System.Windows.Forms.DockStyle.Fill;
            this._toolPanelSplit.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this._toolPanelSplit.Location = new System.Drawing.Point(0, 0);
            this._toolPanelSplit.Name = "_toolPanelSplit";
            this._toolPanelSplit.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // _toolPanelSplit.Panel1
            // 
            this._toolPanelSplit.Panel1.Controls.Add(this._displaySection);
            // 
            // _toolPanelSplit.Panel2
            // 
            this._toolPanelSplit.Panel2.Controls.Add(this._operationSection);
            this._toolPanelSplit.Size = new System.Drawing.Size(388, 944);
            this._toolPanelSplit.SplitterDistance = 380;
            this._toolPanelSplit.TabIndex = 0;
            // 
            // _displaySection
            // 
            this._displaySection.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._displaySection.Controls.Add(this._displayPageHost);
            this._displaySection.Controls.Add(this._displayHeader);
            this._displaySection.Dock = System.Windows.Forms.DockStyle.Fill;
            this._displaySection.Location = new System.Drawing.Point(0, 0);
            this._displaySection.Name = "_displaySection";
            this._displaySection.Size = new System.Drawing.Size(388, 380);
            this._displaySection.TabIndex = 0;
            // 
            // _displayPageHost
            // 
            this._displayPageHost.Controls.Add(this._windowLevelPage);
            this._displayPageHost.Controls.Add(this._measurementPage);
            this._displayPageHost.Controls.Add(this._tonePage);
            this._displayPageHost.Controls.Add(this._annotationPage);
            this._displayPageHost.Controls.Add(this._displaySettingsPage);
            this._displayPageHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._displayPageHost.Location = new System.Drawing.Point(0, 40);
            this._displayPageHost.Name = "_displayPageHost";
            this._displayPageHost.Size = new System.Drawing.Size(388, 340);
            this._displayPageHost.TabIndex = 1;
            // 
            // _windowLevelPage
            // 
            this._windowLevelPage.AutoScroll = true;
            this._windowLevelPage.Controls.Add(this._windowLevelPanel);
            this._windowLevelPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowLevelPage.Location = new System.Drawing.Point(0, 0);
            this._windowLevelPage.Name = "_windowLevelPage";
            this._windowLevelPage.Size = new System.Drawing.Size(388, 340);
            this._windowLevelPage.TabIndex = 1;
            this._windowLevelPage.Visible = false;
            // 
            // _windowLevelPanel
            // 
            this._windowLevelPanel.BackColor = System.Drawing.Color.White;
            this._windowLevelPanel.Controls.Add(this._windowLevelLayout);
            this._windowLevelPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._windowLevelPanel.Location = new System.Drawing.Point(0, 0);
            this._windowLevelPanel.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
            this._windowLevelPanel.Name = "_windowLevelPanel";
            this._windowLevelPanel.Size = new System.Drawing.Size(388, 206);
            this._windowLevelPanel.TabIndex = 0;
            // 
            // _windowLevelLayout
            // 
            this._windowLevelLayout.ColumnCount = 5;
            this._windowLevelLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            this._windowLevelLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            this._windowLevelLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this._windowLevelLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._windowLevelLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 42F));
            this._windowLevelLayout.Controls.Add(this._applyWindowLevelCheckBox, 0, 0);
            this._windowLevelLayout.Controls.Add(this._autoWindowLevelRoiCheckBox, 2, 0);
            this._windowLevelLayout.Controls.Add(this._windowValueLabel, 0, 1);
            this._windowLevelLayout.Controls.Add(this._windowValueEditor, 1, 1);
            this._windowLevelLayout.Controls.Add(this._windowMinimumLabel, 2, 1);
            this._windowLevelLayout.Controls.Add(this._windowTrackBar, 3, 1);
            this._windowLevelLayout.Controls.Add(this._windowMaximumLabel, 4, 1);
            this._windowLevelLayout.Controls.Add(this._levelValueLabel, 0, 2);
            this._windowLevelLayout.Controls.Add(this._levelValueEditor, 1, 2);
            this._windowLevelLayout.Controls.Add(this._levelMinimumLabel, 2, 2);
            this._windowLevelLayout.Controls.Add(this._levelTrackBar, 3, 2);
            this._windowLevelLayout.Controls.Add(this._levelMaximumLabel, 4, 2);
            this._windowLevelLayout.Controls.Add(this._refreshWindowLevelButton, 0, 3);
            this._windowLevelLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowLevelLayout.Location = new System.Drawing.Point(0, 0);
            this._windowLevelLayout.Name = "_windowLevelLayout";
            this._windowLevelLayout.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._windowLevelLayout.RowCount = 4;
            this._windowLevelLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._windowLevelLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this._windowLevelLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this._windowLevelLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 102F));
            this._windowLevelLayout.Size = new System.Drawing.Size(388, 206);
            this._windowLevelLayout.TabIndex = 1;
            // 
            // _applyWindowLevelCheckBox
            // 
            this._applyWindowLevelCheckBox.Checked = true;
            this._applyWindowLevelCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._windowLevelLayout.SetColumnSpan(this._applyWindowLevelCheckBox, 2);
            this._applyWindowLevelCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._applyWindowLevelCheckBox.Location = new System.Drawing.Point(13, 7);
            this._applyWindowLevelCheckBox.Name = "_applyWindowLevelCheckBox";
            this._applyWindowLevelCheckBox.Size = new System.Drawing.Size(122, 22);
            this._applyWindowLevelCheckBox.TabIndex = 0;
            this._applyWindowLevelCheckBox.Text = "应用窗宽窗位";
            this._applyWindowLevelCheckBox.UseVisualStyleBackColor = true;
            // 
            // _autoWindowLevelRoiCheckBox
            // 
            this._windowLevelLayout.SetColumnSpan(this._autoWindowLevelRoiCheckBox, 3);
            this._autoWindowLevelRoiCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._autoWindowLevelRoiCheckBox.Location = new System.Drawing.Point(141, 7);
            this._autoWindowLevelRoiCheckBox.Name = "_autoWindowLevelRoiCheckBox";
            this._autoWindowLevelRoiCheckBox.Size = new System.Drawing.Size(234, 22);
            this._autoWindowLevelRoiCheckBox.TabIndex = 1;
            this._autoWindowLevelRoiCheckBox.Text = "框选自动取窗";
            this._autoWindowLevelRoiCheckBox.UseVisualStyleBackColor = true;
            // 
            // _windowValueLabel
            // 
            this._windowValueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowValueLabel.Location = new System.Drawing.Point(13, 32);
            this._windowValueLabel.Name = "_windowValueLabel";
            this._windowValueLabel.Size = new System.Drawing.Size(50, 34);
            this._windowValueLabel.TabIndex = 1;
            this._windowValueLabel.Text = "窗宽";
            this._windowValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _windowValueEditor
            // 
            this._windowValueEditor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._windowValueEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowValueEditor.Location = new System.Drawing.Point(69, 35);
            this._windowValueEditor.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this._windowValueEditor.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this._windowValueEditor.Name = "_windowValueEditor";
            this._windowValueEditor.Size = new System.Drawing.Size(66, 23);
            this._windowValueEditor.TabIndex = 2;
            this._windowValueEditor.Value = new decimal(new int[] {
            4000,
            0,
            0,
            0});
            // 
            // _windowMinimumLabel
            // 
            this._windowMinimumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowMinimumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._windowMinimumLabel.Location = new System.Drawing.Point(141, 32);
            this._windowMinimumLabel.Name = "_windowMinimumLabel";
            this._windowMinimumLabel.Size = new System.Drawing.Size(36, 34);
            this._windowMinimumLabel.TabIndex = 3;
            this._windowMinimumLabel.Text = "1";
            this._windowMinimumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _windowTrackBar
            // 
            this._windowTrackBar.AutoSize = false;
            this._windowTrackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowTrackBar.Location = new System.Drawing.Point(183, 35);
            this._windowTrackBar.Maximum = 65535;
            this._windowTrackBar.Minimum = 1;
            this._windowTrackBar.Name = "_windowTrackBar";
            this._windowTrackBar.Size = new System.Drawing.Size(150, 28);
            this._windowTrackBar.TabIndex = 4;
            this._windowTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this._windowTrackBar.Value = 4000;
            // 
            // _windowMaximumLabel
            // 
            this._windowMaximumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._windowMaximumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._windowMaximumLabel.Location = new System.Drawing.Point(339, 32);
            this._windowMaximumLabel.Name = "_windowMaximumLabel";
            this._windowMaximumLabel.Size = new System.Drawing.Size(36, 34);
            this._windowMaximumLabel.TabIndex = 5;
            this._windowMaximumLabel.Text = "65535";
            this._windowMaximumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _levelValueLabel
            // 
            this._levelValueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._levelValueLabel.Location = new System.Drawing.Point(13, 66);
            this._levelValueLabel.Name = "_levelValueLabel";
            this._levelValueLabel.Size = new System.Drawing.Size(50, 34);
            this._levelValueLabel.TabIndex = 6;
            this._levelValueLabel.Text = "窗位";
            this._levelValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _levelValueEditor
            // 
            this._levelValueEditor.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this._levelValueEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this._levelValueEditor.Location = new System.Drawing.Point(69, 69);
            this._levelValueEditor.Maximum = new decimal(new int[] {
            65535,
            0,
            0,
            0});
            this._levelValueEditor.Name = "_levelValueEditor";
            this._levelValueEditor.Size = new System.Drawing.Size(66, 23);
            this._levelValueEditor.TabIndex = 7;
            this._levelValueEditor.Value = new decimal(new int[] {
            8000,
            0,
            0,
            0});
            // 
            // _levelMinimumLabel
            // 
            this._levelMinimumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._levelMinimumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._levelMinimumLabel.Location = new System.Drawing.Point(141, 66);
            this._levelMinimumLabel.Name = "_levelMinimumLabel";
            this._levelMinimumLabel.Size = new System.Drawing.Size(36, 34);
            this._levelMinimumLabel.TabIndex = 8;
            this._levelMinimumLabel.Text = "0";
            this._levelMinimumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _levelTrackBar
            // 
            this._levelTrackBar.AutoSize = false;
            this._levelTrackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._levelTrackBar.Location = new System.Drawing.Point(183, 69);
            this._levelTrackBar.Maximum = 65535;
            this._levelTrackBar.Name = "_levelTrackBar";
            this._levelTrackBar.Size = new System.Drawing.Size(150, 28);
            this._levelTrackBar.TabIndex = 9;
            this._levelTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this._levelTrackBar.Value = 8000;
            // 
            // _levelMaximumLabel
            // 
            this._levelMaximumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._levelMaximumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._levelMaximumLabel.Location = new System.Drawing.Point(339, 66);
            this._levelMaximumLabel.Name = "_levelMaximumLabel";
            this._levelMaximumLabel.Size = new System.Drawing.Size(36, 34);
            this._levelMaximumLabel.TabIndex = 10;
            this._levelMaximumLabel.Text = "65535";
            this._levelMaximumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _refreshWindowLevelButton
            // 
            this._refreshWindowLevelButton.BackColor = System.Drawing.Color.White;
            this._windowLevelLayout.SetColumnSpan(this._refreshWindowLevelButton, 2);
            this._refreshWindowLevelButton.Dock = System.Windows.Forms.DockStyle.Left;
            this._refreshWindowLevelButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._refreshWindowLevelButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._refreshWindowLevelButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._refreshWindowLevelButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._refreshWindowLevelButton.Image = global::CxECTImageWinForms.Properties.Resources._06_刷新图像_循环箭头;
            this._refreshWindowLevelButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._refreshWindowLevelButton.Location = new System.Drawing.Point(13, 103);
            this._refreshWindowLevelButton.Name = "_refreshWindowLevelButton";
            this._refreshWindowLevelButton.Size = new System.Drawing.Size(114, 96);
            this._refreshWindowLevelButton.TabIndex = 11;
            this._refreshWindowLevelButton.Text = "刷新窗值";
            this._refreshWindowLevelButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._refreshWindowLevelButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._refreshWindowLevelButton.UseVisualStyleBackColor = false;
            // 
            // _measurementPage
            // 
            this._measurementPage.Controls.Add(this._measurementActionHost);
            this._measurementPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._measurementPage.Location = new System.Drawing.Point(0, 0);
            this._measurementPage.Name = "_measurementPage";
            this._measurementPage.Size = new System.Drawing.Size(388, 340);
            this._measurementPage.TabIndex = 1;
            this._measurementPage.Visible = false;
            // 
            // _measurementActionHost
            // 
            this._measurementActionHost.Controls.Add(this._measurementActionPanel);
            this._measurementActionHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._measurementActionHost.Location = new System.Drawing.Point(0, 0);
            this._measurementActionHost.Name = "_measurementActionHost";
            this._measurementActionHost.Size = new System.Drawing.Size(388, 340);
            this._measurementActionHost.TabIndex = 0;
            // 
            // _measurementActionPanel
            // 
            this._measurementActionPanel.AutoScroll = true;
            this._measurementActionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._measurementActionPanel.Controls.Add(this._distanceButton);
            this._measurementActionPanel.Controls.Add(this._horizontalDistanceButton);
            this._measurementActionPanel.Controls.Add(this._verticalDistanceButton);
            this._measurementActionPanel.Controls.Add(this._angleButton);
            this._measurementActionPanel.Controls.Add(this._diameterButton);
            this._measurementActionPanel.Controls.Add(this._areaButton);
            this._measurementActionPanel.Controls.Add(this._clearMeasurementButton);
            this._measurementActionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._measurementActionPanel.Location = new System.Drawing.Point(0, 0);
            this._measurementActionPanel.Name = "_measurementActionPanel";
            this._measurementActionPanel.Padding = new System.Windows.Forms.Padding(8);
            this._measurementActionPanel.Size = new System.Drawing.Size(388, 340);
            this._measurementActionPanel.TabIndex = 0;
            // 
            // _distanceButton
            // 
            this._distanceButton.BackColor = System.Drawing.Color.White;
            this._distanceButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._distanceButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._distanceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._distanceButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._distanceButton.Image = global::CxECTImageWinForms.Properties.Resources._01_距离_对角测距;
            this._distanceButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._distanceButton.Location = new System.Drawing.Point(11, 11);
            this._distanceButton.Name = "_distanceButton";
            this._distanceButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._distanceButton.Size = new System.Drawing.Size(96, 96);
            this._distanceButton.TabIndex = 0;
            this._distanceButton.Tag = "ExclusiveMode";
            this._distanceButton.Text = "距离";
            this._distanceButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._distanceButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._distanceButton.UseVisualStyleBackColor = false;
            // 
            // _horizontalDistanceButton
            // 
            this._horizontalDistanceButton.BackColor = System.Drawing.Color.White;
            this._horizontalDistanceButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._horizontalDistanceButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._horizontalDistanceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._horizontalDistanceButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._horizontalDistanceButton.Image = global::CxECTImageWinForms.Properties.Resources._02_水平距离_双向测量;
            this._horizontalDistanceButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._horizontalDistanceButton.Location = new System.Drawing.Point(113, 11);
            this._horizontalDistanceButton.Name = "_horizontalDistanceButton";
            this._horizontalDistanceButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._horizontalDistanceButton.Size = new System.Drawing.Size(96, 96);
            this._horizontalDistanceButton.TabIndex = 1;
            this._horizontalDistanceButton.Tag = "ExclusiveMode";
            this._horizontalDistanceButton.Text = "水平距离";
            this._horizontalDistanceButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._horizontalDistanceButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._horizontalDistanceButton.UseVisualStyleBackColor = false;
            // 
            // _verticalDistanceButton
            // 
            this._verticalDistanceButton.BackColor = System.Drawing.Color.White;
            this._verticalDistanceButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._verticalDistanceButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._verticalDistanceButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._verticalDistanceButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._verticalDistanceButton.Image = global::CxECTImageWinForms.Properties.Resources._03_垂直距离_双向测量;
            this._verticalDistanceButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._verticalDistanceButton.Location = new System.Drawing.Point(215, 11);
            this._verticalDistanceButton.Name = "_verticalDistanceButton";
            this._verticalDistanceButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._verticalDistanceButton.Size = new System.Drawing.Size(96, 96);
            this._verticalDistanceButton.TabIndex = 2;
            this._verticalDistanceButton.Tag = "ExclusiveMode";
            this._verticalDistanceButton.Text = "垂直距离";
            this._verticalDistanceButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._verticalDistanceButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._verticalDistanceButton.UseVisualStyleBackColor = false;
            // 
            // _angleButton
            // 
            this._angleButton.BackColor = System.Drawing.Color.White;
            this._angleButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._angleButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._angleButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._angleButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._angleButton.Image = global::CxECTImageWinForms.Properties.Resources._04_角度_弧线测量;
            this._angleButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._angleButton.Location = new System.Drawing.Point(11, 113);
            this._angleButton.Name = "_angleButton";
            this._angleButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._angleButton.Size = new System.Drawing.Size(96, 96);
            this._angleButton.TabIndex = 3;
            this._angleButton.Tag = "ExclusiveMode";
            this._angleButton.Text = "角度";
            this._angleButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._angleButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._angleButton.UseVisualStyleBackColor = false;
            // 
            // _diameterButton
            // 
            this._diameterButton.BackColor = System.Drawing.Color.White;
            this._diameterButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._diameterButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._diameterButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._diameterButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._diameterButton.Image = global::CxECTImageWinForms.Properties.Resources._05_直径_圆形测量;
            this._diameterButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._diameterButton.Location = new System.Drawing.Point(113, 113);
            this._diameterButton.Name = "_diameterButton";
            this._diameterButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._diameterButton.Size = new System.Drawing.Size(96, 96);
            this._diameterButton.TabIndex = 4;
            this._diameterButton.Tag = "ExclusiveMode";
            this._diameterButton.Text = "直径";
            this._diameterButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._diameterButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._diameterButton.UseVisualStyleBackColor = false;
            // 
            // _areaButton
            // 
            this._areaButton.BackColor = System.Drawing.Color.White;
            this._areaButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._areaButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._areaButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._areaButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._areaButton.Image = global::CxECTImageWinForms.Properties.Resources._06_面积_多边形区域;
            this._areaButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._areaButton.Location = new System.Drawing.Point(215, 113);
            this._areaButton.Name = "_areaButton";
            this._areaButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._areaButton.Size = new System.Drawing.Size(96, 96);
            this._areaButton.TabIndex = 5;
            this._areaButton.Tag = "ExclusiveMode";
            this._areaButton.Text = "面积";
            this._areaButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._areaButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._areaButton.UseVisualStyleBackColor = false;
            // 
            // _clearMeasurementButton
            // 
            this._clearMeasurementButton.BackColor = System.Drawing.Color.White;
            this._clearMeasurementButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._clearMeasurementButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._clearMeasurementButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._clearMeasurementButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._clearMeasurementButton.Image = global::CxECTImageWinForms.Properties.Resources._13_清空标注_橡皮擦;
            this._clearMeasurementButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._clearMeasurementButton.Location = new System.Drawing.Point(11, 215);
            this._clearMeasurementButton.Name = "_clearMeasurementButton";
            this._clearMeasurementButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._clearMeasurementButton.Size = new System.Drawing.Size(96, 96);
            this._clearMeasurementButton.TabIndex = 6;
            this._clearMeasurementButton.Tag = "Command";
            this._clearMeasurementButton.Text = "清除测量";
            this._clearMeasurementButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._clearMeasurementButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._clearMeasurementButton.UseVisualStyleBackColor = false;
            // 
            // _tonePage
            // 
            this._tonePage.AutoScroll = true;
            this._tonePage.Controls.Add(this._tonePanel);
            this._tonePage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._tonePage.Location = new System.Drawing.Point(0, 0);
            this._tonePage.Name = "_tonePage";
            this._tonePage.Size = new System.Drawing.Size(388, 340);
            this._tonePage.TabIndex = 2;
            this._tonePage.Visible = false;
            // 
            // _tonePanel
            // 
            this._tonePanel.BackColor = System.Drawing.Color.White;
            this._tonePanel.Controls.Add(this._toneLayout);
            this._tonePanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._tonePanel.Location = new System.Drawing.Point(0, 0);
            this._tonePanel.Margin = new System.Windows.Forms.Padding(0);
            this._tonePanel.Name = "_tonePanel";
            this._tonePanel.Size = new System.Drawing.Size(388, 164);
            this._tonePanel.TabIndex = 1;
            // 
            // _toneLayout
            // 
            this._toneLayout.ColumnCount = 4;
            this._toneLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 92F));
            this._toneLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this._toneLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this._toneLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            this._toneLayout.Controls.Add(this._autoContrastCheckBox, 0, 0);
            this._toneLayout.Controls.Add(this._invertCheckBox, 2, 0);
            this._toneLayout.Controls.Add(this._gammaValueLabel, 0, 1);
            this._toneLayout.Controls.Add(this._gammaMinimumLabel, 1, 1);
            this._toneLayout.Controls.Add(this._gammaTrackBar, 2, 1);
            this._toneLayout.Controls.Add(this._gammaMaximumLabel, 3, 1);
            this._toneLayout.Controls.Add(this._brightnessValueLabel, 0, 2);
            this._toneLayout.Controls.Add(this._brightnessMinimumLabel, 1, 2);
            this._toneLayout.Controls.Add(this._brightnessTrackBar, 2, 2);
            this._toneLayout.Controls.Add(this._brightnessMaximumLabel, 3, 2);
            this._toneLayout.Controls.Add(this._contrastValueLabel, 0, 3);
            this._toneLayout.Controls.Add(this._contrastMinimumLabel, 1, 3);
            this._toneLayout.Controls.Add(this._contrastTrackBar, 2, 3);
            this._toneLayout.Controls.Add(this._contrastMaximumLabel, 3, 3);
            this._toneLayout.Dock = System.Windows.Forms.DockStyle.Fill;
            this._toneLayout.Location = new System.Drawing.Point(0, 0);
            this._toneLayout.Name = "_toneLayout";
            this._toneLayout.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._toneLayout.RowCount = 4;
            this._toneLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this._toneLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this._toneLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this._toneLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this._toneLayout.Size = new System.Drawing.Size(388, 164);
            this._toneLayout.TabIndex = 1;
            // 
            // _autoContrastCheckBox
            // 
            this._toneLayout.SetColumnSpan(this._autoContrastCheckBox, 2);
            this._autoContrastCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._autoContrastCheckBox.Location = new System.Drawing.Point(13, 7);
            this._autoContrastCheckBox.Name = "_autoContrastCheckBox";
            this._autoContrastCheckBox.Size = new System.Drawing.Size(136, 22);
            this._autoContrastCheckBox.TabIndex = 5;
            this._autoContrastCheckBox.Text = "自动灰度拉伸";
            this._autoContrastCheckBox.UseVisualStyleBackColor = true;
            // 
            // _invertCheckBox
            // 
            this._invertCheckBox.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._toneLayout.SetColumnSpan(this._invertCheckBox, 2);
            this._invertCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._invertCheckBox.Location = new System.Drawing.Point(155, 7);
            this._invertCheckBox.Name = "_invertCheckBox";
            this._invertCheckBox.Size = new System.Drawing.Size(220, 22);
            this._invertCheckBox.TabIndex = 6;
            this._invertCheckBox.Text = "反色";
            this._invertCheckBox.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._invertCheckBox.UseVisualStyleBackColor = true;
            // 
            // _gammaValueLabel
            // 
            this._gammaValueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gammaValueLabel.Location = new System.Drawing.Point(13, 32);
            this._gammaValueLabel.Name = "_gammaValueLabel";
            this._gammaValueLabel.Size = new System.Drawing.Size(86, 34);
            this._gammaValueLabel.TabIndex = 7;
            this._gammaValueLabel.Text = "Gamma 1.00";
            this._gammaValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _gammaMinimumLabel
            // 
            this._gammaMinimumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gammaMinimumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._gammaMinimumLabel.Location = new System.Drawing.Point(105, 32);
            this._gammaMinimumLabel.Name = "_gammaMinimumLabel";
            this._gammaMinimumLabel.Size = new System.Drawing.Size(44, 34);
            this._gammaMinimumLabel.TabIndex = 8;
            this._gammaMinimumLabel.Text = "0.10";
            this._gammaMinimumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _gammaTrackBar
            // 
            this._gammaTrackBar.AutoSize = false;
            this._gammaTrackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gammaTrackBar.Location = new System.Drawing.Point(155, 35);
            this._gammaTrackBar.Maximum = 300;
            this._gammaTrackBar.Minimum = 10;
            this._gammaTrackBar.Name = "_gammaTrackBar";
            this._gammaTrackBar.Size = new System.Drawing.Size(170, 28);
            this._gammaTrackBar.TabIndex = 8;
            this._gammaTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this._gammaTrackBar.Value = 100;
            // 
            // _gammaMaximumLabel
            // 
            this._gammaMaximumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gammaMaximumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._gammaMaximumLabel.Location = new System.Drawing.Point(331, 32);
            this._gammaMaximumLabel.Name = "_gammaMaximumLabel";
            this._gammaMaximumLabel.Size = new System.Drawing.Size(44, 34);
            this._gammaMaximumLabel.TabIndex = 10;
            this._gammaMaximumLabel.Text = "3.00";
            this._gammaMaximumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _brightnessValueLabel
            // 
            this._brightnessValueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._brightnessValueLabel.Location = new System.Drawing.Point(13, 66);
            this._brightnessValueLabel.Name = "_brightnessValueLabel";
            this._brightnessValueLabel.Size = new System.Drawing.Size(86, 34);
            this._brightnessValueLabel.TabIndex = 9;
            this._brightnessValueLabel.Text = "亮度 0";
            this._brightnessValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _brightnessMinimumLabel
            // 
            this._brightnessMinimumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._brightnessMinimumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._brightnessMinimumLabel.Location = new System.Drawing.Point(105, 66);
            this._brightnessMinimumLabel.Name = "_brightnessMinimumLabel";
            this._brightnessMinimumLabel.Size = new System.Drawing.Size(44, 34);
            this._brightnessMinimumLabel.TabIndex = 12;
            this._brightnessMinimumLabel.Text = "-255";
            this._brightnessMinimumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _brightnessTrackBar
            // 
            this._brightnessTrackBar.AutoSize = false;
            this._brightnessTrackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._brightnessTrackBar.Location = new System.Drawing.Point(155, 69);
            this._brightnessTrackBar.Maximum = 255;
            this._brightnessTrackBar.Minimum = -255;
            this._brightnessTrackBar.Name = "_brightnessTrackBar";
            this._brightnessTrackBar.Size = new System.Drawing.Size(170, 28);
            this._brightnessTrackBar.TabIndex = 10;
            this._brightnessTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            // 
            // _brightnessMaximumLabel
            // 
            this._brightnessMaximumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._brightnessMaximumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._brightnessMaximumLabel.Location = new System.Drawing.Point(331, 66);
            this._brightnessMaximumLabel.Name = "_brightnessMaximumLabel";
            this._brightnessMaximumLabel.Size = new System.Drawing.Size(44, 34);
            this._brightnessMaximumLabel.TabIndex = 14;
            this._brightnessMaximumLabel.Text = "255";
            this._brightnessMaximumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _contrastValueLabel
            // 
            this._contrastValueLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contrastValueLabel.Location = new System.Drawing.Point(13, 100);
            this._contrastValueLabel.Name = "_contrastValueLabel";
            this._contrastValueLabel.Size = new System.Drawing.Size(86, 60);
            this._contrastValueLabel.TabIndex = 12;
            this._contrastValueLabel.Text = "对比度 1.00";
            this._contrastValueLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _contrastMinimumLabel
            // 
            this._contrastMinimumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contrastMinimumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._contrastMinimumLabel.Location = new System.Drawing.Point(105, 100);
            this._contrastMinimumLabel.Name = "_contrastMinimumLabel";
            this._contrastMinimumLabel.Size = new System.Drawing.Size(44, 60);
            this._contrastMinimumLabel.TabIndex = 16;
            this._contrastMinimumLabel.Text = "0.00";
            this._contrastMinimumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _contrastTrackBar
            // 
            this._contrastTrackBar.AutoSize = false;
            this._contrastTrackBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contrastTrackBar.Location = new System.Drawing.Point(155, 103);
            this._contrastTrackBar.Maximum = 300;
            this._contrastTrackBar.Name = "_contrastTrackBar";
            this._contrastTrackBar.Size = new System.Drawing.Size(170, 54);
            this._contrastTrackBar.TabIndex = 13;
            this._contrastTrackBar.TickStyle = System.Windows.Forms.TickStyle.None;
            this._contrastTrackBar.Value = 100;
            // 
            // _contrastMaximumLabel
            // 
            this._contrastMaximumLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._contrastMaximumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(108)))), ((int)(((byte)(122)))));
            this._contrastMaximumLabel.Location = new System.Drawing.Point(331, 100);
            this._contrastMaximumLabel.Name = "_contrastMaximumLabel";
            this._contrastMaximumLabel.Size = new System.Drawing.Size(44, 60);
            this._contrastMaximumLabel.TabIndex = 18;
            this._contrastMaximumLabel.Text = "3.00";
            this._contrastMaximumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _annotationPage
            // 
            this._annotationPage.Controls.Add(this._annotationActionHost);
            this._annotationPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._annotationPage.Location = new System.Drawing.Point(0, 0);
            this._annotationPage.Name = "_annotationPage";
            this._annotationPage.Size = new System.Drawing.Size(388, 340);
            this._annotationPage.TabIndex = 2;
            this._annotationPage.Visible = false;
            // 
            // _annotationActionHost
            // 
            this._annotationActionHost.Controls.Add(this._annotationActionPanel);
            this._annotationActionHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._annotationActionHost.Location = new System.Drawing.Point(0, 0);
            this._annotationActionHost.Name = "_annotationActionHost";
            this._annotationActionHost.Size = new System.Drawing.Size(388, 340);
            this._annotationActionHost.TabIndex = 0;
            // 
            // _annotationActionPanel
            // 
            this._annotationActionPanel.AutoScroll = true;
            this._annotationActionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._annotationActionPanel.Controls.Add(this._pointAnnotationButton);
            this._annotationActionPanel.Controls.Add(this._rectangleAnnotationButton);
            this._annotationActionPanel.Controls.Add(this._polygonAnnotationButton);
            this._annotationActionPanel.Controls.Add(this._textAnnotationButton);
            this._annotationActionPanel.Controls.Add(this._arrowAnnotationButton);
            this._annotationActionPanel.Controls.Add(this._defectBoxButton);
            this._annotationActionPanel.Controls.Add(this._clearAnnotationButton);
            this._annotationActionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._annotationActionPanel.Location = new System.Drawing.Point(0, 0);
            this._annotationActionPanel.Name = "_annotationActionPanel";
            this._annotationActionPanel.Padding = new System.Windows.Forms.Padding(8);
            this._annotationActionPanel.Size = new System.Drawing.Size(388, 340);
            this._annotationActionPanel.TabIndex = 0;
            // 
            // _pointAnnotationButton
            // 
            this._pointAnnotationButton.BackColor = System.Drawing.Color.White;
            this._pointAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._pointAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._pointAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._pointAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._pointAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._01_点标注_定位准星;
            this._pointAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._pointAnnotationButton.Location = new System.Drawing.Point(11, 11);
            this._pointAnnotationButton.Name = "_pointAnnotationButton";
            this._pointAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._pointAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._pointAnnotationButton.TabIndex = 0;
            this._pointAnnotationButton.Tag = "ExclusiveMode";
            this._pointAnnotationButton.Text = "点标注";
            this._pointAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._pointAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._pointAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _rectangleAnnotationButton
            // 
            this._rectangleAnnotationButton.BackColor = System.Drawing.Color.White;
            this._rectangleAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._rectangleAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._rectangleAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._rectangleAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._rectangleAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._03_矩形标注_选框;
            this._rectangleAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._rectangleAnnotationButton.Location = new System.Drawing.Point(113, 11);
            this._rectangleAnnotationButton.Name = "_rectangleAnnotationButton";
            this._rectangleAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._rectangleAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._rectangleAnnotationButton.TabIndex = 1;
            this._rectangleAnnotationButton.Tag = "ExclusiveMode";
            this._rectangleAnnotationButton.Text = "矩形标注";
            this._rectangleAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._rectangleAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._rectangleAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _polygonAnnotationButton
            // 
            this._polygonAnnotationButton.BackColor = System.Drawing.Color.White;
            this._polygonAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._polygonAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._polygonAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._polygonAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._polygonAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._05_多边形标注_节点;
            this._polygonAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._polygonAnnotationButton.Location = new System.Drawing.Point(215, 11);
            this._polygonAnnotationButton.Name = "_polygonAnnotationButton";
            this._polygonAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._polygonAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._polygonAnnotationButton.TabIndex = 2;
            this._polygonAnnotationButton.Tag = "ExclusiveMode";
            this._polygonAnnotationButton.Text = "多边形标注";
            this._polygonAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._polygonAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._polygonAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _textAnnotationButton
            // 
            this._textAnnotationButton.BackColor = System.Drawing.Color.White;
            this._textAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._textAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._textAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._textAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._textAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._07_文字标注_T气泡;
            this._textAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._textAnnotationButton.Location = new System.Drawing.Point(11, 113);
            this._textAnnotationButton.Name = "_textAnnotationButton";
            this._textAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._textAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._textAnnotationButton.TabIndex = 3;
            this._textAnnotationButton.Tag = "ExclusiveMode";
            this._textAnnotationButton.Text = "文字标注";
            this._textAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._textAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._textAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _arrowAnnotationButton
            // 
            this._arrowAnnotationButton.BackColor = System.Drawing.Color.White;
            this._arrowAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._arrowAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._arrowAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._arrowAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._arrowAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._09_箭头_右上箭头;
            this._arrowAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._arrowAnnotationButton.Location = new System.Drawing.Point(113, 113);
            this._arrowAnnotationButton.Name = "_arrowAnnotationButton";
            this._arrowAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._arrowAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._arrowAnnotationButton.TabIndex = 4;
            this._arrowAnnotationButton.Tag = "ExclusiveMode";
            this._arrowAnnotationButton.Text = "箭头";
            this._arrowAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._arrowAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._arrowAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _defectBoxButton
            // 
            this._defectBoxButton.BackColor = System.Drawing.Color.White;
            this._defectBoxButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._defectBoxButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._defectBoxButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._defectBoxButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._defectBoxButton.Image = global::CxECTImageWinForms.Properties.Resources._11_缺陷框_警示框;
            this._defectBoxButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._defectBoxButton.Location = new System.Drawing.Point(215, 113);
            this._defectBoxButton.Name = "_defectBoxButton";
            this._defectBoxButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._defectBoxButton.Size = new System.Drawing.Size(96, 96);
            this._defectBoxButton.TabIndex = 5;
            this._defectBoxButton.Tag = "ExclusiveMode";
            this._defectBoxButton.Text = "缺陷框";
            this._defectBoxButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._defectBoxButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._defectBoxButton.UseVisualStyleBackColor = false;
            // 
            // _clearAnnotationButton
            // 
            this._clearAnnotationButton.BackColor = System.Drawing.Color.White;
            this._clearAnnotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._clearAnnotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._clearAnnotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._clearAnnotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._clearAnnotationButton.Image = global::CxECTImageWinForms.Properties.Resources._13_清空标注_橡皮擦;
            this._clearAnnotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._clearAnnotationButton.Location = new System.Drawing.Point(11, 215);
            this._clearAnnotationButton.Name = "_clearAnnotationButton";
            this._clearAnnotationButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._clearAnnotationButton.Size = new System.Drawing.Size(96, 96);
            this._clearAnnotationButton.TabIndex = 6;
            this._clearAnnotationButton.Tag = "Command";
            this._clearAnnotationButton.Text = "清空标注";
            this._clearAnnotationButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._clearAnnotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._clearAnnotationButton.UseVisualStyleBackColor = false;
            // 
            // _displaySettingsPage
            // 
            this._displaySettingsPage.AutoScroll = true;
            this._displaySettingsPage.Controls.Add(this._layerVisibilityLayout);
            this._displaySettingsPage.Controls.Add(this._overlayColorLayout);
            this._displaySettingsPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._displaySettingsPage.Location = new System.Drawing.Point(0, 0);
            this._displaySettingsPage.Name = "_displaySettingsPage";
            this._displaySettingsPage.Size = new System.Drawing.Size(388, 340);
            this._displaySettingsPage.TabIndex = 0;
            // 
            // _layerVisibilityLayout
            // 
            this._layerVisibilityLayout.BackColor = System.Drawing.Color.White;
            this._layerVisibilityLayout.ColumnCount = 3;
            this._layerVisibilityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._layerVisibilityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._layerVisibilityLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._layerVisibilityLayout.Controls.Add(this._showImageRulerCheckBox, 0, 0);
            this._layerVisibilityLayout.Controls.Add(this._showRoiCheckBox, 1, 0);
            this._layerVisibilityLayout.Controls.Add(this._showMeasurementCheckBox, 2, 0);
            this._layerVisibilityLayout.Controls.Add(this._showAnnotationCheckBox, 0, 1);
            this._layerVisibilityLayout.Controls.Add(this._showDefectCheckBox, 1, 1);
            this._layerVisibilityLayout.Dock = System.Windows.Forms.DockStyle.Top;
            this._layerVisibilityLayout.Location = new System.Drawing.Point(0, 90);
            this._layerVisibilityLayout.Name = "_layerVisibilityLayout";
            this._layerVisibilityLayout.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this._layerVisibilityLayout.RowCount = 2;
            this._layerVisibilityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this._layerVisibilityLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            this._layerVisibilityLayout.Size = new System.Drawing.Size(388, 68);
            this._layerVisibilityLayout.TabIndex = 1;
            // 
            // _showImageRulerCheckBox
            // 
            this._showImageRulerCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._showImageRulerCheckBox.Location = new System.Drawing.Point(13, 9);
            this._showImageRulerCheckBox.Name = "_showImageRulerCheckBox";
            this._showImageRulerCheckBox.Size = new System.Drawing.Size(116, 26);
            this._showImageRulerCheckBox.TabIndex = 2;
            this._showImageRulerCheckBox.Text = "显示标尺";
            this._showImageRulerCheckBox.UseVisualStyleBackColor = true;
            // 
            // _showRoiCheckBox
            // 
            this._showRoiCheckBox.Checked = true;
            this._showRoiCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showRoiCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._showRoiCheckBox.Location = new System.Drawing.Point(135, 9);
            this._showRoiCheckBox.Name = "_showRoiCheckBox";
            this._showRoiCheckBox.Size = new System.Drawing.Size(116, 26);
            this._showRoiCheckBox.TabIndex = 3;
            this._showRoiCheckBox.Text = "显示 ROI";
            this._showRoiCheckBox.UseVisualStyleBackColor = true;
            // 
            // _showMeasurementCheckBox
            // 
            this._showMeasurementCheckBox.Checked = true;
            this._showMeasurementCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showMeasurementCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._showMeasurementCheckBox.Location = new System.Drawing.Point(257, 9);
            this._showMeasurementCheckBox.Name = "_showMeasurementCheckBox";
            this._showMeasurementCheckBox.Size = new System.Drawing.Size(118, 26);
            this._showMeasurementCheckBox.TabIndex = 4;
            this._showMeasurementCheckBox.Text = "显示测量";
            this._showMeasurementCheckBox.UseVisualStyleBackColor = true;
            // 
            // _showAnnotationCheckBox
            // 
            this._showAnnotationCheckBox.Checked = true;
            this._showAnnotationCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showAnnotationCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._showAnnotationCheckBox.Location = new System.Drawing.Point(13, 41);
            this._showAnnotationCheckBox.Name = "_showAnnotationCheckBox";
            this._showAnnotationCheckBox.Size = new System.Drawing.Size(116, 18);
            this._showAnnotationCheckBox.TabIndex = 5;
            this._showAnnotationCheckBox.Text = "显示标注";
            this._showAnnotationCheckBox.UseVisualStyleBackColor = true;
            // 
            // _showDefectCheckBox
            // 
            this._showDefectCheckBox.Checked = true;
            this._showDefectCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this._showDefectCheckBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this._showDefectCheckBox.Location = new System.Drawing.Point(135, 41);
            this._showDefectCheckBox.Name = "_showDefectCheckBox";
            this._showDefectCheckBox.Size = new System.Drawing.Size(116, 18);
            this._showDefectCheckBox.TabIndex = 6;
            this._showDefectCheckBox.Text = "显示缺陷";
            this._showDefectCheckBox.UseVisualStyleBackColor = true;
            // 
            // _overlayColorLayout
            // 
            this._overlayColorLayout.BackColor = System.Drawing.Color.White;
            this._overlayColorLayout.ColumnCount = 3;
            this._overlayColorLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._overlayColorLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._overlayColorLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._overlayColorLayout.Controls.Add(this._overlayColorTitleLabel, 0, 0);
            this._overlayColorLayout.Controls.Add(this._roiColorButton, 0, 1);
            this._overlayColorLayout.Controls.Add(this._measurementColorButton, 1, 1);
            this._overlayColorLayout.Controls.Add(this._annotationColorButton, 2, 1);
            this._overlayColorLayout.Controls.Add(this._defectColorButton, 0, 2);
            this._overlayColorLayout.Controls.Add(this._selectedColorButton, 1, 2);
            this._overlayColorLayout.Dock = System.Windows.Forms.DockStyle.Top;
            this._overlayColorLayout.Location = new System.Drawing.Point(0, 0);
            this._overlayColorLayout.Name = "_overlayColorLayout";
            this._overlayColorLayout.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this._overlayColorLayout.RowCount = 3;
            this._overlayColorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 22F));
            this._overlayColorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._overlayColorLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this._overlayColorLayout.Size = new System.Drawing.Size(388, 90);
            this._overlayColorLayout.TabIndex = 1;
            // 
            // _overlayColorTitleLabel
            // 
            this._overlayColorLayout.SetColumnSpan(this._overlayColorTitleLabel, 3);
            this._overlayColorTitleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._overlayColorTitleLabel.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Bold);
            this._overlayColorTitleLabel.Location = new System.Drawing.Point(13, 6);
            this._overlayColorTitleLabel.Name = "_overlayColorTitleLabel";
            this._overlayColorTitleLabel.Size = new System.Drawing.Size(362, 22);
            this._overlayColorTitleLabel.TabIndex = 0;
            this._overlayColorTitleLabel.Text = "叠加颜色（点击色块修改）";
            this._overlayColorTitleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _roiColorButton
            // 
            this._roiColorButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(184)))), ((int)(((byte)(217)))));
            this._roiColorButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._roiColorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._roiColorButton.ForeColor = System.Drawing.Color.White;
            this._roiColorButton.Location = new System.Drawing.Point(13, 31);
            this._roiColorButton.Name = "_roiColorButton";
            this._roiColorButton.Size = new System.Drawing.Size(116, 24);
            this._roiColorButton.TabIndex = 1;
            this._roiColorButton.Text = "ROI";
            this._roiColorButton.UseVisualStyleBackColor = false;
            // 
            // _measurementColorButton
            // 
            this._measurementColorButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(152)))), ((int)(((byte)(0)))));
            this._measurementColorButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._measurementColorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._measurementColorButton.ForeColor = System.Drawing.Color.Black;
            this._measurementColorButton.Location = new System.Drawing.Point(135, 31);
            this._measurementColorButton.Name = "_measurementColorButton";
            this._measurementColorButton.Size = new System.Drawing.Size(116, 24);
            this._measurementColorButton.TabIndex = 2;
            this._measurementColorButton.Text = "测量";
            this._measurementColorButton.UseVisualStyleBackColor = false;
            // 
            // _annotationColorButton
            // 
            this._annotationColorButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(194)))), ((int)(((byte)(24)))), ((int)(((byte)(91)))));
            this._annotationColorButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._annotationColorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._annotationColorButton.ForeColor = System.Drawing.Color.White;
            this._annotationColorButton.Location = new System.Drawing.Point(257, 31);
            this._annotationColorButton.Name = "_annotationColorButton";
            this._annotationColorButton.Size = new System.Drawing.Size(118, 24);
            this._annotationColorButton.TabIndex = 3;
            this._annotationColorButton.Text = "标注";
            this._annotationColorButton.UseVisualStyleBackColor = false;
            // 
            // _defectColorButton
            // 
            this._defectColorButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(244)))), ((int)(((byte)(67)))), ((int)(((byte)(54)))));
            this._defectColorButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._defectColorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._defectColorButton.ForeColor = System.Drawing.Color.White;
            this._defectColorButton.Location = new System.Drawing.Point(13, 61);
            this._defectColorButton.Name = "_defectColorButton";
            this._defectColorButton.Size = new System.Drawing.Size(116, 24);
            this._defectColorButton.TabIndex = 4;
            this._defectColorButton.Text = "缺陷";
            this._defectColorButton.UseVisualStyleBackColor = false;
            // 
            // _selectedColorButton
            // 
            this._selectedColorButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(57)))), ((int)(((byte)(211)))), ((int)(((byte)(83)))));
            this._selectedColorButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._selectedColorButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._selectedColorButton.ForeColor = System.Drawing.Color.Black;
            this._selectedColorButton.Location = new System.Drawing.Point(135, 61);
            this._selectedColorButton.Name = "_selectedColorButton";
            this._selectedColorButton.Size = new System.Drawing.Size(116, 24);
            this._selectedColorButton.TabIndex = 5;
            this._selectedColorButton.Text = "当前选中";
            this._selectedColorButton.UseVisualStyleBackColor = false;
            // 
            // _displayHeader
            // 
            this._displayHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(246)))), ((int)(((byte)(248)))), ((int)(((byte)(251)))));
            this._displayHeader.Controls.Add(this._displayAccent);
            this._displayHeader.Controls.Add(this._displaySettingsTabButton);
            this._displayHeader.Controls.Add(this._displayTabSeparator);
            this._displayHeader.Controls.Add(this._windowLevelTabButton);
            this._displayHeader.Controls.Add(this._windowLevelTabSeparator);
            this._displayHeader.Controls.Add(this._toneTabButton);
            this._displayHeader.Controls.Add(this._toneTabSeparator);
            this._displayHeader.Controls.Add(this._measurementTabButton);
            this._displayHeader.Controls.Add(this._operationSeparator1);
            this._displayHeader.Controls.Add(this._annotationTabButton);
            this._displayHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this._displayHeader.Location = new System.Drawing.Point(0, 0);
            this._displayHeader.Name = "_displayHeader";
            this._displayHeader.Size = new System.Drawing.Size(388, 40);
            this._displayHeader.TabIndex = 0;
            // 
            // _displayAccent
            // 
            this._displayAccent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(56)))), ((int)(((byte)(56)))));
            this._displayAccent.Location = new System.Drawing.Point(12, 10);
            this._displayAccent.Name = "_displayAccent";
            this._displayAccent.Size = new System.Drawing.Size(3, 20);
            this._displayAccent.TabIndex = 0;
            // 
            // _displaySettingsTabButton
            // 
            this._displaySettingsTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._displaySettingsTabButton.FlatAppearance.BorderSize = 0;
            this._displaySettingsTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._displaySettingsTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._displaySettingsTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._displaySettingsTabButton.Location = new System.Drawing.Point(28, 0);
            this._displaySettingsTabButton.Name = "_displaySettingsTabButton";
            this._displaySettingsTabButton.Size = new System.Drawing.Size(54, 39);
            this._displaySettingsTabButton.TabIndex = 1;
            this._displaySettingsTabButton.Text = "显示";
            this._displaySettingsTabButton.UseVisualStyleBackColor = false;
            // 
            // _displayTabSeparator
            // 
            this._displayTabSeparator.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._displayTabSeparator.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._displayTabSeparator.Location = new System.Drawing.Point(82, 0);
            this._displayTabSeparator.Name = "_displayTabSeparator";
            this._displayTabSeparator.Size = new System.Drawing.Size(8, 39);
            this._displayTabSeparator.TabIndex = 2;
            this._displayTabSeparator.Text = "|";
            this._displayTabSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _windowLevelTabButton
            // 
            this._windowLevelTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._windowLevelTabButton.FlatAppearance.BorderSize = 0;
            this._windowLevelTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._windowLevelTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._windowLevelTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._windowLevelTabButton.Location = new System.Drawing.Point(90, 0);
            this._windowLevelTabButton.Name = "_windowLevelTabButton";
            this._windowLevelTabButton.Size = new System.Drawing.Size(54, 39);
            this._windowLevelTabButton.TabIndex = 3;
            this._windowLevelTabButton.Text = "窗位";
            this._windowLevelTabButton.UseVisualStyleBackColor = false;
            // 
            // _windowLevelTabSeparator
            // 
            this._windowLevelTabSeparator.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._windowLevelTabSeparator.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._windowLevelTabSeparator.Location = new System.Drawing.Point(144, 0);
            this._windowLevelTabSeparator.Name = "_windowLevelTabSeparator";
            this._windowLevelTabSeparator.Size = new System.Drawing.Size(8, 39);
            this._windowLevelTabSeparator.TabIndex = 4;
            this._windowLevelTabSeparator.Text = "|";
            this._windowLevelTabSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _toneTabButton
            // 
            this._toneTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._toneTabButton.FlatAppearance.BorderSize = 0;
            this._toneTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._toneTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._toneTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._toneTabButton.Location = new System.Drawing.Point(152, 0);
            this._toneTabButton.Name = "_toneTabButton";
            this._toneTabButton.Size = new System.Drawing.Size(54, 39);
            this._toneTabButton.TabIndex = 5;
            this._toneTabButton.Text = "灰阶";
            this._toneTabButton.UseVisualStyleBackColor = false;
            // 
            // _toneTabSeparator
            // 
            this._toneTabSeparator.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._toneTabSeparator.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._toneTabSeparator.Location = new System.Drawing.Point(206, 0);
            this._toneTabSeparator.Name = "_toneTabSeparator";
            this._toneTabSeparator.Size = new System.Drawing.Size(8, 39);
            this._toneTabSeparator.TabIndex = 6;
            this._toneTabSeparator.Text = "|";
            this._toneTabSeparator.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _measurementTabButton
            // 
            this._measurementTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._measurementTabButton.FlatAppearance.BorderSize = 0;
            this._measurementTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._measurementTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._measurementTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._measurementTabButton.Location = new System.Drawing.Point(214, 0);
            this._measurementTabButton.Name = "_measurementTabButton";
            this._measurementTabButton.Size = new System.Drawing.Size(64, 39);
            this._measurementTabButton.TabIndex = 7;
            this._measurementTabButton.Text = "测量";
            this._measurementTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSeparator1
            // 
            this._operationSeparator1.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._operationSeparator1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._operationSeparator1.Location = new System.Drawing.Point(268, 0);
            this._operationSeparator1.Name = "_operationSeparator1";
            this._operationSeparator1.Size = new System.Drawing.Size(8, 39);
            this._operationSeparator1.TabIndex = 8;
            this._operationSeparator1.Text = "|";
            this._operationSeparator1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _annotationTabButton
            // 
            this._annotationTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._annotationTabButton.FlatAppearance.BorderSize = 0;
            this._annotationTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._annotationTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._annotationTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._annotationTabButton.Location = new System.Drawing.Point(276, 0);
            this._annotationTabButton.Name = "_annotationTabButton";
            this._annotationTabButton.Size = new System.Drawing.Size(64, 39);
            this._annotationTabButton.TabIndex = 9;
            this._annotationTabButton.Text = "标注";
            this._annotationTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSection
            // 
            this._operationSection.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._operationSection.Controls.Add(this._operationPageHost);
            this._operationSection.Controls.Add(this._operationHeader);
            this._operationSection.Dock = System.Windows.Forms.DockStyle.Fill;
            this._operationSection.Location = new System.Drawing.Point(0, 0);
            this._operationSection.Name = "_operationSection";
            this._operationSection.Size = new System.Drawing.Size(388, 560);
            this._operationSection.TabIndex = 0;
            // 
            // _operationPageHost
            // 
            this._operationPageHost.Controls.Add(this._transformPanel);
            this._operationPageHost.Controls.Add(this._outputPage);
            this._operationPageHost.Controls.Add(this._pixelAnalysisPage);
            this._operationPageHost.Controls.Add(this._roiPage);
            this._operationPageHost.Controls.Add(this._profileAnalysisPage);
            this._operationPageHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._operationPageHost.Location = new System.Drawing.Point(0, 40);
            this._operationPageHost.Name = "_operationPageHost";
            this._operationPageHost.Size = new System.Drawing.Size(388, 520);
            this._operationPageHost.TabIndex = 1;
            // 
            // _transformPanel
            // 
            this._transformPanel.BackColor = System.Drawing.Color.White;
            this._transformPanel.Controls.Add(this._rotationCommandPanel);
            this._transformPanel.Controls.Add(this._transformCommandPanel);
            this._transformPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._transformPanel.Location = new System.Drawing.Point(0, 0);
            this._transformPanel.Name = "_transformPanel";
            this._transformPanel.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
            this._transformPanel.Size = new System.Drawing.Size(388, 520);
            this._transformPanel.TabIndex = 1;
            this._transformPanel.Visible = false;
            // 
            // _rotationCommandPanel
            // 
            this._rotationCommandPanel.ColumnCount = 3;
            this._rotationCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this._rotationCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._rotationCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33F));
            this._rotationCommandPanel.Controls.Add(this._rotationAngleLabel, 0, 0);
            this._rotationCommandPanel.Controls.Add(this._applyRotationButton, 2, 0);
            this._rotationCommandPanel.Controls.Add(this._rotationAngleInputPanel, 1, 0);
            this._rotationCommandPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._rotationCommandPanel.Location = new System.Drawing.Point(10, 214);
            this._rotationCommandPanel.Name = "_rotationCommandPanel";
            this._rotationCommandPanel.RowCount = 1;
            this._rotationCommandPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 86F));
            this._rotationCommandPanel.Size = new System.Drawing.Size(368, 86);
            this._rotationCommandPanel.TabIndex = 1;
            // 
            // _rotationAngleLabel
            // 
            this._rotationAngleLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rotationAngleLabel.Location = new System.Drawing.Point(3, 0);
            this._rotationAngleLabel.Name = "_rotationAngleLabel";
            this._rotationAngleLabel.Size = new System.Drawing.Size(56, 86);
            this._rotationAngleLabel.TabIndex = 0;
            this._rotationAngleLabel.Text = "旋转\r\n角度";
            this._rotationAngleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // _applyRotationButton
            // 
            this._applyRotationButton.BackColor = System.Drawing.Color.White;
            this._applyRotationButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._applyRotationButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._applyRotationButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._applyRotationButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._applyRotationButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._applyRotationButton.Image = global::CxECTImageWinForms.Properties.Resources._05_应用变换;
            this._applyRotationButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._applyRotationButton.Location = new System.Drawing.Point(246, 3);
            this._applyRotationButton.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
            this._applyRotationButton.Name = "_applyRotationButton";
            this._applyRotationButton.Size = new System.Drawing.Size(119, 80);
            this._applyRotationButton.TabIndex = 2;
            this._applyRotationButton.Text = "应用旋转";
            this._applyRotationButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._applyRotationButton.UseVisualStyleBackColor = false;
            // 
            // _rotationAngleInputPanel
            // 
            this._rotationAngleInputPanel.Controls.Add(this._rotationAngleInput);
            this._rotationAngleInputPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rotationAngleInputPanel.Location = new System.Drawing.Point(65, 3);
            this._rotationAngleInputPanel.Name = "_rotationAngleInputPanel";
            this._rotationAngleInputPanel.Size = new System.Drawing.Size(178, 80);
            this._rotationAngleInputPanel.TabIndex = 3;
            // 
            // _rotationAngleInput
            // 
            this._rotationAngleInput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this._rotationAngleInput.DecimalPlaces = 1;
            this._rotationAngleInput.Increment = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this._rotationAngleInput.Location = new System.Drawing.Point(3, 30);
            this._rotationAngleInput.Margin = new System.Windows.Forms.Padding(3, 6, 6, 6);
            this._rotationAngleInput.Maximum = new decimal(new int[] {
            3599,
            0,
            0,
            65536});
            this._rotationAngleInput.Minimum = new decimal(new int[] {
            3599,
            0,
            0,
            -2147418112});
            this._rotationAngleInput.Name = "_rotationAngleInput";
            this._rotationAngleInput.Size = new System.Drawing.Size(172, 23);
            this._rotationAngleInput.TabIndex = 1;
            // 
            // _transformCommandPanel
            // 
            this._transformCommandPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._transformCommandPanel.ColumnCount = 3;
            this._transformCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._transformCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._transformCommandPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this._transformCommandPanel.Controls.Add(this._rotate90Button, 0, 0);
            this._transformCommandPanel.Controls.Add(this._flipHorizontalButton, 1, 0);
            this._transformCommandPanel.Controls.Add(this._flipVerticalButton, 2, 0);
            this._transformCommandPanel.Controls.Add(this._rotateCounterClockwiseButton, 0, 1);
            this._transformCommandPanel.Controls.Add(this._resetTransformButton, 1, 1);
            this._transformCommandPanel.Controls.Add(this._cropToRoiButton, 2, 1);
            this._transformCommandPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this._transformCommandPanel.Location = new System.Drawing.Point(10, 6);
            this._transformCommandPanel.Name = "_transformCommandPanel";
            this._transformCommandPanel.RowCount = 2;
            this._transformCommandPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 104F));
            this._transformCommandPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 104F));
            this._transformCommandPanel.Size = new System.Drawing.Size(368, 208);
            this._transformCommandPanel.TabIndex = 2;
            // 
            // _rotate90Button
            // 
            this._rotate90Button.BackColor = System.Drawing.Color.White;
            this._rotate90Button.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rotate90Button.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._rotate90Button.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._rotate90Button.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._rotate90Button.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._rotate90Button.Image = global::CxECTImageWinForms.Properties.Resources._01_旋转90度;
            this._rotate90Button.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._rotate90Button.Location = new System.Drawing.Point(4, 4);
            this._rotate90Button.Margin = new System.Windows.Forms.Padding(4);
            this._rotate90Button.Name = "_rotate90Button";
            this._rotate90Button.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._rotate90Button.Size = new System.Drawing.Size(114, 96);
            this._rotate90Button.TabIndex = 0;
            this._rotate90Button.Text = "顺时针 90°";
            this._rotate90Button.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._rotate90Button.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._rotate90Button.UseVisualStyleBackColor = false;
            // 
            // _flipHorizontalButton
            // 
            this._flipHorizontalButton.BackColor = System.Drawing.Color.White;
            this._flipHorizontalButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._flipHorizontalButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(216)))), ((int)(((byte)(224)))));
            this._flipHorizontalButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._flipHorizontalButton.Image = global::CxECTImageWinForms.Properties.Resources._02_水平翻转;
            this._flipHorizontalButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._flipHorizontalButton.Location = new System.Drawing.Point(126, 4);
            this._flipHorizontalButton.Margin = new System.Windows.Forms.Padding(4);
            this._flipHorizontalButton.Name = "_flipHorizontalButton";
            this._flipHorizontalButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._flipHorizontalButton.Size = new System.Drawing.Size(114, 96);
            this._flipHorizontalButton.TabIndex = 1;
            this._flipHorizontalButton.Text = "水平翻转";
            this._flipHorizontalButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._flipHorizontalButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._flipHorizontalButton.UseVisualStyleBackColor = false;
            // 
            // _flipVerticalButton
            // 
            this._flipVerticalButton.BackColor = System.Drawing.Color.White;
            this._flipVerticalButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._flipVerticalButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(216)))), ((int)(((byte)(224)))));
            this._flipVerticalButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._flipVerticalButton.Image = global::CxECTImageWinForms.Properties.Resources._03_垂直翻转;
            this._flipVerticalButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._flipVerticalButton.Location = new System.Drawing.Point(248, 4);
            this._flipVerticalButton.Margin = new System.Windows.Forms.Padding(4);
            this._flipVerticalButton.Name = "_flipVerticalButton";
            this._flipVerticalButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._flipVerticalButton.Size = new System.Drawing.Size(116, 96);
            this._flipVerticalButton.TabIndex = 2;
            this._flipVerticalButton.Text = "垂直翻转";
            this._flipVerticalButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._flipVerticalButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._flipVerticalButton.UseVisualStyleBackColor = false;
            // 
            // _rotateCounterClockwiseButton
            // 
            this._rotateCounterClockwiseButton.BackColor = System.Drawing.Color.White;
            this._rotateCounterClockwiseButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._rotateCounterClockwiseButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._rotateCounterClockwiseButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._rotateCounterClockwiseButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._rotateCounterClockwiseButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._rotateCounterClockwiseButton.Image = global::CxECTImageWinForms.Properties.Resources._01_旋转90度;
            this._rotateCounterClockwiseButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._rotateCounterClockwiseButton.Location = new System.Drawing.Point(4, 108);
            this._rotateCounterClockwiseButton.Margin = new System.Windows.Forms.Padding(4);
            this._rotateCounterClockwiseButton.Name = "_rotateCounterClockwiseButton";
            this._rotateCounterClockwiseButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._rotateCounterClockwiseButton.Size = new System.Drawing.Size(114, 96);
            this._rotateCounterClockwiseButton.TabIndex = 3;
            this._rotateCounterClockwiseButton.Text = "逆时针 90°";
            this._rotateCounterClockwiseButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._rotateCounterClockwiseButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._rotateCounterClockwiseButton.UseVisualStyleBackColor = false;
            // 
            // _resetTransformButton
            // 
            this._resetTransformButton.BackColor = System.Drawing.Color.White;
            this._resetTransformButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._resetTransformButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._resetTransformButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._resetTransformButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._resetTransformButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._resetTransformButton.Image = global::CxECTImageWinForms.Properties.Resources._04_重置变换;
            this._resetTransformButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._resetTransformButton.Location = new System.Drawing.Point(126, 108);
            this._resetTransformButton.Margin = new System.Windows.Forms.Padding(4);
            this._resetTransformButton.Name = "_resetTransformButton";
            this._resetTransformButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._resetTransformButton.Size = new System.Drawing.Size(114, 96);
            this._resetTransformButton.TabIndex = 4;
            this._resetTransformButton.Text = "重置变换";
            this._resetTransformButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._resetTransformButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._resetTransformButton.UseVisualStyleBackColor = false;
            // 
            // _cropToRoiButton
            // 
            this._cropToRoiButton.BackColor = System.Drawing.Color.White;
            this._cropToRoiButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this._cropToRoiButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._cropToRoiButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._cropToRoiButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._cropToRoiButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._cropToRoiButton.Image = global::CxECTImageWinForms.Properties.Resources._06_裁剪ROI_矩形选区;
            this._cropToRoiButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._cropToRoiButton.Location = new System.Drawing.Point(248, 108);
            this._cropToRoiButton.Margin = new System.Windows.Forms.Padding(4);
            this._cropToRoiButton.Name = "_cropToRoiButton";
            this._cropToRoiButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._cropToRoiButton.Size = new System.Drawing.Size(116, 96);
            this._cropToRoiButton.TabIndex = 5;
            this._cropToRoiButton.Text = "裁剪 ROI";
            this._cropToRoiButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._cropToRoiButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._cropToRoiButton.UseVisualStyleBackColor = false;
            // 
            // _outputPage
            // 
            this._outputPage.Controls.Add(this._outputActionHost);
            this._outputPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._outputPage.Location = new System.Drawing.Point(0, 0);
            this._outputPage.Name = "_outputPage";
            this._outputPage.Size = new System.Drawing.Size(388, 520);
            this._outputPage.TabIndex = 3;
            this._outputPage.Visible = false;
            // 
            // _outputActionHost
            // 
            this._outputActionHost.Controls.Add(this._outputActionPanel);
            this._outputActionHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._outputActionHost.Location = new System.Drawing.Point(0, 0);
            this._outputActionHost.Name = "_outputActionHost";
            this._outputActionHost.Size = new System.Drawing.Size(388, 520);
            this._outputActionHost.TabIndex = 0;
            // 
            // _outputActionPanel
            // 
            this._outputActionPanel.AutoScroll = true;
            this._outputActionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._outputActionPanel.Controls.Add(this._toggleImageButton);
            this._outputActionPanel.Controls.Add(this._syncViewButton);
            this._outputActionPanel.Controls.Add(this._blendImageButton);
            this._outputActionPanel.Controls.Add(this._saveImageButton);
            this._outputActionPanel.Controls.Add(this._copyClipboardButton);
            this._outputActionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._outputActionPanel.Location = new System.Drawing.Point(0, 0);
            this._outputActionPanel.Name = "_outputActionPanel";
            this._outputActionPanel.Padding = new System.Windows.Forms.Padding(8);
            this._outputActionPanel.Size = new System.Drawing.Size(388, 520);
            this._outputActionPanel.TabIndex = 0;
            // 
            // _toggleImageButton
            // 
            this._toggleImageButton.BackColor = System.Drawing.Color.White;
            this._toggleImageButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._toggleImageButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._toggleImageButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._toggleImageButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._toggleImageButton.Image = global::CxECTImageWinForms.Properties.Resources._01_原图结果_对比滑杆;
            this._toggleImageButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._toggleImageButton.Location = new System.Drawing.Point(11, 11);
            this._toggleImageButton.Name = "_toggleImageButton";
            this._toggleImageButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._toggleImageButton.Size = new System.Drawing.Size(96, 96);
            this._toggleImageButton.TabIndex = 0;
            this._toggleImageButton.Tag = "Command";
            this._toggleImageButton.Text = "原图/结果";
            this._toggleImageButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._toggleImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._toggleImageButton.UseVisualStyleBackColor = false;
            // 
            // _syncViewButton
            // 
            this._syncViewButton.BackColor = System.Drawing.Color.White;
            this._syncViewButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._syncViewButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._syncViewButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._syncViewButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._syncViewButton.Image = global::CxECTImageWinForms.Properties.Resources._03_双图同步_循环箭头;
            this._syncViewButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._syncViewButton.Location = new System.Drawing.Point(113, 11);
            this._syncViewButton.Name = "_syncViewButton";
            this._syncViewButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._syncViewButton.Size = new System.Drawing.Size(96, 96);
            this._syncViewButton.TabIndex = 1;
            this._syncViewButton.Tag = "Command";
            this._syncViewButton.Text = "双图同步";
            this._syncViewButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._syncViewButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._syncViewButton.UseVisualStyleBackColor = false;
            // 
            // _blendImageButton
            // 
            this._blendImageButton.BackColor = System.Drawing.Color.White;
            this._blendImageButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._blendImageButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._blendImageButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._blendImageButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._blendImageButton.Image = global::CxECTImageWinForms.Properties.Resources._02_图像叠合_双图覆盖;
            this._blendImageButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._blendImageButton.Location = new System.Drawing.Point(215, 11);
            this._blendImageButton.Name = "_blendImageButton";
            this._blendImageButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._blendImageButton.Size = new System.Drawing.Size(96, 96);
            this._blendImageButton.TabIndex = 2;
            this._blendImageButton.Tag = "Command";
            this._blendImageButton.Text = "图像融合";
            this._blendImageButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._blendImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._blendImageButton.UseVisualStyleBackColor = false;
            // 
            // _saveImageButton
            // 
            this._saveImageButton.BackColor = System.Drawing.Color.White;
            this._saveImageButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._saveImageButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._saveImageButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._saveImageButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._saveImageButton.Image = global::CxECTImageWinForms.Properties.Resources._04_保存图像_下载;
            this._saveImageButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._saveImageButton.Location = new System.Drawing.Point(11, 113);
            this._saveImageButton.Name = "_saveImageButton";
            this._saveImageButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._saveImageButton.Size = new System.Drawing.Size(96, 96);
            this._saveImageButton.TabIndex = 3;
            this._saveImageButton.Tag = "Command";
            this._saveImageButton.Text = "保存图像";
            this._saveImageButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._saveImageButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._saveImageButton.UseVisualStyleBackColor = false;
            // 
            // _copyClipboardButton
            // 
            this._copyClipboardButton.BackColor = System.Drawing.Color.White;
            this._copyClipboardButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._copyClipboardButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._copyClipboardButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._copyClipboardButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._copyClipboardButton.Image = global::CxECTImageWinForms.Properties.Resources._05_复制到剪贴板;
            this._copyClipboardButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._copyClipboardButton.Location = new System.Drawing.Point(113, 113);
            this._copyClipboardButton.Name = "_copyClipboardButton";
            this._copyClipboardButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._copyClipboardButton.Size = new System.Drawing.Size(96, 96);
            this._copyClipboardButton.TabIndex = 4;
            this._copyClipboardButton.Tag = "Command";
            this._copyClipboardButton.Text = "复制到剪贴板";
            this._copyClipboardButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._copyClipboardButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._copyClipboardButton.UseVisualStyleBackColor = false;
            // 
            // _pixelAnalysisPage
            // 
            this._pixelAnalysisPage.Controls.Add(this._statisticsAnalysisPanel);
            this._pixelAnalysisPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pixelAnalysisPage.Location = new System.Drawing.Point(0, 0);
            this._pixelAnalysisPage.Name = "_pixelAnalysisPage";
            this._pixelAnalysisPage.Size = new System.Drawing.Size(388, 520);
            this._pixelAnalysisPage.TabIndex = 1;
            this._pixelAnalysisPage.Visible = false;
            // 
            // _statisticsAnalysisPanel
            // 
            this._statisticsAnalysisPanel.BackColor = System.Drawing.Color.White;
            this._statisticsAnalysisPanel.Controls.Add(this._pixelAnalysisActionHost);
            this._statisticsAnalysisPanel.Controls.Add(this._pixelAnalysisActionPanel);
            this._statisticsAnalysisPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._statisticsAnalysisPanel.Location = new System.Drawing.Point(0, 0);
            this._statisticsAnalysisPanel.Margin = new System.Windows.Forms.Padding(0);
            this._statisticsAnalysisPanel.Name = "_statisticsAnalysisPanel";
            this._statisticsAnalysisPanel.Size = new System.Drawing.Size(388, 520);
            this._statisticsAnalysisPanel.TabIndex = 0;
            // 
            // _pixelAnalysisActionHost
            // 
            this._pixelAnalysisActionHost.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._pixelAnalysisActionHost.Controls.Add(this._histogramPlot);
            this._pixelAnalysisActionHost.Controls.Add(this._grayLevelScale);
            this._pixelAnalysisActionHost.Controls.Add(this._statisticsSummaryLabel);
            this._pixelAnalysisActionHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pixelAnalysisActionHost.Location = new System.Drawing.Point(0, 0);
            this._pixelAnalysisActionHost.Name = "_pixelAnalysisActionHost";
            this._pixelAnalysisActionHost.Size = new System.Drawing.Size(388, 408);
            this._pixelAnalysisActionHost.TabIndex = 1;
            // 
            // _histogramPlot
            // 
            this._histogramPlot.BackColor = System.Drawing.Color.White;
            this._histogramPlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this._histogramPlot.Location = new System.Drawing.Point(0, 60);
            this._histogramPlot.Margin = new System.Windows.Forms.Padding(10);
            this._histogramPlot.MinimumSize = new System.Drawing.Size(180, 90);
            this._histogramPlot.Name = "_histogramPlot";
            this._histogramPlot.Size = new System.Drawing.Size(388, 348);
            this._histogramPlot.TabIndex = 3;
            // 
            // _grayLevelScale
            // 
            this._grayLevelScale.BackColor = System.Drawing.Color.White;
            this._grayLevelScale.Dock = System.Windows.Forms.DockStyle.Top;
            this._grayLevelScale.Location = new System.Drawing.Point(0, 32);
            this._grayLevelScale.Margin = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._grayLevelScale.Maximum = 255D;
            this._grayLevelScale.Minimum = 0D;
            this._grayLevelScale.Name = "_grayLevelScale";
            this._grayLevelScale.Size = new System.Drawing.Size(388, 28);
            this._grayLevelScale.TabIndex = 1;
            // 
            // _statisticsSummaryLabel
            // 
            this._statisticsSummaryLabel.Dock = System.Windows.Forms.DockStyle.Top;
            this._statisticsSummaryLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(70)))), ((int)(((byte)(80)))), ((int)(((byte)(95)))));
            this._statisticsSummaryLabel.Location = new System.Drawing.Point(0, 0);
            this._statisticsSummaryLabel.Name = "_statisticsSummaryLabel";
            this._statisticsSummaryLabel.Padding = new System.Windows.Forms.Padding(10, 4, 10, 4);
            this._statisticsSummaryLabel.Size = new System.Drawing.Size(388, 32);
            this._statisticsSummaryLabel.TabIndex = 0;
            this._statisticsSummaryLabel.Text = "统计：等待图像";
            this._statisticsSummaryLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _pixelAnalysisActionPanel
            // 
            this._pixelAnalysisActionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._pixelAnalysisActionPanel.Controls.Add(this._pixelStatisticsButton);
            this._pixelAnalysisActionPanel.Controls.Add(this._histogramButton);
            this._pixelAnalysisActionPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._pixelAnalysisActionPanel.Location = new System.Drawing.Point(0, 408);
            this._pixelAnalysisActionPanel.Name = "_pixelAnalysisActionPanel";
            this._pixelAnalysisActionPanel.Padding = new System.Windows.Forms.Padding(8);
            this._pixelAnalysisActionPanel.Size = new System.Drawing.Size(388, 112);
            this._pixelAnalysisActionPanel.TabIndex = 0;
            // 
            // _pixelStatisticsButton
            // 
            this._pixelStatisticsButton.BackColor = System.Drawing.Color.White;
            this._pixelStatisticsButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._pixelStatisticsButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._pixelStatisticsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._pixelStatisticsButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._pixelStatisticsButton.Image = global::CxECTImageWinForms.Properties.Resources._02_MinMaxMean_三柱统计;
            this._pixelStatisticsButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._pixelStatisticsButton.Location = new System.Drawing.Point(113, 11);
            this._pixelStatisticsButton.Name = "_pixelStatisticsButton";
            this._pixelStatisticsButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._pixelStatisticsButton.Size = new System.Drawing.Size(96, 96);
            this._pixelStatisticsButton.TabIndex = 1;
            this._pixelStatisticsButton.Tag = "Command";
            this._pixelStatisticsButton.Text = "Min/Max/Mean";
            this._pixelStatisticsButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._pixelStatisticsButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._pixelStatisticsButton.UseVisualStyleBackColor = false;
            // 
            // _histogramButton
            // 
            this._histogramButton.BackColor = System.Drawing.Color.White;
            this._histogramButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._histogramButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._histogramButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._histogramButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._histogramButton.Image = global::CxECTImageWinForms.Properties.Resources._03_直方图_柱状图;
            this._histogramButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._histogramButton.Location = new System.Drawing.Point(215, 11);
            this._histogramButton.Name = "_histogramButton";
            this._histogramButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._histogramButton.Size = new System.Drawing.Size(96, 96);
            this._histogramButton.TabIndex = 2;
            this._histogramButton.Tag = "Command";
            this._histogramButton.Text = "直方图";
            this._histogramButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._histogramButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._histogramButton.UseVisualStyleBackColor = false;
            // 
            // _roiPage
            // 
            this._roiPage.Controls.Add(this._roiActionHost);
            this._roiPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._roiPage.Location = new System.Drawing.Point(0, 0);
            this._roiPage.Name = "_roiPage";
            this._roiPage.Size = new System.Drawing.Size(388, 520);
            this._roiPage.TabIndex = 0;
            // 
            // _roiActionHost
            // 
            this._roiActionHost.Controls.Add(this._roiActionPanel);
            this._roiActionHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._roiActionHost.Location = new System.Drawing.Point(0, 0);
            this._roiActionHost.Name = "_roiActionHost";
            this._roiActionHost.Size = new System.Drawing.Size(388, 520);
            this._roiActionHost.TabIndex = 0;
            // 
            // _roiActionPanel
            // 
            this._roiActionPanel.AutoScroll = true;
            this._roiActionPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._roiActionPanel.Controls.Add(this._rectangleRoiButton);
            this._roiActionPanel.Controls.Add(this._clearRoiButton);
            this._roiActionPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._roiActionPanel.Location = new System.Drawing.Point(0, 0);
            this._roiActionPanel.Name = "_roiActionPanel";
            this._roiActionPanel.Padding = new System.Windows.Forms.Padding(8);
            this._roiActionPanel.Size = new System.Drawing.Size(388, 520);
            this._roiActionPanel.TabIndex = 0;
            // 
            // _rectangleRoiButton
            // 
            this._rectangleRoiButton.BackColor = System.Drawing.Color.White;
            this._rectangleRoiButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._rectangleRoiButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._rectangleRoiButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._rectangleRoiButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._rectangleRoiButton.Image = global::CxECTImageWinForms.Properties.Resources._01_矩形ROI_中心十字准星;
            this._rectangleRoiButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._rectangleRoiButton.Location = new System.Drawing.Point(11, 11);
            this._rectangleRoiButton.Name = "_rectangleRoiButton";
            this._rectangleRoiButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._rectangleRoiButton.Size = new System.Drawing.Size(96, 96);
            this._rectangleRoiButton.TabIndex = 0;
            this._rectangleRoiButton.Tag = "ExclusiveMode";
            this._rectangleRoiButton.Text = "矩形 ROI";
            this._rectangleRoiButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._rectangleRoiButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._rectangleRoiButton.UseVisualStyleBackColor = false;
            // 
            // _clearRoiButton
            // 
            this._clearRoiButton.BackColor = System.Drawing.Color.White;
            this._clearRoiButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._clearRoiButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._clearRoiButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._clearRoiButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._clearRoiButton.Image = global::CxECTImageWinForms.Properties.Resources._05_清空ROI_橡皮擦;
            this._clearRoiButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._clearRoiButton.Location = new System.Drawing.Point(113, 11);
            this._clearRoiButton.Name = "_clearRoiButton";
            this._clearRoiButton.Padding = new System.Windows.Forms.Padding(3, 6, 3, 6);
            this._clearRoiButton.Size = new System.Drawing.Size(96, 96);
            this._clearRoiButton.TabIndex = 1;
            this._clearRoiButton.Tag = "Command";
            this._clearRoiButton.Text = "清空 ROI";
            this._clearRoiButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._clearRoiButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._clearRoiButton.UseVisualStyleBackColor = false;
            // 
            // _profileAnalysisPage
            // 
            this._profileAnalysisPage.Controls.Add(this._profileAnalysisPanel);
            this._profileAnalysisPage.Dock = System.Windows.Forms.DockStyle.Fill;
            this._profileAnalysisPage.Location = new System.Drawing.Point(0, 0);
            this._profileAnalysisPage.Name = "_profileAnalysisPage";
            this._profileAnalysisPage.Size = new System.Drawing.Size(388, 520);
            this._profileAnalysisPage.TabIndex = 2;
            this._profileAnalysisPage.Visible = false;
            // 
            // _profileAnalysisPanel
            // 
            this._profileAnalysisPanel.BackColor = System.Drawing.Color.White;
            this._profileAnalysisPanel.Controls.Add(this._lineProfilePlot);
            this._profileAnalysisPanel.Controls.Add(this._lineProfileButton);
            this._profileAnalysisPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this._profileAnalysisPanel.Location = new System.Drawing.Point(0, 0);
            this._profileAnalysisPanel.Margin = new System.Windows.Forms.Padding(0);
            this._profileAnalysisPanel.Name = "_profileAnalysisPanel";
            this._profileAnalysisPanel.Size = new System.Drawing.Size(388, 520);
            this._profileAnalysisPanel.TabIndex = 1;
            // 
            // _lineProfilePlot
            // 
            this._lineProfilePlot.BackColor = System.Drawing.Color.White;
            this._lineProfilePlot.Dock = System.Windows.Forms.DockStyle.Fill;
            this._lineProfilePlot.Location = new System.Drawing.Point(0, 0);
            this._lineProfilePlot.Margin = new System.Windows.Forms.Padding(10);
            this._lineProfilePlot.MinimumSize = new System.Drawing.Size(180, 80);
            this._lineProfilePlot.Name = "_lineProfilePlot";
            this._lineProfilePlot.Size = new System.Drawing.Size(388, 416);
            this._lineProfilePlot.TabIndex = 2;
            // 
            // _lineProfileButton
            // 
            this._lineProfileButton.BackColor = System.Drawing.Color.White;
            this._lineProfileButton.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._lineProfileButton.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(222)))), ((int)(((byte)(232)))));
            this._lineProfileButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(234)))), ((int)(((byte)(243)))), ((int)(((byte)(255)))));
            this._lineProfileButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._lineProfileButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._lineProfileButton.Image = global::CxECTImageWinForms.Properties.Resources._04_灰度剖面_强度曲线;
            this._lineProfileButton.ImageAlign = System.Drawing.ContentAlignment.TopCenter;
            this._lineProfileButton.Location = new System.Drawing.Point(0, 416);
            this._lineProfileButton.Margin = new System.Windows.Forms.Padding(0);
            this._lineProfileButton.Name = "_lineProfileButton";
            this._lineProfileButton.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._lineProfileButton.Size = new System.Drawing.Size(388, 104);
            this._lineProfileButton.TabIndex = 3;
            this._lineProfileButton.Tag = "ExclusiveMode";
            this._lineProfileButton.Text = "灰度剖面";
            this._lineProfileButton.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this._lineProfileButton.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
            this._lineProfileButton.UseVisualStyleBackColor = false;
            // 
            // _operationHeader
            // 
            this._operationHeader.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(246)))), ((int)(((byte)(248)))), ((int)(((byte)(251)))));
            this._operationHeader.Controls.Add(this._operationAccent);
            this._operationHeader.Controls.Add(this._roiTabButton);
            this._operationHeader.Controls.Add(this._operationSeparator2);
            this._operationHeader.Controls.Add(this._pixelAnalysisTabButton);
            this._operationHeader.Controls.Add(this._operationSeparator3);
            this._operationHeader.Controls.Add(this._profileAnalysisTabButton);
            this._operationHeader.Controls.Add(this._operationSeparator4);
            this._operationHeader.Controls.Add(this._transformTabButton);
            this._operationHeader.Controls.Add(this._operationSeparator5);
            this._operationHeader.Controls.Add(this._outputTabButton);
            this._operationHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this._operationHeader.Location = new System.Drawing.Point(0, 0);
            this._operationHeader.Name = "_operationHeader";
            this._operationHeader.Size = new System.Drawing.Size(388, 40);
            this._operationHeader.TabIndex = 0;
            // 
            // _operationAccent
            // 
            this._operationAccent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(231)))), ((int)(((byte)(56)))), ((int)(((byte)(56)))));
            this._operationAccent.Location = new System.Drawing.Point(12, 10);
            this._operationAccent.Name = "_operationAccent";
            this._operationAccent.Size = new System.Drawing.Size(3, 20);
            this._operationAccent.TabIndex = 0;
            // 
            // _roiTabButton
            // 
            this._roiTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._roiTabButton.FlatAppearance.BorderSize = 0;
            this._roiTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._roiTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._roiTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._roiTabButton.Location = new System.Drawing.Point(28, 0);
            this._roiTabButton.Name = "_roiTabButton";
            this._roiTabButton.Size = new System.Drawing.Size(54, 39);
            this._roiTabButton.TabIndex = 1;
            this._roiTabButton.Text = "区域";
            this._roiTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSeparator2
            // 
            this._operationSeparator2.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._operationSeparator2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._operationSeparator2.Location = new System.Drawing.Point(82, 0);
            this._operationSeparator2.Name = "_operationSeparator2";
            this._operationSeparator2.Size = new System.Drawing.Size(8, 39);
            this._operationSeparator2.TabIndex = 2;
            this._operationSeparator2.Text = "|";
            this._operationSeparator2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _pixelAnalysisTabButton
            // 
            this._pixelAnalysisTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._pixelAnalysisTabButton.FlatAppearance.BorderSize = 0;
            this._pixelAnalysisTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._pixelAnalysisTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._pixelAnalysisTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._pixelAnalysisTabButton.Location = new System.Drawing.Point(90, 0);
            this._pixelAnalysisTabButton.Name = "_pixelAnalysisTabButton";
            this._pixelAnalysisTabButton.Size = new System.Drawing.Size(54, 39);
            this._pixelAnalysisTabButton.TabIndex = 3;
            this._pixelAnalysisTabButton.Text = "统计";
            this._pixelAnalysisTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSeparator3
            // 
            this._operationSeparator3.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._operationSeparator3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._operationSeparator3.Location = new System.Drawing.Point(144, 0);
            this._operationSeparator3.Name = "_operationSeparator3";
            this._operationSeparator3.Size = new System.Drawing.Size(8, 39);
            this._operationSeparator3.TabIndex = 4;
            this._operationSeparator3.Text = "|";
            this._operationSeparator3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _profileAnalysisTabButton
            // 
            this._profileAnalysisTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._profileAnalysisTabButton.FlatAppearance.BorderSize = 0;
            this._profileAnalysisTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._profileAnalysisTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._profileAnalysisTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._profileAnalysisTabButton.Location = new System.Drawing.Point(152, 0);
            this._profileAnalysisTabButton.Name = "_profileAnalysisTabButton";
            this._profileAnalysisTabButton.Size = new System.Drawing.Size(54, 39);
            this._profileAnalysisTabButton.TabIndex = 5;
            this._profileAnalysisTabButton.Text = "剖面";
            this._profileAnalysisTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSeparator4
            // 
            this._operationSeparator4.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._operationSeparator4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._operationSeparator4.Location = new System.Drawing.Point(206, 0);
            this._operationSeparator4.Name = "_operationSeparator4";
            this._operationSeparator4.Size = new System.Drawing.Size(8, 39);
            this._operationSeparator4.TabIndex = 6;
            this._operationSeparator4.Text = "|";
            this._operationSeparator4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _transformTabButton
            // 
            this._transformTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._transformTabButton.FlatAppearance.BorderSize = 0;
            this._transformTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._transformTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._transformTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._transformTabButton.Location = new System.Drawing.Point(214, 0);
            this._transformTabButton.Name = "_transformTabButton";
            this._transformTabButton.Size = new System.Drawing.Size(54, 39);
            this._transformTabButton.TabIndex = 7;
            this._transformTabButton.Text = "变换";
            this._transformTabButton.UseVisualStyleBackColor = false;
            // 
            // _operationSeparator5
            // 
            this._operationSeparator5.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._operationSeparator5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(127)))), ((int)(((byte)(230)))));
            this._operationSeparator5.Location = new System.Drawing.Point(268, 0);
            this._operationSeparator5.Name = "_operationSeparator5";
            this._operationSeparator5.Size = new System.Drawing.Size(8, 39);
            this._operationSeparator5.TabIndex = 8;
            this._operationSeparator5.Text = "|";
            this._operationSeparator5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _outputTabButton
            // 
            this._outputTabButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(249)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this._outputTabButton.FlatAppearance.BorderSize = 0;
            this._outputTabButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this._outputTabButton.Font = new System.Drawing.Font("Microsoft YaHei UI", 10.5F, System.Drawing.FontStyle.Bold);
            this._outputTabButton.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(50)))), ((int)(((byte)(64)))));
            this._outputTabButton.Location = new System.Drawing.Point(276, 0);
            this._outputTabButton.Name = "_outputTabButton";
            this._outputTabButton.Size = new System.Drawing.Size(54, 39);
            this._outputTabButton.TabIndex = 9;
            this._outputTabButton.Text = "输出";
            this._outputTabButton.UseVisualStyleBackColor = false;
            // 
            // _statusBar
            // 
            this._statusBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this._statusBar.ImageScalingSize = new System.Drawing.Size(24, 24);
            this._statusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this._zoomStatus,
            this._imageStatus,
            this._pixelStatus,
            this._operationStatus});
            this._statusBar.Location = new System.Drawing.Point(0, 1014);
            this._statusBar.Name = "_statusBar";
            this._statusBar.Size = new System.Drawing.Size(1580, 24);
            this._statusBar.SizingGrip = false;
            this._statusBar.TabIndex = 2;
            // 
            // _zoomStatus
            // 
            this._zoomStatus.Name = "_zoomStatus";
            this._zoomStatus.Size = new System.Drawing.Size(81, 19);
            this._zoomStatus.Text = "Zoom: 100%";
            // 
            // _imageStatus
            // 
            this._imageStatus.AutoSize = true;
            this._imageStatus.Name = "_imageStatus";
            this._imageStatus.Size = new System.Drawing.Size(49, 19);
            this._imageStatus.Text = "图像: --";
            // 
            // _pixelStatus
            // 
            this._pixelStatus.Name = "_pixelStatus";
            this._pixelStatus.Size = new System.Drawing.Size(97, 19);
            this._pixelStatus.Text = "X/Y: --  Gray: --";
            // 
            // _operationStatus
            // 
            this._operationStatus.Name = "_operationStatus";
            this._operationStatus.Size = new System.Drawing.Size(32, 19);
            this._operationStatus.Text = "就绪";
            // 
            // ImageViewerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(247)))), ((int)(((byte)(250)))));
            this.Controls.Add(this._rootLayout);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            this.Name = "ImageViewerControl";
            this.Size = new System.Drawing.Size(1580, 1038);
            this._rootLayout.ResumeLayout(false);
            this._rootLayout.PerformLayout();
            this._toolbar.ResumeLayout(false);
            this._toolbar.PerformLayout();
            this._contentSplit.Panel1.ResumeLayout(false);
            this._contentSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._contentSplit)).EndInit();
            this._contentSplit.ResumeLayout(false);
            this._viewerRulerLayout.ResumeLayout(false);
            this._viewerHost.ResumeLayout(false);
            this._viewerHost.PerformLayout();
            this._toolPanelSplit.Panel1.ResumeLayout(false);
            this._toolPanelSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._toolPanelSplit)).EndInit();
            this._toolPanelSplit.ResumeLayout(false);
            this._displaySection.ResumeLayout(false);
            this._displayPageHost.ResumeLayout(false);
            this._windowLevelPage.ResumeLayout(false);
            this._windowLevelPanel.ResumeLayout(false);
            this._windowLevelLayout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._windowValueEditor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._windowTrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._levelValueEditor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._levelTrackBar)).EndInit();
            this._measurementPage.ResumeLayout(false);
            this._measurementActionHost.ResumeLayout(false);
            this._measurementActionPanel.ResumeLayout(false);
            this._tonePage.ResumeLayout(false);
            this._tonePanel.ResumeLayout(false);
            this._toneLayout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._gammaTrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._brightnessTrackBar)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._contrastTrackBar)).EndInit();
            this._annotationPage.ResumeLayout(false);
            this._annotationActionHost.ResumeLayout(false);
            this._annotationActionPanel.ResumeLayout(false);
            this._displaySettingsPage.ResumeLayout(false);
            this._layerVisibilityLayout.ResumeLayout(false);
            this._overlayColorLayout.ResumeLayout(false);
            this._displayHeader.ResumeLayout(false);
            this._operationSection.ResumeLayout(false);
            this._operationPageHost.ResumeLayout(false);
            this._transformPanel.ResumeLayout(false);
            this._rotationCommandPanel.ResumeLayout(false);
            this._rotationAngleInputPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._rotationAngleInput)).EndInit();
            this._transformCommandPanel.ResumeLayout(false);
            this._outputPage.ResumeLayout(false);
            this._outputActionHost.ResumeLayout(false);
            this._outputActionPanel.ResumeLayout(false);
            this._pixelAnalysisPage.ResumeLayout(false);
            this._statisticsAnalysisPanel.ResumeLayout(false);
            this._pixelAnalysisActionHost.ResumeLayout(false);
            this._pixelAnalysisActionPanel.ResumeLayout(false);
            this._roiPage.ResumeLayout(false);
            this._roiActionHost.ResumeLayout(false);
            this._roiActionPanel.ResumeLayout(false);
            this._profileAnalysisPage.ResumeLayout(false);
            this._profileAnalysisPanel.ResumeLayout(false);
            this._operationHeader.ResumeLayout(false);
            this._statusBar.ResumeLayout(false);
            this._statusBar.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel _rotationAngleInputPanel;
    }
}
