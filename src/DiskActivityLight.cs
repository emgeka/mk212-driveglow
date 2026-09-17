using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
    private const string AppName = "MK212 DriveGlow";
    private const string RunValue = "MK212DriveGlow";
    private const string LegacyRunValue = "MK212DiskActivityLight";
    private const string SettingsKey = @"Software\MK212DiskActivityLight";
    private const string StopEventName = "Local\\MK212DiskActivityLightStop";
    private const string MutexName = "Local\\MK212DiskActivityLightInstance";

    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { ReportFatalError(e.Exception, true); };
        AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
        {
            ReportFatalError(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject == null ? "Unknown error" : e.ExceptionObject.ToString()), false);
        };
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        if (args.Length > 0 && args[0].Equals("--install", StringComparison.OrdinalIgnoreCase))
        {
            Install();
            return;
        }
        if (args.Length > 0 && args[0].Equals("--uninstall", StringComparison.OrdinalIgnoreCase))
        {
            Uninstall();
            return;
        }
        if (args.Length > 0 && args[0].Equals("--shutdown", StringComparison.OrdinalIgnoreCase))
        {
            SignalStop();
            return;
        }
        if (args.Length > 0 && args[0].Equals("--test", StringComparison.OrdinalIgnoreCase))
        {
            TestLight();
            return;
        }
        if (args.Length > 0 && args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
        {
            SelfTest();
            return;
        }

        bool created;
        using (var mutex = new Mutex(true, MutexName, out created))
        {
            if (!created) return;
            MigrateStartupEntry();
            using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName))
            {
                stop.Reset();
                Application.Run(new TrayContext(stop));
            }
        }
    }

    private static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }

    private static string ErrorLogPath
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MK212 DriveGlow", "error.log"); }
    }

    private static void ReportFatalError(Exception exception, bool showMessage)
    {
        try
        {
            string directory = Path.GetDirectoryName(ErrorLogPath);
            Directory.CreateDirectory(directory);
            File.AppendAllText(ErrorLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine);
        }
        catch { }

        if (showMessage)
        {
            try
            {
                MessageBox.Show("DriveGlow musste wegen eines Fehlers beendet werden. Ein Fehlerbericht wurde hier gespeichert:\r\n\r\n" + ErrorLogPath,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
            Application.Exit();
        }
    }

    internal static bool StartupEnabled
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                return key != null && (key.GetValue(RunValue) != null || key.GetValue(LegacyRunValue) != null);
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                if (value)
                {
                    key.SetValue(RunValue, "\"" + ExePath + "\"");
                    key.DeleteValue(LegacyRunValue, false);
                }
                else
                {
                    key.DeleteValue(RunValue, false);
                    key.DeleteValue(LegacyRunValue, false);
                }
            }
        }
    }

    private static void MigrateStartupEntry()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
        {
            if (key.GetValue(RunValue) != null || key.GetValue(LegacyRunValue) != null)
            {
                key.SetValue(RunValue, "\"" + ExePath + "\"");
                key.DeleteValue(LegacyRunValue, false);
            }
        }
    }

    internal static Color ActivityColor
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object stored = key == null ? null : key.GetValue("ActivityColor");
                int argb;
                if (stored != null && int.TryParse(stored.ToString(), out argb)) return Color.FromArgb(argb);
            }
            return Color.FromArgb(255, 145, 0);
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue("ActivityColor", value.ToArgb().ToString(), RegistryValueKind.String);
        }
    }

    internal static int DisplayMode
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object stored = key == null ? null : key.GetValue("DisplayMode");
                int mode;
                if (stored != null && int.TryParse(stored.ToString(), out mode) && mode == 1) return 1;
            }
            return 0;
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue("DisplayMode", value == 1 ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    internal static int TrayDisplayMode
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object stored = key == null ? null : key.GetValue("TrayDisplayMode");
                int mode;
                if (stored != null && int.TryParse(stored.ToString(), out mode) && mode >= 0 && mode <= 2) return mode;
            }
            return 1;
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue("TrayDisplayMode", Math.Max(0, Math.Min(2, value)), RegistryValueKind.DWord);
        }
    }

    internal static bool TaskbarDisplayEnabled
    {
        get
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
            {
                object stored = key == null ? null : key.GetValue("TaskbarDisplayEnabled");
                return stored != null && stored.ToString() == "1";
            }
        }
        set
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                key.SetValue("TaskbarDisplayEnabled", value ? 1 : 0, RegistryValueKind.DWord);
        }
    }

    private static void ToKeyboardHsv(Color color, out byte hue, out byte saturation)
    {
        hue = (byte)Math.Round((color.GetHue() / 360.0) * 255.0);
        double max = Math.Max(color.R, Math.Max(color.G, color.B));
        double min = Math.Min(color.R, Math.Min(color.G, color.B));
        saturation = max <= 0 ? (byte)0 : (byte)Math.Round(((max - min) / max) * 255.0);
    }

    private static void Install()
    {
        StartupEnabled = true;
        Process.Start(ExePath);
        MessageBox.Show("Die MK212-Lichtleiste zeigt ab jetzt die Datenträgeraktivität an und startet künftig automatisch mit Windows.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void Uninstall()
    {
        SignalStop();
        StartupEnabled = false;
        MessageBox.Show("Der automatische Start wurde entfernt. Die vorherige Beleuchtungseinstellung wurde wiederhergestellt, sofern die Tastatur verbunden war.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static void SignalStop()
    {
        try
        {
            using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName)) stop.Set();
            Thread.Sleep(2200);
        }
        catch (UnauthorizedAccessException) { }

        Process current = Process.GetCurrentProcess();
        foreach (Process process in Process.GetProcessesByName("MK212-DriveGlow"))
        {
            try
            {
                if (process.Id != current.Id && process.SessionId == current.SessionId &&
                    string.Equals(process.MainModule.FileName, ExePath, StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill();
                    process.WaitForExit(1500);
                }
            }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
    }

    private static void TestLight()
    {
        try
        {
            using (var keyboard = Mk212.Open())
            {
                LightingState old = keyboard.ReadState();
                try
                {
                    byte hue, saturation;
                    ToKeyboardHsv(ActivityColor, out hue, out saturation);
                    keyboard.SetEffect(5);
                    keyboard.SetColor(hue, saturation);
                    for (int i = 0; i < 4; i++)
                    {
                        keyboard.SetBrightness(120);
                        Thread.Sleep(180);
                        keyboard.SetBrightness(0);
                        Thread.Sleep(180);
                    }
                }
                finally { keyboard.Restore(old); }
            }
            MessageBox.Show("Test erfolgreich. Die Seitenleiste wurde anschließend auf ihre vorherige Einstellung zurückgesetzt.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Die MK212 konnte nicht gesteuert werden:\r\n\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void SelfTest()
    {
        try
        {
            using (var keyboard = Mk212.Open())
            {
                LightingState old = keyboard.ReadState();
                Console.WriteLine("MK212 side light: effect={0}, brightness={1}, speed={2}, hue={3}, saturation={4}", old.Effect, old.Brightness, old.Speed, old.Hue, old.Saturation);
                byte hue, saturation;
                ToKeyboardHsv(ActivityColor, out hue, out saturation);
                keyboard.SetEffect(5);
                keyboard.SetColor(hue, saturation);
                keyboard.SetBrightness(80);
                Thread.Sleep(220);
                keyboard.Restore(old);
            }
            using (var disk = new DiskRate())
                Console.WriteLine("Disk counter: {0:0} bytes/s", disk.Sample());
            Console.WriteLine("Self-test successful.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            Environment.ExitCode = 1;
        }
    }

    private sealed class TrayContext : ApplicationContext
    {
        private readonly EventWaitHandle stop;
        private readonly NotifyIcon icon;
        private readonly ToolStripMenuItem status;
        private readonly Thread worker;
        private readonly SynchronizationContext ui;
        private readonly RegisteredWaitHandle stopWatcher;
        private Icon activeIcon;
        private Icon idleIcon;
        private Icon staticIcon;
        private Icon[] levelIcons;
        private readonly Icon waitingIcon;
        private int activityColorArgb;
        private int colorRevision;
        private int previewMode;
        private int displayMode;
        private int trayDisplayMode;
        private int lastTrayLevel = -1;
        private double latestActivityLevel;
        private bool latestActive;
        private TaskbarStatusForm taskbarForm;
        private ToolStripMenuItem taskbarDisplay;

        public TrayContext(EventWaitHandle stopEvent)
        {
            stop = stopEvent;
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            Color savedColor = ActivityColor;
            displayMode = DisplayMode;
            trayDisplayMode = TrayDisplayMode;
            activityColorArgb = savedColor.ToArgb();
            BuildTrayIcons(savedColor);
            waitingIcon = TrayIcon.Create(Color.FromArgb(95, 95, 95));
            status = new ToolStripMenuItem("Tastatur wird gesucht …") { Enabled = false };
            var menu = new ContextMenuStrip();
            menu.Items.Add(status);
            menu.Items.Add(new ToolStripSeparator());
            var chooseColor = new ToolStripMenuItem("Anzeigefarbe …");
            chooseColor.Click += delegate { ChooseColor(); };
            menu.Items.Add(chooseColor);
            var displayType = new ToolStripMenuItem("Anzeigeart");
            var classicMode = new ToolStripMenuItem("Klassisches Blinken") { Checked = displayMode == 0 };
            var levelMode = new ToolStripMenuItem("Aktivit\u00e4tspegel") { Checked = displayMode == 1 };
            classicMode.Click += delegate
            {
                DisplayMode = 0;
                Interlocked.Exchange(ref displayMode, 0);
                classicMode.Checked = true;
                levelMode.Checked = false;
            };
            levelMode.Click += delegate
            {
                DisplayMode = 1;
                Interlocked.Exchange(ref displayMode, 1);
                classicMode.Checked = false;
                levelMode.Checked = true;
            };
            displayType.DropDownItems.Add(classicMode);
            displayType.DropDownItems.Add(levelMode);
            menu.Items.Add(displayType);
            var trayDisplay = new ToolStripMenuItem("Tray-Anzeige");
            var trayPoint = new ToolStripMenuItem("Aktivit\u00e4tspunkt") { Checked = trayDisplayMode == 0 };
            var trayLevel = new ToolStripMenuItem("Aktivit\u00e4tspegel") { Checked = trayDisplayMode == 1 };
            var trayStatic = new ToolStripMenuItem("Nur App-Symbol") { Checked = trayDisplayMode == 2 };
            ToolStripMenuItem[] trayChoices = { trayPoint, trayLevel, trayStatic };
            for (int i = 0; i < trayChoices.Length; i++)
            {
                int selectedMode = i;
                trayChoices[i].Click += delegate
                {
                    TrayDisplayMode = selectedMode;
                    Interlocked.Exchange(ref trayDisplayMode, selectedMode);
                    for (int choice = 0; choice < trayChoices.Length; choice++) trayChoices[choice].Checked = choice == selectedMode;
                    lastTrayLevel = -1;
                    UpdateActivityDisplays(latestActivityLevel, latestActive);
                };
                trayDisplay.DropDownItems.Add(trayChoices[i]);
            }
            menu.Items.Add(trayDisplay);
            taskbarDisplay = new ToolStripMenuItem("Aktivit\u00e4t in der Taskleiste") { CheckOnClick = true, Checked = TaskbarDisplayEnabled };
            taskbarDisplay.CheckedChanged += delegate
            {
                TaskbarDisplayEnabled = taskbarDisplay.Checked;
                UpdateTaskbarWindow();
            };
            menu.Items.Add(taskbarDisplay);
            var startup = new ToolStripMenuItem("Beim Anmelden starten") { CheckOnClick = true, Checked = StartupEnabled };
            startup.CheckedChanged += delegate
            {
                try { StartupEnabled = startup.Checked; }
                catch (Exception ex) { MessageBox.Show("Die Autostart-Einstellung konnte nicht geändert werden:\r\n\r\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            menu.Items.Add(startup);
            var exit = new ToolStripMenuItem("Beenden");
            exit.Click += delegate { stop.Set(); };
            menu.Items.Add(exit);
            icon = new NotifyIcon { Icon = waitingIcon, Text = AppName + " – Tastatur wird gesucht", ContextMenuStrip = menu, Visible = true };
            icon.BalloonTipTitle = AppName;
            icon.BalloonTipText = "Das Datenträgerlicht läuft. Falls das Laufwerkssymbol nicht dauerhaft sichtbar ist, findest du es unter den ausgeblendeten Symbolen der Taskleiste.";
            icon.BalloonTipIcon = ToolTipIcon.Info;
            icon.DoubleClick += delegate
            {
                MessageBox.Show(status.Text + "\r\n\r\nRechtsklick auf das Laufwerkssymbol öffnet das Menü.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            icon.ShowBalloonTip(7000);
            UpdateTaskbarWindow();

            worker = new Thread(WorkerLoop) { IsBackground = true, Name = AppName };
            worker.Start();
            stopWatcher = ThreadPool.RegisterWaitForSingleObject(stop, delegate
            {
                ui.Post(delegate
                {
                    icon.Visible = false;
                    if (worker.IsAlive) worker.Join(1500);
                    ExitThread();
                }, null);
            }, null, Timeout.Infinite, true);
        }

        private void SetStatus(string text, Icon stateIcon)
        {
            ui.Post(delegate
            {
                status.Text = text;
                icon.Text = (AppName + " – " + text).Substring(0, Math.Min(63, (AppName + " – " + text).Length));
                icon.Icon = stateIcon;
                lastTrayLevel = -1;
            }, null);
        }

        private void SetActivity(bool active)
        {
            SetActivityLevel(active ? 1.0 : 0.0, active);
        }

        private void SetActivityLevel(double level, bool active)
        {
            ui.Post(delegate { UpdateActivityDisplays(level, active); }, null);
        }

        private void UpdateActivityDisplays(double level, bool active)
        {
            latestActivityLevel = Math.Max(0, Math.Min(1, level));
            latestActive = active;
            int mode = Thread.VolatileRead(ref trayDisplayMode);
            int trayLevel = mode == 0 ? (active ? 5 : 0) : mode == 1 ? (int)Math.Ceiling(latestActivityLevel * 5.0) : 0;
            if (trayLevel != lastTrayLevel)
            {
                lastTrayLevel = trayLevel;
                icon.Icon = mode == 0 ? (active ? activeIcon : idleIcon) : mode == 1 ? levelIcons[trayLevel] : staticIcon;
            }
            if (taskbarForm != null) taskbarForm.SetActivity(latestActivityLevel);
        }

        private void UpdateTaskbarWindow()
        {
            if (taskbarDisplay.Checked)
            {
                if (taskbarForm == null || taskbarForm.IsDisposed)
                {
                    taskbarForm = new TaskbarStatusForm(Color.FromArgb(activityColorArgb));
                    taskbarForm.FormClosed += delegate
                    {
                        taskbarForm = null;
                        if (taskbarDisplay.Checked) taskbarDisplay.Checked = false;
                    };
                    taskbarForm.Show();
                    taskbarForm.SetActivity(latestActivityLevel);
                }
            }
            else if (taskbarForm != null)
            {
                TaskbarStatusForm closing = taskbarForm;
                taskbarForm = null;
                closing.Close();
            }
        }

        private void ChooseColor()
        {
            Color original = Color.FromArgb(activityColorArgb);
            Interlocked.Exchange(ref previewMode, 1);
            Interlocked.Increment(ref colorRevision);
            using (var dialog = new NativeLiveColorDialog(original, PreviewColor))
            {
                if (dialog.ShowDialog() == DialogResult.OK) ActivityColor = dialog.SelectedColor;
                else PreviewColor(original);
            }
            Interlocked.Exchange(ref previewMode, 0);
            Interlocked.Increment(ref colorRevision);
        }

        private void PreviewColor(Color selected)
        {
            activityColorArgb = selected.ToArgb();
            Interlocked.Increment(ref colorRevision);

            BuildTrayIcons(selected);
            lastTrayLevel = -1;
            UpdateActivityDisplays(latestActivityLevel, latestActive);
            if (taskbarForm != null) taskbarForm.ActivityColor = selected;
        }

        private void BuildTrayIcons(Color color)
        {
            Icon newActive = TrayIcon.Create(color);
            Icon newIdle = TrayIcon.Create(Dim(color));
            Icon newStatic = TrayIcon.CreateStatic();
            Icon[] newLevels = new Icon[6];
            for (int i = 0; i < newLevels.Length; i++) newLevels[i] = TrayIcon.CreateLevel(color, i);

            Icon oldActive = activeIcon;
            Icon oldIdle = idleIcon;
            Icon oldStatic = staticIcon;
            Icon[] oldLevels = levelIcons;
            activeIcon = newActive;
            idleIcon = newIdle;
            staticIcon = newStatic;
            levelIcons = newLevels;
            if (oldActive != null) oldActive.Dispose();
            if (oldIdle != null) oldIdle.Dispose();
            if (oldStatic != null) oldStatic.Dispose();
            if (oldLevels != null) foreach (Icon old in oldLevels) old.Dispose();
        }

        private static Color Dim(Color color)
        {
            return Color.FromArgb(Math.Max(28, color.R / 3), Math.Max(28, color.G / 3), Math.Max(28, color.B / 3));
        }

        private void GetKeyboardColor(out byte hue, out byte saturation)
        {
            Color color = Color.FromArgb(Thread.VolatileRead(ref activityColorArgb));
            ToKeyboardHsv(color, out hue, out saturation);
        }

        private void WorkerLoop()
        {
            while (!stop.WaitOne(0))
            {
                try
                {
                    using (var keyboard = Mk212.Open())
                    using (var disk = new DiskRate())
                    {
                        LightingState old = keyboard.ReadState();
                        SetStatus("Aktiv – MK212 verbunden", idleIcon);
                        try
                        {
                            int appliedColorRevision = -1;
                            DateTime lastColorWrite = DateTime.MinValue;
                            keyboard.SetEffect(5);       // steady side light
                            keyboard.SetBrightness(0);
                            bool trayActive = false;
                            int appliedBrightness = 0;
                            double activityLevel = 0;
                            DateTime lastActivity = DateTime.MinValue;
                            while (!stop.WaitOne(70))
                            {
                                int wantedColorRevision = Thread.VolatileRead(ref colorRevision);
                                if (wantedColorRevision != appliedColorRevision ||
                                    (DateTime.UtcNow - lastColorWrite).TotalMilliseconds >= 65)
                                {
                                    byte hue, saturation;
                                    GetKeyboardColor(out hue, out saturation);
                                    keyboard.SetColor(hue, saturation);
                                    appliedColorRevision = wantedColorRevision;
                                    lastColorWrite = DateTime.UtcNow;
                                }
                                bool previewing = Thread.VolatileRead(ref previewMode) != 0;
                                double bytesPerSecond = disk.Sample();
                                double instantaneous = bytesPerSecond < 4096 ? 0 :
                                    Math.Log10(1.0 + bytesPerSecond / 4096.0) / Math.Log10(1.0 + 250000000.0 / 4096.0);
                                instantaneous = Math.Max(0, Math.Min(1, instantaneous));
                                activityLevel = Math.Max(instantaneous, activityLevel * 0.78);
                                int mode = Thread.VolatileRead(ref displayMode);
                                int wantedBrightness;
                                if (previewing)
                                {
                                    wantedBrightness = 120;
                                }
                                else if (mode == 1)
                                {
                                    wantedBrightness = activityLevel < 0.025 ? 0 : (int)Math.Round(12 + activityLevel * 148);
                                }
                                else
                                {
                                    if (bytesPerSecond >= 4096) lastActivity = DateTime.UtcNow;
                                    wantedBrightness = (DateTime.UtcNow - lastActivity).TotalMilliseconds < 130 ? 120 : 0;
                                }
                                if (Math.Abs(wantedBrightness - appliedBrightness) >= 3 || (wantedBrightness == 0) != (appliedBrightness == 0))
                                {
                                    keyboard.SetBrightness(wantedBrightness);
                                    appliedBrightness = wantedBrightness;
                                }
                                bool isActive = wantedBrightness > 0;
                                if (isActive != trayActive)
                                {
                                    trayActive = isActive;
                                }
                                SetActivityLevel(previewing ? 0.75 : activityLevel, trayActive || previewing);
                            }
                        }
                        finally
                        {
                            try { keyboard.Restore(old); } catch { }
                        }
                    }
                }
                catch (Exception)
                {
                    SetStatus("Warte auf MK212 …", waitingIcon);
                    if (stop.WaitOne(2000)) break;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                icon.Dispose();
                stopWatcher.Unregister(null);
                activeIcon.Dispose();
                idleIcon.Dispose();
                staticIcon.Dispose();
                foreach (Icon levelIcon in levelIcons) levelIcon.Dispose();
                waitingIcon.Dispose();
                if (taskbarForm != null) taskbarForm.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

internal sealed class NativeLiveColorDialog : ColorDialog
{
    private const int WmInitDialog = 0x0110;
    private const int WmDestroy = 0x0002;
    private const int ColorRed = 706;
    private const int ColorGreen = 707;
    private const int ColorBlue = 708;

    private readonly Action<Color> preview;
    private readonly System.Windows.Forms.Timer pollTimer;
    private IntPtr dialogHandle;
    private int lastArgb;

    public Color SelectedColor { get { return Color; } }

    public NativeLiveColorDialog(Color initial, Action<Color> previewAction)
    {
        Color = initial;
        FullOpen = true;
        AnyColor = true;
        SolidColorOnly = false;
        preview = previewAction;
        lastArgb = initial.ToArgb();
        pollTimer = new System.Windows.Forms.Timer { Interval = 40 };
        pollTimer.Tick += delegate { PollColor(); };
        pollTimer.Start();
    }

    protected override IntPtr HookProc(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam)
    {
        IntPtr result = base.HookProc(hWnd, msg, wparam, lparam);
        if (msg == WmInitDialog)
        {
            dialogHandle = hWnd;
            PollColor();
        }
        else if (msg == WmDestroy)
        {
            dialogHandle = IntPtr.Zero;
        }
        return result;
    }

    private void PollColor()
    {
        if (dialogHandle == IntPtr.Zero) return;
        bool redOk, greenOk, blueOk;
        uint r = GetDlgItemInt(dialogHandle, ColorRed, out redOk, false);
        uint g = GetDlgItemInt(dialogHandle, ColorGreen, out greenOk, false);
        uint b = GetDlgItemInt(dialogHandle, ColorBlue, out blueOk, false);
        if (!redOk || !greenOk || !blueOk || r > 255 || g > 255 || b > 255) return;
        Color selected = Color.FromArgb((int)r, (int)g, (int)b);
        if (selected.ToArgb() == lastArgb) return;
        lastArgb = selected.ToArgb();
        if (preview != null) preview(selected);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetDlgItemInt(IntPtr dialog, int controlId, out bool translated, bool signed);

    protected override void Dispose(bool disposing)
    {
        if (disposing) pollTimer.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class LiveColorDialog : Form
{
    private readonly ColorSpectrum spectrum;
    private readonly HueStrip hueStrip;
    private readonly Panel swatch;
    private readonly TextBox hex;
    private readonly NumericUpDown red;
    private readonly NumericUpDown green;
    private readonly NumericUpDown blue;
    private readonly NumericUpDown hsvHue;
    private readonly NumericUpDown hsvSaturation;
    private readonly NumericUpDown hsvValue;
    private readonly Action<Color> preview;
    private bool synchronizing;

    public Color SelectedColor { get; private set; }

    public LiveColorDialog(Color initial, Action<Color> previewAction)
    {
        preview = previewAction;
        SelectedColor = initial;

        Text = "Anzeigefarbe";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 462);
        Font = SystemFonts.MessageBoxFont;

        var hint = new Label
        {
            AutoSize = true,
            Location = new Point(18, 16),
            Text = "\u00c4nderungen werden sofort auf der Lichtleiste angezeigt."
        };

        var basicLabel = new Label { AutoSize = true, Location = new Point(18, 46), Text = "Grundfarben" };
        Color[] colors = new Color[]
        {
            Color.FromArgb(255, 145, 0), Color.Red, Color.DeepPink, Color.Magenta,
            Color.MediumPurple, Color.Blue, Color.DeepSkyBlue, Color.Cyan,
            Color.SpringGreen, Color.LimeGreen, Color.Yellow, Color.Gold,
            Color.White, Color.Silver, Color.Gray, Color.FromArgb(45, 45, 45)
        };
        var palette = new Panel { Location = new Point(18, 65), Size = new Size(584, 62) };
        for (int i = 0; i < colors.Length; i++)
        {
            Color color = colors[i];
            var button = new Button
            {
                BackColor = color,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((i % 8) * 72, (i / 8) * 31),
                Size = new Size(64, 25),
                TabStop = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 90);
            button.Click += delegate { ApplyColor(color, true); };
            palette.Controls.Add(button);
        }

        var spectrumLabel = new Label { AutoSize = true, Location = new Point(18, 138), Text = "Farbspektrum" };
        spectrum = new ColorSpectrum(initial) { Location = new Point(21, 158), Size = new Size(300, 205) };
        hueStrip = new HueStrip(initial.GetHue()) { Location = new Point(331, 158), Size = new Size(28, 205) };
        spectrum.SelectionChanged += delegate { if (!synchronizing) ApplyColor(spectrum.SelectedColor, false); };
        hueStrip.SelectionChanged += delegate
        {
            if (synchronizing) return;
            spectrum.Hue = hueStrip.Hue;
            ApplyColor(spectrum.SelectedColor, false);
        };

        var selectedLabel = new Label { AutoSize = true, Location = new Point(382, 144), Text = "Auswahl" };
        swatch = new Panel { Location = new Point(446, 139), Size = new Size(156, 28), BorderStyle = BorderStyle.FixedSingle, BackColor = initial };

        var hexLabel = new Label { AutoSize = true, Location = new Point(382, 184), Text = "HEX" };
        hex = new TextBox { Location = new Point(446, 180), Size = new Size(156, 23), CharacterCasing = CharacterCasing.Upper };

        var rgbLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Location = new Point(382, 218), Text = "RGB" };
        red = AddNumber("Rot", 382, 244, 255);
        green = AddNumber("Gr\u00fcn", 382, 272, 255);
        blue = AddNumber("Blau", 382, 300, 255);

        var hsvLabel = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Location = new Point(500, 218), Text = "HSV" };
        hsvHue = AddNumber("H", 500, 244, 359);
        hsvSaturation = AddNumber("S", 500, 272, 100);
        hsvValue = AddNumber("V", 500, 300, 100);

        red.ValueChanged += RgbChanged;
        green.ValueChanged += RgbChanged;
        blue.ValueChanged += RgbChanged;
        hsvHue.ValueChanged += HsvChanged;
        hsvSaturation.ValueChanged += HsvChanged;
        hsvValue.ValueChanged += HsvChanged;
        hex.TextChanged += HexChanged;
        hex.Leave += delegate { if (!synchronizing) hex.Text = ToHex(SelectedColor); };

        var explanation = new Label
        {
            AutoSize = false,
            Location = new Point(382, 337),
            Size = new Size(220, 45),
            Text = "HEX: #RRGGBB\r\nRGB: 0-255   HSV: H 0-359, S/V 0-100"
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(437, 413), Size = new Size(78, 30) };
        var cancel = new Button { Text = "Abbrechen", DialogResult = DialogResult.Cancel, Location = new Point(525, 413), Size = new Size(78, 30) };

        Controls.AddRange(new Control[] { hint, basicLabel, palette, spectrumLabel, spectrum, hueStrip, selectedLabel, swatch, hexLabel, hex, rgbLabel, hsvLabel, explanation, ok, cancel });
        AcceptButton = ok;
        CancelButton = cancel;
        ApplyColor(initial, true);
    }

    private NumericUpDown AddNumber(string label, int x, int y, int maximum)
    {
        var caption = new Label { AutoSize = true, Location = new Point(x, y + 4), Text = label };
        var number = new NumericUpDown { Location = new Point(x + 42, y), Size = new Size(66, 23), Minimum = 0, Maximum = maximum };
        Controls.Add(caption);
        Controls.Add(number);
        return number;
    }

    private void ApplyColor(Color color, bool updatePickers)
    {
        SelectedColor = color;
        swatch.BackColor = SelectedColor;
        synchronizing = true;
        double h, s, v;
        ColorMath.ToHsv(color, out h, out s, out v);
        if (updatePickers)
        {
            spectrum.SetColor(color);
            hueStrip.Hue = h;
        }
        red.Value = color.R;
        green.Value = color.G;
        blue.Value = color.B;
        hsvHue.Value = Math.Min(359, (decimal)Math.Round(h));
        hsvSaturation.Value = Math.Min(100, (decimal)Math.Round(s * 100.0));
        hsvValue.Value = Math.Min(100, (decimal)Math.Round(v * 100.0));
        hex.Text = ToHex(color);
        synchronizing = false;
        if (preview != null) preview(SelectedColor);
    }

    private void RgbChanged(object sender, EventArgs e)
    {
        if (!synchronizing) ApplyColor(Color.FromArgb((int)red.Value, (int)green.Value, (int)blue.Value), true);
    }

    private void HsvChanged(object sender, EventArgs e)
    {
        if (!synchronizing) ApplyColor(ColorMath.FromHsv((double)hsvHue.Value, (double)hsvSaturation.Value / 100.0, (double)hsvValue.Value / 100.0), true);
    }

    private void HexChanged(object sender, EventArgs e)
    {
        if (synchronizing) return;
        string value = hex.Text.Trim().TrimStart('#');
        int rgb;
        if (value.Length == 6 && int.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out rgb))
            ApplyColor(Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255), true);
    }

    private static string ToHex(Color color) { return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2"); }
}

internal sealed class ColorSpectrum : Control
{
    private Bitmap background;
    private double selectedHue;
    private double selectedSaturation;
    private double selectedValue;

    public event EventHandler SelectionChanged;
    public Color SelectedColor { get { return ColorMath.FromHsv(selectedHue, selectedSaturation, selectedValue); } }
    public double Hue
    {
        get { return selectedHue; }
        set { selectedHue = Math.Max(0, Math.Min(359, value)); Rebuild(); }
    }

    public ColorSpectrum(Color initial)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        SetColor(initial);
        Cursor = Cursors.Cross;
        TabStop = true;
    }

    public void SetColor(Color color)
    {
        ColorMath.ToHsv(color, out selectedHue, out selectedSaturation, out selectedValue);
        Rebuild();
    }

    private void Rebuild()
    {
        if (background != null) { background.Dispose(); background = null; }
        Invalidate();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        if (background != null) { background.Dispose(); background = null; }
        base.OnSizeChanged(e);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        EnsureBackground();
        if (background != null) e.Graphics.DrawImageUnscaled(background, 0, 0);
        int x = (int)Math.Round(selectedSaturation * Math.Max(0, Width - 1));
        int y = (int)Math.Round((1.0 - selectedValue) * Math.Max(0, Height - 1));
        using (var dark = new Pen(Color.Black, 3))
        using (var light = new Pen(Color.White, 1))
        {
            e.Graphics.DrawEllipse(dark, x - 6, y - 6, 12, 12);
            e.Graphics.DrawEllipse(light, x - 6, y - 6, 12, 12);
        }
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(90, 90, 90), ButtonBorderStyle.Solid);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Capture = true;
        Pick(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Capture && e.Button == MouseButtons.Left) Pick(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        Capture = false;
        base.OnMouseUp(e);
    }

    private void Pick(int x, int y)
    {
        selectedSaturation = Math.Max(0, Math.Min(Width - 1, x)) / (double)Math.Max(1, Width - 1);
        selectedValue = 1.0 - Math.Max(0, Math.Min(Height - 1, y)) / (double)Math.Max(1, Height - 1);
        Invalidate();
        EventHandler handler = SelectionChanged;
        if (handler != null) handler(this, EventArgs.Empty);
    }

    private void EnsureBackground()
    {
        if (Width <= 0 || Height <= 0 || background != null) return;
        background = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
        for (int x = 0; x < Width; x++)
        {
            double saturation = x / (double)Math.Max(1, Width - 1);
            for (int y = 0; y < Height; y++)
            {
                double value = 1.0 - y / (double)Math.Max(1, Height - 1);
                background.SetPixel(x, y, ColorMath.FromHsv(selectedHue, saturation, value));
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && background != null) background.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class HueStrip : Control
{
    private Bitmap background;
    private double hue;
    public event EventHandler SelectionChanged;
    public double Hue
    {
        get { return hue; }
        set { hue = Math.Max(0, Math.Min(359, value)); Invalidate(); }
    }

    public HueStrip(double initialHue)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        hue = initialHue;
        Cursor = Cursors.Hand;
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        if (background != null) { background.Dispose(); background = null; }
        base.OnSizeChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (background == null && Width > 0 && Height > 0)
        {
            background = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
            for (int y = 0; y < Height; y++)
            {
                Color color = ColorMath.FromHsv(y * 359.0 / Math.Max(1, Height - 1), 1.0, 1.0);
                for (int x = 0; x < Width; x++) background.SetPixel(x, y, color);
            }
        }
        if (background != null) e.Graphics.DrawImageUnscaled(background, 0, 0);
        int marker = (int)Math.Round(hue / 359.0 * Math.Max(0, Height - 1));
        using (var dark = new Pen(Color.Black, 3))
        using (var light = new Pen(Color.White, 1))
        {
            e.Graphics.DrawLine(dark, 0, marker, Width - 1, marker);
            e.Graphics.DrawLine(light, 0, marker, Width - 1, marker);
        }
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.FromArgb(90, 90, 90), ButtonBorderStyle.Solid);
    }

    protected override void OnMouseDown(MouseEventArgs e) { Capture = true; Pick(e.Y); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (Capture && e.Button == MouseButtons.Left) Pick(e.Y); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; base.OnMouseUp(e); }

    private void Pick(int y)
    {
        Hue = Math.Max(0, Math.Min(Height - 1, y)) * 359.0 / Math.Max(1, Height - 1);
        EventHandler handler = SelectionChanged;
        if (handler != null) handler(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && background != null) background.Dispose();
        base.Dispose(disposing);
    }
}

internal static class ColorMath
{
    public static Color FromHsv(double hueDegrees, double saturation, double value)
    {
        double chroma = value * saturation;
        double part = hueDegrees / 60.0;
        double x = chroma * (1.0 - Math.Abs(part % 2.0 - 1.0));
        double r = 0, g = 0, b = 0;
        if (part < 1) { r = chroma; g = x; }
        else if (part < 2) { r = x; g = chroma; }
        else if (part < 3) { g = chroma; b = x; }
        else if (part < 4) { g = x; b = chroma; }
        else if (part < 5) { r = x; b = chroma; }
        else { r = chroma; b = x; }
        double m = value - chroma;
        return Color.FromArgb(Clamp((r + m) * 255.0), Clamp((g + m) * 255.0), Clamp((b + m) * 255.0));
    }

    public static void ToHsv(Color color, out double hue, out double saturation, out double value)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        hue = color.GetHue();
        saturation = max <= 0 ? 0 : delta / max;
        value = max;
    }

    private static int Clamp(double value) { return Math.Max(0, Math.Min(255, (int)Math.Round(value))); }
}

internal static class TrayIcon
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(Color led)
    {
        using (var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var body = new SolidBrush(Color.FromArgb(47, 52, 58)))
            using (var edge = new Pen(Color.FromArgb(220, 225, 230), 2.2f))
            using (var platter = new Pen(Color.FromArgb(170, 180, 190), 1.8f))
            using (var light = new SolidBrush(led))
            {
                g.FillRectangle(body, 3, 6, 26, 20);
                g.DrawRectangle(edge, 4, 7, 24, 18);
                g.DrawEllipse(platter, 9, 10, 12, 12);
                g.DrawLine(platter, 17, 17, 23, 12);
                g.FillEllipse(light, 23, 20, 4, 4);
            }
            IntPtr handle = bitmap.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
    }

    public static Icon CreateStatic()
    {
        return Create(Color.FromArgb(105, 170, 225));
    }

    public static Icon CreateLevel(Color led, int level)
    {
        level = Math.Max(0, Math.Min(5, level));
        using (var bitmap = new Bitmap(32, 32, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (var body = new SolidBrush(Color.FromArgb(47, 52, 58)))
            using (var edge = new Pen(Color.FromArgb(220, 225, 230), 2.2f))
            using (var platter = new Pen(Color.FromArgb(150, 165, 180), 1.6f))
            using (var on = new SolidBrush(led))
            using (var off = new SolidBrush(Color.FromArgb(75, 82, 90)))
            {
                g.FillRectangle(body, 3, 5, 26, 22);
                g.DrawRectangle(edge, 4, 6, 24, 20);
                g.DrawEllipse(platter, 10, 8, 11, 11);
                g.DrawLine(platter, 17, 15, 23, 10);
                for (int i = 0; i < 5; i++)
                    g.FillRectangle(i < level ? on : off, 6 + i * 4, 21, 3, 3);
            }
            IntPtr handle = bitmap.GetHicon();
            try { return (Icon)Icon.FromHandle(handle).Clone(); }
            finally { DestroyIcon(handle); }
        }
    }
}

internal sealed class TaskbarStatusForm : Form
{
    private readonly Label activityText;
    private readonly ProgressBar activityBar;
    private ITaskbarList3 taskbar;
    private double pendingActivity;

    public Color ActivityColor
    {
        set
        {
            Icon old = Icon;
            Icon = TrayIcon.Create(value);
            if (old != null) old.Dispose();
        }
    }

    public TaskbarStatusForm(Color color)
    {
        Text = "MK212 DriveGlow – Laufwerksaktivität";
        ClientSize = new Size(340, 92);
        MinimumSize = new Size(300, 130);
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = TrayIcon.Create(color);

        activityText = new Label
        {
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(308, 24),
            Text = "Datenträgeraktivität: 0 %"
        };
        activityBar = new ProgressBar
        {
            Location = new Point(16, 44),
            Size = new Size(308, 22),
            Minimum = 0,
            Maximum = 100
        };
        Controls.Add(activityText);
        Controls.Add(activityBar);

        Shown += delegate
        {
            try
            {
                taskbar = (ITaskbarList3)new TaskbarList();
                taskbar.HrInit();
                ApplyActivity();
            }
            catch { taskbar = null; }
            BeginInvoke((MethodInvoker)delegate { WindowState = FormWindowState.Minimized; });
        };
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    public void SetActivity(double level)
    {
        pendingActivity = Math.Max(0, Math.Min(1, level));
        if (IsHandleCreated) ApplyActivity();
    }

    private void ApplyActivity()
    {
        int percent = (int)Math.Round(pendingActivity * 100.0);
        activityBar.Value = percent;
        activityText.Text = "Datenträgeraktivität: " + percent + " %";
        if (taskbar != null)
        {
            try
            {
                taskbar.SetProgressState(Handle, TaskbarProgressState.Normal);
                taskbar.SetProgressValue(Handle, (ulong)percent, 100);
            }
            catch { }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (taskbar != null && Marshal.IsComObject(taskbar)) Marshal.FinalReleaseComObject(taskbar);
            taskbar = null;
            if (Icon != null) Icon.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal enum TaskbarProgressState
{
    NoProgress = 0,
    Indeterminate = 1,
    Normal = 2,
    Error = 4,
    Paused = 8
}

[ComImport]
[Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
internal class TaskbarList { }

[ComImport]
[Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEA84")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITaskbarList3
{
    void HrInit();
    void AddTab(IntPtr hwnd);
    void DeleteTab(IntPtr hwnd);
    void ActivateTab(IntPtr hwnd);
    void SetActiveAlt(IntPtr hwnd);
    void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
    void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
    void SetProgressState(IntPtr hwnd, TaskbarProgressState state);
}

internal sealed class DiskRate : IDisposable
{
    private IntPtr query;
    private IntPtr counter;
    private const uint PDH_FMT_DOUBLE = 0x00000200;

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhOpenQuery(string source, IntPtr userData, out IntPtr query);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)] private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);
    [DllImport("pdh.dll")] private static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll")] private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, IntPtr type, out PDH_FMT_COUNTERVALUE value);
    [DllImport("pdh.dll")] private static extern uint PdhCloseQuery(IntPtr query);

    public DiskRate()
    {
        Check(PdhOpenQuery(null, IntPtr.Zero, out query), "Datenträgerüberwachung konnte nicht gestartet werden");
        try
        {
            Check(PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\Disk Bytes/sec", IntPtr.Zero, out counter), "Datenträgerzähler ist nicht verfügbar");
            Check(PdhCollectQueryData(query), "Datenträgerzähler konnte nicht gelesen werden");
            Thread.Sleep(80);
        }
        catch { Dispose(); throw; }
    }

    public double Sample()
    {
        Check(PdhCollectQueryData(query), "Datenträgerzähler konnte nicht gelesen werden");
        PDH_FMT_COUNTERVALUE value;
        Check(PdhGetFormattedCounterValue(counter, PDH_FMT_DOUBLE, IntPtr.Zero, out value), "Datenträgerrate konnte nicht ermittelt werden");
        return value.DoubleValue;
    }

    private static void Check(uint result, string message) { if (result != 0) throw new InvalidOperationException(message + " (0x" + result.ToString("X8") + ")"); }
    public void Dispose() { if (query != IntPtr.Zero) { PdhCloseQuery(query); query = IntPtr.Zero; } }
}

internal struct LightingState
{
    public byte Brightness;
    public byte Effect;
    public byte Speed;
    public byte Hue;
    public byte Saturation;
}

internal sealed class Mk212 : IDisposable
{
    private readonly FileStream stream;
    private readonly int inputLength;
    private readonly int outputLength;

    private Mk212(SafeFileHandle handle, int input, int output)
    {
        inputLength = input;
        outputLength = output;
        stream = new FileStream(handle, FileAccess.ReadWrite, 4096, false);
    }

    public static Mk212 Open()
    {
        Guid hidGuid;
        Native.HidD_GetHidGuid(out hidGuid);
        IntPtr set = Native.SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) throw new Win32Exception();
        try
        {
            for (int i = 0; ; i++)
            {
                var iface = new Native.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA)) };
                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hidGuid, i, ref iface)) break;
                int needed;
                Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                IntPtr detail = Marshal.AllocHGlobal(needed);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref iface, detail, needed, out needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    SafeFileHandle handle = Native.CreateFile(path, Native.GENERIC_READ | Native.GENERIC_WRITE, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
                    if (handle.IsInvalid) { handle.Dispose(); continue; }
                    var attr = new Native.HIDD_ATTRIBUTES { Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES)) };
                    if (!Native.HidD_GetAttributes(handle, ref attr) || attr.VendorID != 0x36B0 || attr.ProductID != 0x3142) { handle.Dispose(); continue; }
                    IntPtr prep;
                    Native.HIDP_CAPS caps;
                    if (!Native.HidD_GetPreparsedData(handle, out prep)) { handle.Dispose(); continue; }
                    try { Native.HidP_GetCaps(prep, out caps); } finally { Native.HidD_FreePreparsedData(prep); }
                    if (caps.UsagePage == 0xFF60 && caps.Usage == 0x61 && caps.InputReportByteLength >= 5 && caps.OutputReportByteLength >= 5)
                    {
                        var device = new Mk212(handle, caps.InputReportByteLength, caps.OutputReportByteLength);
                        byte[] protocol = device.Send(0x01);
                        if (protocol[1] == 0x01) return device;
                        device.Dispose();
                    }
                    else handle.Dispose();
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        throw new InvalidOperationException("MK212 (VID 36B0 / PID 3142) wurde nicht gefunden.");
    }

    public LightingState ReadState()
    {
        byte[] b = Get(1), e = Get(2), s = Get(3), c = Get(4);
        return new LightingState { Brightness = b[4], Effect = e[4], Speed = s[4], Hue = c[4], Saturation = c[5] };
    }

    public void Restore(LightingState state)
    {
        SetEffect(state.Effect);
        SetSpeed(state.Speed);
        SetColor(state.Hue, state.Saturation);
        SetBrightness(state.Brightness);
    }

    public void SetBrightness(int value) { Set(1, (byte)Math.Max(0, Math.Min(160, value))); }
    public void SetEffect(int value) { Set(2, (byte)value); }
    public void SetSpeed(int value) { Set(3, (byte)value); }
    public void SetColor(int hue, int saturation) { Set(4, (byte)hue, (byte)saturation); }
    private byte[] Get(byte valueId) { return Send(0x08, 4, valueId); }
    private void Set(byte valueId, params byte[] values)
    {
        byte[] args = new byte[2 + values.Length];
        args[0] = 4;
        args[1] = valueId;
        Array.Copy(values, 0, args, 2, values.Length);
        Send(0x07, args);
    }

    private byte[] Send(byte command, params byte[] args)
    {
        byte[] output = new byte[outputLength];
        output[0] = 0;
        output[1] = command;
        Array.Copy(args, 0, output, 2, Math.Min(args.Length, output.Length - 2));
        stream.Write(output, 0, output.Length);
        stream.Flush();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            byte[] input = new byte[inputLength];
            int read = 0;
            while (read < input.Length)
            {
                int n = stream.Read(input, read, input.Length - read);
                if (n <= 0) throw new IOException("Keine Antwort von der MK212.");
                read += n;
            }
            if (input.Length >= 2 && input[1] == command) return input;
        }
        throw new IOException("Keine passende Antwort von der MK212.");
    }

    public void Dispose() { stream.Dispose(); }
}

internal static class Native
{
    public const int DIGCF_PRESENT = 0x02, DIGCF_DEVICEINTERFACE = 0x10;
    public const int GENERIC_READ = unchecked((int)0x80000000), GENERIC_WRITE = 0x40000000;
    public const int FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3;

    [StructLayout(LayoutKind.Sequential)] public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)] public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }
    [StructLayout(LayoutKind.Sequential)] public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices;
        public ushort NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetAttributes(SafeFileHandle handle, ref HIDD_ATTRIBUTES attributes);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out IntPtr data);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);
    [DllImport("setupapi.dll", SetLastError = true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid guid, IntPtr enumerator, IntPtr hwndParent, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr info, IntPtr deviceInfo, ref Guid guid, int index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr info, ref SP_DEVICE_INTERFACE_DATA data, IntPtr detail, int detailSize, out int requiredSize, IntPtr deviceInfo);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern SafeFileHandle CreateFile(string name, int access, int share, IntPtr security, int creation, int flags, IntPtr template);
}
