using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace UniversalResizeLib
{
    public class UniversalResizeForm : Form
    {
        private sealed class ControlLayoutInfo
        {
            public Rectangle Bounds { get; set; }
            public float FontSize { get; set; }
        }

        private readonly Dictionary<Control, ControlLayoutInfo> _layoutInfo =
            new Dictionary<Control, ControlLayoutInfo>();

        private Size _originalClientSize;
        private bool _layoutCaptured;
        private bool _isApplyingResize;
        private Timer _resizeTimer;

        protected virtual bool EnableFontResize { get { return true; } }
        protected virtual float MinimumFontSize { get { return 6f; } }
        protected virtual int ResizeDelayMs { get { return 60; } }
        protected virtual int MinimumControlWidth { get { return 10; } }
        protected virtual int MinimumControlHeight { get { return 10; } }

        public UniversalResizeForm()
        {
            // Keep constructor empty for designer safety.
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            if (IsDesignerSafe())
                return;

            EnsureInitialized();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (IsDesignerSafe())
                return;

            if (!_layoutCaptured)
                return;

            EnsureTimer();
            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        protected virtual void EnsureInitialized()
        {
            EnsureTimer();

            if (!_layoutCaptured)
                CaptureOriginalLayout();
        }

        private void EnsureTimer()
        {
            if (_resizeTimer != null)
                return;

            _resizeTimer = new Timer();
            _resizeTimer.Interval = ResizeDelayMs;
            _resizeTimer.Tick += ResizeTimer_Tick;
        }

        private void ResizeTimer_Tick(object sender, EventArgs e)
        {
            _resizeTimer.Stop();
            ApplyResize();
        }

        protected virtual bool IsDesignerSafe()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime
                   || DesignMode
                   || (Site != null && Site.DesignMode)
                   || ProcessNameIsDesigner();
        }

        private static bool ProcessNameIsDesigner()
        {
            try
            {
                string name = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                return string.Equals(name, "devenv", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(name, "xdesproc", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        protected void CaptureOriginalLayout()
        {
            if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            _layoutInfo.Clear();
            _originalClientSize = ClientSize;
            CaptureControlTree(this);
            _layoutCaptured = true;
        }

        private void CaptureControlTree(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (!_layoutInfo.ContainsKey(ctrl))
                {
                    _layoutInfo.Add(ctrl, new ControlLayoutInfo
                    {
                        Bounds = ctrl.Bounds,
                        FontSize = ctrl.Font.Size
                    });
                }

                if (ctrl.HasChildren)
                    CaptureControlTree(ctrl);
            }
        }

        protected void ApplyResize()
        {
            if (_isApplyingResize)
                return;

            if (!_layoutCaptured)
                return;

            if (_originalClientSize.Width <= 0 || _originalClientSize.Height <= 0)
                return;

            float xRatio = (float)ClientSize.Width / _originalClientSize.Width;
            float yRatio = (float)ClientSize.Height / _originalClientSize.Height;

            _isApplyingResize = true;
            SuspendLayout();
            try
            {
                ResizeControlTree(this, xRatio, yRatio);
            }
            finally
            {
                ResumeLayout();
                _isApplyingResize = false;
            }
        }

        private void ResizeControlTree(Control parent, float xRatio, float yRatio)
        {
            foreach (Control ctrl in parent.Controls)
            {
                ControlLayoutInfo info;
                if (!_layoutInfo.TryGetValue(ctrl, out info))
                {
                    if (ctrl.HasChildren)
                        ResizeControlTree(ctrl, xRatio, yRatio);
                    continue;
                }

                string tag = Convert.ToString(ctrl.Tag) ?? string.Empty;
                string lowerTag = tag.ToLowerInvariant();

                bool noResize = lowerTag.Contains("noresize");
                bool noFontResize = lowerTag.Contains("nofontresize");
                bool moveOnly = lowerTag.Contains("moveonly");
                bool widthOnly = lowerTag.Contains("widthonly");
                bool heightOnly = lowerTag.Contains("heightonly");

                int newX = ctrl.Left;
                int newY = ctrl.Top;
                int newWidth = ctrl.Width;
                int newHeight = ctrl.Height;

                if (!noResize)
                {
                    if (widthOnly)
                    {
                        newWidth = Math.Max(MinimumControlWidth, (int)(info.Bounds.Width * xRatio));
                    }
                    else if (heightOnly)
                    {
                        newHeight = Math.Max(MinimumControlHeight, (int)(info.Bounds.Height * yRatio));
                    }
                    else if (moveOnly)
                    {
                        newX = (int)(info.Bounds.X * xRatio);
                        newY = (int)(info.Bounds.Y * yRatio);
                    }
                    else
                    {
                        newX = (int)(info.Bounds.X * xRatio);
                        newY = (int)(info.Bounds.Y * yRatio);
                        newWidth = Math.Max(MinimumControlWidth, (int)(info.Bounds.Width * xRatio));
                        newHeight = Math.Max(MinimumControlHeight, (int)(info.Bounds.Height * yRatio));
                    }

                    ctrl.SetBounds(newX, newY, newWidth, newHeight);
                }

                if (EnableFontResize && !noFontResize)
                {
                    float scale = Math.Min(xRatio, yRatio);
                    float newFontSize = Math.Max(MinimumFontSize, info.FontSize * scale);

                    if (Math.Abs(ctrl.Font.Size - newFontSize) > 0.20f)
                    {
                        ctrl.Font = new Font(ctrl.Font.FontFamily, newFontSize, ctrl.Font.Style);
                    }
                }

                var dgv = ctrl as DataGridView;
                if (dgv != null)
                {
                    dgv.RowTemplate.Height = Math.Max(18, (int)(22 * yRatio));
                    dgv.ColumnHeadersHeight = Math.Max(20, (int)(24 * yRatio));
                }

                if (ctrl.HasChildren)
                    ResizeControlTree(ctrl, xRatio, yRatio);
            }
        }

        protected void ReinitializeResizeLayout()
        {
            CaptureOriginalLayout();
            ApplyResize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _resizeTimer != null)
            {
                _resizeTimer.Stop();
                _resizeTimer.Tick -= ResizeTimer_Tick;
                _resizeTimer.Dispose();
                _resizeTimer = null;
            }

            base.Dispose(disposing);
        }
    }
}
