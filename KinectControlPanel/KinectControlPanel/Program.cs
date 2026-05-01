using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Kinect;
using System.IO;

namespace KinectControlPanel
{
    // ══════════════════════════════════════════════════════════════════════════
    //  SHARED MEMORY – usado pelo botão VCam
    // ══════════════════════════════════════════════════════════════════════════
    public class KinectShmWriter : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileMappingW(IntPtr hFile, IntPtr lpAttr,
            uint flProtect, uint dwMaxSizeHigh, uint dwMaxSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hMap, uint dwAccess,
            uint dwOffHigh, uint dwOffLow, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x0002;
        private const int HEADER_SIZE = 16;
        private const string SHM_NAME = "KinectCam_SharedMem";

        private IntPtr _hMap = IntPtr.Zero;
        private IntPtr _pView = IntPtr.Zero;

        public bool Start(int width, int height)
        {
            Stop();
            long total = HEADER_SIZE + (long)width * height * 4;
            _hMap = CreateFileMappingW(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE,
                (uint)(total >> 32), (uint)(total & 0xFFFFFFFF), SHM_NAME);
            if (_hMap == IntPtr.Zero) return false;
            _pView = MapViewOfFile(_hMap, FILE_MAP_WRITE, 0, 0, UIntPtr.Zero);
            if (_pView == IntPtr.Zero) { CloseHandle(_hMap); _hMap = IntPtr.Zero; return false; }
            Marshal.WriteInt32(_pView, 0, width);
            Marshal.WriteInt32(_pView, 4, height);
            return true;
        }

        public void SendFrame(byte[] bgraPixels)
        {
            if (_pView == IntPtr.Zero) return;
            Marshal.Copy(bgraPixels, 0, _pView + HEADER_SIZE, bgraPixels.Length);
        }

        public void Stop()
        {
            if (_pView != IntPtr.Zero) { UnmapViewOfFile(_pView); _pView = IntPtr.Zero; }
            if (_hMap != IntPtr.Zero) { CloseHandle(_hMap); _hMap = IntPtr.Zero; }
        }

        public void Dispose() => Stop();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MAIN FORM
    // ══════════════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        // ── Kinect ─────────────────────────────────────────────────────────────
        private KinectSensor sensor;
        private KinectShmWriter vcamWriter = new KinectShmWriter();

        // ── Estado ─────────────────────────────────────────────────────────────
        private bool useIR = false;
        private bool previewOn = true;
        private bool darkMode = false;
        private bool vcamOn = false;
        private bool drawerOpen = false;
        private int appMode = 0;       // 0=Normal, 1=Câmera, 2=OBS
        private int selectedResIndex = 0;

        // ── Controles ──────────────────────────────────────────────────────────
        private PictureBox previewBox;
        private Label noSignalLabel;
        private TrackBar angleSlider;
        private Label angleLabel;

        private Button upButton, downButton, centerButton;
        private Button irButton, previewToggleButton, drawerButton;
        private Button themeButton, camModeBtn, obsModeBtn, vcamButton;
        private Button[] resButtons;
        private Panel drawerPanel;
        private Panel obsSidebar;

        // ── Drag (modo OBS / Câmera sem barra) ────────────────────────────────
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        // ── Resoluções ─────────────────────────────────────────────────────────
        private static readonly (string Label, ColorImageFormat Format, bool IsIR, int W, int H)[] Resolutions =
        {
            ("640×480 @ 30fps RGB",  ColorImageFormat.RgbResolution640x480Fps30,      false, 640,  480),
            ("1280×960 @ 12fps RGB", ColorImageFormat.RgbResolution1280x960Fps12,     false, 1280, 960),
            ("640×480 @ 30fps IR",   ColorImageFormat.InfraredResolution640x480Fps30, true,  640,  480),
        };

        private int CamW => Resolutions[selectedResIndex].W;
        private int CamH => Resolutions[selectedResIndex].H;

        // Paleta Xbox 360
        private static readonly Color XboxGreen = Color.FromArgb(82, 176, 67);
        private static readonly Color XboxGreenD = Color.FromArgb(55, 130, 42);
        private static readonly Color XboxRed = Color.FromArgb(209, 17, 28);
        private static readonly Color XboxBlack = Color.FromArgb(16, 16, 16);
        private static readonly Color XboxWhite = Color.FromArgb(242, 242, 242);
        private static readonly Color XboxDark = Color.FromArgb(38, 38, 38);

        private string configPath = "config.txt";

        // ══════════════════════════════════════════════════════════════════════
        public MainForm()
        {
            Text = "Kinect Cam";
            Width = 580;
            Height = 540;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 9);

            BuildNormalUI();
            BuildObsSidebar();
            LoadConfig();
            ApplyTheme();
            InitializeKinect();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD – Modo Normal
        // ══════════════════════════════════════════════════════════════════════
        private void BuildNormalUI()
        {
            upButton = MakeBtn("▲", "Subir ângulo", 30, 20);
            downButton = MakeBtn("▼", "Descer ângulo", 155, 20);
            centerButton = MakeBtn("○", "Centralizar", 280, 20);

            irButton = MakeBtn("IR", "Modo IR / RGB", 30, 65);
            previewToggleButton = MakeBtn("👁", "Liga/desliga preview", 155, 65);
            drawerButton = MakeBtn("📐▾", "Resoluções", 290, 65);

            themeButton = MakeBtn("🌙", "Alternar tema", 30, 110);
            camModeBtn = MakeBtn("📷", "Modo Câmera", 155, 110);
            obsModeBtn = MakeBtn("🎥", "Modo OBS", 280, 110);

            vcamButton = MakeBtn("📡 VCam OFF", "Câmera virtual", 30, 155);
            vcamButton.Width = 200;

            angleLabel = new Label
            {
                Text = "Ângulo: 0°",
                Left = 210,
                Top = 165,
                Width = 140,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            angleSlider = new TrackBar
            {
                Minimum = -27,
                Maximum = 27,
                Value = 0,
                Left = 80,
                Top = 188,
                Width = 390,
                TickFrequency = 3
            };

            drawerPanel = new Panel { Left = 30, Top = 195, Width = 490, Height = 0, Visible = false };

            resButtons = new Button[Resolutions.Length];
            for (int i = 0; i < Resolutions.Length; i++)
            {
                int idx = i;
                resButtons[i] = new Button
                {
                    Text = Resolutions[i].Label,
                    Left = 0,
                    Top = i * 36,
                    Width = 490,
                    Height = 34,
                    FlatStyle = FlatStyle.Flat
                };
                resButtons[i].Click += (s, e) =>
                {
                    selectedResIndex = idx;
                    useIR = Resolutions[idx].IsIR;
                    if (vcamOn) { vcamWriter.Stop(); vcamWriter.Start(CamW, CamH); }
                    RestartKinect();
                    ToggleDrawer();
                    ApplyTheme();
                };
                drawerPanel.Controls.Add(resButtons[i]);
            }

            previewBox = new PictureBox
            {
                Left = 90,
                Top = 230,
                Width = 370,
                Height = 210,
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            noSignalLabel = new Label
            {
                Text = "📷 Preview desligado",
                Left = 90,
                Top = 230,
                Width = 370,
                Height = 210,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Visible = false
            };

            Controls.AddRange(new Control[]
            {
                upButton, downButton, centerButton,
                irButton, previewToggleButton, drawerButton,
                themeButton, camModeBtn, obsModeBtn,
                vcamButton, angleLabel, angleSlider,
                drawerPanel, previewBox, noSignalLabel
            });

            upButton.Click += (s, e) => ChangeAngle(5);
            downButton.Click += (s, e) => ChangeAngle(-5);
            centerButton.Click += (s, e) => SetAngle(0);
            irButton.Click += (s, e) => ToggleIR();
            previewToggleButton.Click += (s, e) => TogglePreview();
            drawerButton.Click += (s, e) => ToggleDrawer();
            themeButton.Click += (s, e) => { darkMode = !darkMode; ApplyTheme(); SaveConfig(); };
            camModeBtn.Click += (s, e) => EnterCamMode();
            obsModeBtn.Click += (s, e) => ToggleObsMode();
            vcamButton.Click += (s, e) => ToggleVCam();
            angleSlider.Scroll += (s, e) => SetAngle(angleSlider.Value);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD – Sidebar OBS
        // ══════════════════════════════════════════════════════════════════════
        private void BuildObsSidebar()
        {
            obsSidebar = new Panel { Width = 52, BackColor = XboxBlack, Visible = false };

            var tips = new ToolTip { InitialDelay = 300, ShowAlways = true };

            void SBtn(string icon, string tip, int slot, Action act)
            {
                var b = new Button
                {
                    Text = icon,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(50, 50, 50),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10),
                    Tag = slot
                };
                b.FlatAppearance.BorderSize = 0;
                b.Click += (s, e) => act();
                tips.SetToolTip(b, tip);
                obsSidebar.Controls.Add(b);
            }

            SBtn("▲", "Subir ângulo", 0, () => ChangeAngle(5));
            SBtn("▼", "Descer ângulo", 1, () => ChangeAngle(-5));
            SBtn("○", "Centralizar", 2, () => SetAngle(0));
            SBtn("IR", "Alternar IR/RGB", 3, () => ToggleIR());
            SBtn("👁", "Preview ON/OFF", 4, () => TogglePreview());
            SBtn("🌙", "Tema", 5, () => { darkMode = !darkMode; ApplyTheme(); SaveConfig(); });
            SBtn("📡", "Câmera virtual ON/OFF", 6, () => ToggleVCam());

            var back = new Button
            {
                Text = "◀",
                FlatStyle = FlatStyle.Flat,
                BackColor = XboxRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Tag = "back"
            };
            back.FlatAppearance.BorderSize = 0;
            back.Click += (s, e) => ToggleObsMode();
            tips.SetToolTip(back, "Voltar ao modo normal");
            obsSidebar.Controls.Add(back);

            Controls.Add(obsSidebar);
        }

        private void LayoutObsSidebar()
        {
            int bh = 46, gap = 2, top = 4;
            foreach (Control c in obsSidebar.Controls)
            {
                if (c is Button b)
                {
                    if (b.Tag is int slot)
                        b.SetBounds(2, top + slot * (bh + gap), 48, bh);
                    else if (b.Tag is string s && s == "back")
                        b.SetBounds(2, obsSidebar.Height - bh - 4, 48, bh);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MODOS
        // ══════════════════════════════════════════════════════════════════════
        private void EnterCamMode()
        {
            appMode = 1;
            SetNormalButtonsVisible(false);
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(CamW, CamH);
            previewBox.SetBounds(0, 0, CamW, CamH);
            previewBox.BorderStyle = BorderStyle.None;
            previewBox.Visible = previewOn;
            noSignalLabel.SetBounds(0, 0, CamW, CamH);
            noSignalLabel.Visible = !previewOn;
            BackColor = Color.Black;
            SaveConfig();
        }

        private void ExitCamMode()
        {
            appMode = 0;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Width = 580; Height = 540;
            previewBox.SetBounds(90, 230, 370, 210);
            previewBox.BorderStyle = BorderStyle.FixedSingle;
            previewBox.Visible = previewOn;
            noSignalLabel.SetBounds(90, 230, 370, 210);
            noSignalLabel.Visible = !previewOn;
            SetNormalButtonsVisible(true);
            ApplyTheme();
            SaveConfig();
        }

        private void ToggleObsMode()
        {
            if (appMode == 2)
            {
                appMode = 0;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                Width = 580; Height = 540;
                previewBox.SetBounds(90, 230, 370, 210);
                previewBox.BorderStyle = BorderStyle.FixedSingle;
                previewBox.Visible = previewOn;
                noSignalLabel.SetBounds(90, 230, 370, 210);
                noSignalLabel.Visible = !previewOn;
                obsSidebar.Visible = false;
                SetNormalButtonsVisible(true);
                ApplyTheme();
            }
            else
            {
                appMode = 2;
                SetNormalButtonsVisible(false);
                FormBorderStyle = FormBorderStyle.None;
                ClientSize = new Size(CamW + 52, CamH);
                previewBox.SetBounds(0, 0, CamW, CamH);
                previewBox.BorderStyle = BorderStyle.None;
                previewBox.Visible = previewOn;
                noSignalLabel.SetBounds(0, 0, CamW, CamH);
                noSignalLabel.Visible = !previewOn;
                obsSidebar.SetBounds(CamW, 0, 52, CamH);
                obsSidebar.Visible = true;
                obsSidebar.BringToFront();
                LayoutObsSidebar();
                BackColor = Color.Black;
            }
            SaveConfig();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            if (appMode == 1) ExitCamMode();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if ((appMode == 1 || appMode == 2) && e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
            base.OnMouseDown(e);
        }

        private void SetNormalButtonsVisible(bool v)
        {
            upButton.Visible = downButton.Visible = centerButton.Visible = v;
            irButton.Visible = previewToggleButton.Visible = drawerButton.Visible = v;
            themeButton.Visible = camModeBtn.Visible = obsModeBtn.Visible = v;
            vcamButton.Visible = angleSlider.Visible = angleLabel.Visible = v;
            drawerPanel.Visible = v && drawerOpen;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GAVETA DE RESOLUÇÕES
        // ══════════════════════════════════════════════════════════════════════
        private void ToggleDrawer()
        {
            drawerOpen = !drawerOpen;
            int totalH = Resolutions.Length * 36;
            drawerPanel.Height = drawerOpen ? totalH : 0;
            drawerPanel.Visible = drawerOpen;
            drawerButton.Text = drawerOpen ? "📐▴" : "📐▾";

            int shift = drawerOpen ? totalH : 0;
            angleSlider.Top = 195 + shift;
            angleLabel.Top = 165 + shift;
            previewBox.Top = angleSlider.Top + 42;
            noSignalLabel.Top = previewBox.Top;
            Height = previewBox.Top + previewBox.Height + 30;

            drawerPanel.BringToFront();
            ApplyTheme();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  VIRTUAL CAM
        // ══════════════════════════════════════════════════════════════════════
        private void ToggleVCam()
        {
            vcamOn = !vcamOn;
            if (vcamOn)
            {
                bool ok = vcamWriter.Start(CamW, CamH);
                if (!ok)
                {
                    vcamOn = false;
                    MessageBox.Show(
                        "Não foi possível iniciar a câmera virtual.\n\n" +
                        "Abra o OBS uma vez, vá em\n" +
                        "Ferramentas → VirtualCam → Start,\n" +
                        "depois feche o OBS e tente novamente.",
                        "VirtualCam", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                vcamButton.Text = "📡 VCam ON";
            }
            else
            {
                vcamWriter.Stop();
                vcamButton.Text = "📡 VCam OFF";
            }
            ApplyTheme();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  KINECT
        // ══════════════════════════════════════════════════════════════════════
        private void InitializeKinect()
        {
            sensor = KinectSensor.KinectSensors.Count > 0 ? KinectSensor.KinectSensors[0] : null;
            if (sensor == null) { MessageBox.Show("Nenhum Kinect detectado."); return; }
            RestartKinect();
            SetAngle(angleSlider.Value);
        }

        private void RestartKinect()
        {
            if (sensor == null) return;
            try { sensor.Stop(); } catch { }
            sensor.ColorStream.Enable(Resolutions[selectedResIndex].Format);
            sensor.ColorFrameReady -= Sensor_ColorFrameReady;
            sensor.ColorFrameReady += Sensor_ColorFrameReady;
            sensor.Start();
        }

        // ── Frame handler com conversão IR correta ────────────────────────────
        private void Sensor_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            using (var frame = e.OpenColorImageFrame())
            {
                if (frame == null) return;

                byte[] rawPixels = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(rawPixels);

                byte[] bgraPixels;

                if (useIR)
                {
                    // IR = 16 bits por pixel (2 bytes little-endian, valor 0–65535).
                    // Convertemos para grayscale BGRA 32bpp, igual ao Infrared Basics WPF da Microsoft.
                    int pixelCount = frame.Width * frame.Height;
                    bgraPixels = new byte[pixelCount * 4];

                    for (int i = 0; i < pixelCount; i++)
                    {
                        // Lê o valor de 16 bits
                        ushort irValue = (ushort)(rawPixels[i * 2] | (rawPixels[i * 2 + 1] << 8));

                        // Mapeia para 0–255 pegando os 8 bits mais significativos
                        // (igual ao sample oficial: value >> 8)
                        byte intensity = (byte)(irValue >> 8);

                        // Escreve como BGRA cinza com alpha=255
                        bgraPixels[i * 4 + 0] = intensity; // B
                        bgraPixels[i * 4 + 1] = intensity; // G
                        bgraPixels[i * 4 + 2] = intensity; // R
                        bgraPixels[i * 4 + 3] = 255;        // A
                    }
                }
                else
                {
                    // RGB normal: o SDK já entrega 4 bytes por pixel (BGRA)
                    bgraPixels = rawPixels;
                    // Garante alpha = 255
                    for (int i = 3; i < bgraPixels.Length; i += 4)
                        bgraPixels[i] = 255;
                }

                if (vcamOn) vcamWriter.SendFrame(bgraPixels);
                if (!previewOn) return;

                if (previewBox.IsHandleCreated)
                {
                    previewBox.BeginInvoke((MethodInvoker)(() =>
                    {
                        var old = previewBox.Image;
                        var bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
                        var bd = bmp.LockBits(new Rectangle(0, 0, frame.Width, frame.Height),
                                               ImageLockMode.WriteOnly, bmp.PixelFormat);
                        Marshal.Copy(bgraPixels, 0, bd.Scan0, bgraPixels.Length);
                        bmp.UnlockBits(bd);
                        previewBox.Image = bmp;
                        old?.Dispose();
                    }));
                }
            }
        }

        private void ToggleIR()
        {
            useIR = !useIR;
            selectedResIndex = useIR ? 2 : 0;
            if (vcamOn) { vcamWriter.Stop(); vcamWriter.Start(CamW, CamH); }
            RestartKinect();
            ApplyTheme();
        }

        private void TogglePreview()
        {
            previewOn = !previewOn;
            previewBox.Visible = previewOn;
            noSignalLabel.Visible = !previewOn;
            previewToggleButton.Text = previewOn ? "👁" : "▶";
        }

        private void ChangeAngle(int delta)
        {
            if (sensor == null) return;
            SetAngle(Math.Max(-27, Math.Min(27, sensor.ElevationAngle + delta)));
        }

        private void SetAngle(int value)
        {
            if (sensor == null) return;
            try { sensor.ElevationAngle = value; } catch { }
            angleSlider.Value = value;
            angleLabel.Text = $"Ângulo: {value}°";
            SaveConfig();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TEMA – Xbox 360
        // ══════════════════════════════════════════════════════════════════════
        private void ApplyTheme()
        {
            if (appMode != 0) return;

            Color bg = darkMode ? XboxDark : XboxWhite;
            Color btnBg = darkMode ? XboxGreenD : XboxGreen;
            Color selBg = darkMode ? Color.FromArgb(110, 200, 90) : Color.FromArgb(50, 140, 35);
            Color fg = darkMode ? Color.White : XboxBlack;
            Color noSig = darkMode ? Color.FromArgb(55, 55, 55) : Color.FromArgb(210, 210, 210);

            BackColor = bg;

            void Style(Button b, bool sel = false, Color? over = null)
            {
                b.BackColor = over ?? (sel ? selBg : btnBg);
                b.ForeColor = Color.White;
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderSize = sel ? 2 : 0;
                b.FlatAppearance.BorderColor = sel ? Color.White : (over ?? btnBg);
            }

            Style(upButton); Style(downButton); Style(centerButton);
            Style(irButton, useIR);
            Style(previewToggleButton);
            Style(drawerButton, drawerOpen);
            Style(themeButton); Style(camModeBtn); Style(obsModeBtn);
            Style(vcamButton, vcamOn, vcamOn ? Color.FromArgb(0, 140, 255) : (Color?)null);

            drawerPanel.BackColor = darkMode ? Color.FromArgb(48, 48, 48) : Color.FromArgb(220, 220, 220);
            for (int i = 0; i < resButtons.Length; i++)
            {
                resButtons[i].BackColor = (i == selectedResIndex) ? selBg : btnBg;
                resButtons[i].ForeColor = Color.White;
                resButtons[i].FlatStyle = FlatStyle.Flat;
                resButtons[i].FlatAppearance.BorderSize = (i == selectedResIndex) ? 2 : 0;
                resButtons[i].FlatAppearance.BorderColor = (i == selectedResIndex) ? Color.White : btnBg;
            }

            angleLabel.BackColor = bg;
            angleLabel.ForeColor = fg;
            angleSlider.BackColor = bg;
            noSignalLabel.BackColor = noSig;
            noSignalLabel.ForeColor = darkMode ? Color.DimGray : Color.Gray;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONFIG
        // ══════════════════════════════════════════════════════════════════════
        private void LoadConfig()
        {
            if (!File.Exists(configPath)) return;
            foreach (var line in File.ReadAllLines(configPath))
            {
                if (line.StartsWith("darkMode=")) darkMode = line.Contains("true");
                if (line.StartsWith("previewOn=")) previewOn = !line.Contains("false");
                if (line.StartsWith("useIR=")) useIR = line.Contains("true");
                if (line.StartsWith("resIndex=") && int.TryParse(line.Split('=')[1], out int ri))
                    selectedResIndex = Math.Max(0, Math.Min(Resolutions.Length - 1, ri));
                if (line.StartsWith("angle=") && int.TryParse(line.Split('=')[1], out int av))
                    angleSlider.Value = Math.Max(-27, Math.Min(27, av));
            }
            if (useIR && selectedResIndex != 2) selectedResIndex = 2;
            if (!useIR && selectedResIndex == 2) selectedResIndex = 0;

            previewBox.Visible = previewOn;
            noSignalLabel.Visible = !previewOn;
            previewToggleButton.Text = previewOn ? "👁" : "▶";
        }

        private void SaveConfig()
        {
            File.WriteAllLines(configPath, new[]
            {
                $"darkMode={darkMode}",
                $"previewOn={previewOn}",
                $"useIR={useIR}",
                $"resIndex={selectedResIndex}",
                $"angle={angleSlider.Value}"
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════
        private Button MakeBtn(string icon, string tooltip, int x, int y)
        {
            var btn = new Button
            {
                Text = icon,
                Left = x,
                Top = y,
                Width = 110,
                Height = 32,
                FlatStyle = FlatStyle.Flat
            };
            new ToolTip().SetToolTip(btn, tooltip);
            return btn;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FECHAMENTO
        // ══════════════════════════════════════════════════════════════════════
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            vcamWriter.Stop();
            SaveConfig();
            try { sensor?.Stop(); } catch { }
            base.OnFormClosing(e);
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}