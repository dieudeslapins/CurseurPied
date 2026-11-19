using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

class EquipeCurseurLapin : Form
{
    const uint SPI_SETCURSORS = 0x0057;
    const uint SPIF_SENDCHANGE = 0x0002;
    const string CURSOR_KEY = @"Control Panel\Cursors";
    const string RUN_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr LoadCursorFromFile(string path);

    [DllImport("user32.dll")]
    static extern bool DestroyCursor(IntPtr handle);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, IntPtr value, uint flags);

    string baseDir;
    Dictionary<string, string> originals = new Dictionary<string, string>();
    Dictionary<string, string> bunny = new Dictionary<string, string>();
    Label status;
    Button sizeButton;
    Timer cycle;
    bool cycling;
    int tick;

    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new EquipeCurseurLapin());
    }

    EquipeCurseurLapin()
    {
        baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string curDir = Path.Combine(baseDir, "curseurs");

        string[] names = { "", "Arrow", "AppStarting", "Crosshair", "Hand", "Help", "IBeam",
                           "No", "NWPen", "SizeAll", "SizeNESW", "SizeNS", "SizeNWSE",
                           "SizeWE", "UpArrow", "Wait" };
        string[] files = { "arrow.cur", "arrow.cur", "appstarting.ani", "crosshair.cur", "hand.cur",
                           "help.cur", "ibeam.cur", "no.cur", "nwpen.cur", "sizeall.cur",
                           "sizenesw.cur", "sizens.cur", "sizenwse.cur", "sizewe.cur",
                           "uparrow.cur", "wait.ani" };
        string[] defaults = { "aero_arrow.cur", "aero_arrow.cur", "aero_working.ani", "", "aero_link.cur",
                              "aero_helpsel.cur", "", "aero_unavail.cur", "aero_pen.cur", "aero_move.cur",
                              "aero_nesw.cur", "aero_ns.cur", "aero_nwse.cur", "aero_ew.cur",
                              "aero_up.cur", "aero_busy.ani" };

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(CURSOR_KEY, true))
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    string value = key.GetValue(name) as string;
                    if (String.IsNullOrEmpty(value) ||
                        value.IndexOf("CurseurLapin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        value.IndexOf("curseurs", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!String.IsNullOrEmpty(value) || String.IsNullOrEmpty(defaults[i]))
                            value = String.IsNullOrEmpty(defaults[i]) ? "" :
                                Environment.ExpandEnvironmentVariables(@"%SystemRoot%\Cursors\" + defaults[i]);
                    }
                    originals[name] = value;
                    bunny[name] = Path.Combine(curDir, files[i]);
                    key.DeleteValue("(Default)", false);
                }
            }
        }
        catch (Exception ex)
        {
            Log("Init registre: " + ex.Message);
        }

        Text = "Equipe-Curseur Lapin (tous les curseurs)";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(390, 285);
        BackColor = Color.FromArgb(255, 253, 245, 250);
        Font = new Font("Segoe UI", 9);

        Label title = new Label();
        title.Text = "Curseurs PIEDS chair - 144px";
        title.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        title.ForeColor = Color.FromArgb(255, 120, 40, 80);
        title.SetBounds(16, 12, 350, 28);
        Controls.Add(title);

        Button all = MakeButton("Appliquer TOUS les curseurs PIEDS", 16, 48);
        all.Click += (s, e) => ApplyAll(true);
        Controls.Add(all);

        Button restore = MakeButton("Restaurer TOUS les curseurs Windows", 16, 86);
        restore.Click += (s, e) => { StopCycle(); ApplyAll(false); };
        Controls.Add(restore);

        Button cycleButton = MakeButton("Cycle automatique 5 s (lapin / Windows)", 16, 124);
        cycleButton.Click += (s, e) => ToggleCycle();
        Controls.Add(cycleButton);

        Button furry = MakeButton("Appliquer le curseur PIEDS principal", 16, 162);
        furry.Click += (s, e) => ApplyFurry();
        Controls.Add(furry);

        sizeButton = MakeButton("Taille Windows : 48 px (cliquer pour changer)", 16, 200);
        sizeButton.Click += (s, e) => CycleCursorSize();
        Controls.Add(sizeButton);

        CheckBox startup = new CheckBox();
        startup.Text = "Lancer avec Windows";
        startup.SetBounds(16, 241, 180, 22);
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_KEY))
            startup.Checked = key.GetValue("EquipeCurseurLapin") != null;
        startup.CheckedChanged += (s, e) =>
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RUN_KEY, true))
            {
                if (startup.Checked)
                    key.SetValue("EquipeCurseurLapin", "\"" + Application.ExecutablePath + "\"");
                else
                    key.DeleteValue("EquipeCurseurLapin", false);
            }
        };
        Controls.Add(startup);

        status = new Label();
        status.SetBounds(200, 241, 175, 28);
        status.ForeColor = Color.FromArgb(255, 70, 70, 70);
        Controls.Add(status);

        cycle = new Timer();
        cycle.Interval = 5000;
        cycle.Tick += Cycle_Tick;

        UpdateSizeLabel();
    }

    static int[] SIZES = { 32, 48, 64, 96, 128 };

    int GetCursorSize()
    {
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(CURSOR_KEY))
            {
                object v = key.GetValue("CursorBaseSize");
                if (v is int) return (int)v;
            }
        }
        catch (Exception ex) { Log("Taille lecture: " + ex.Message); }
        return 32;
    }

    void UpdateSizeLabel()
    {
        sizeButton.Text = "Taille Windows : " + GetCursorSize() + " px (cliquer pour changer)";
    }

    void CycleCursorSize()
    {
        int current = GetCursorSize();
        int index = 0;
        for (int i = 0; i < SIZES.Length; i++)
            if (SIZES[i] == current) index = i;
        int next = SIZES[(index + 1) % SIZES.Length];
        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(CURSOR_KEY, true))
                key.SetValue("CursorBaseSize", next, RegistryValueKind.DWord);
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE);
            UpdateSizeLabel();
            status.Text = "Taille " + next + " px";
            Log("Taille curseur=" + next);
        }
        catch (Exception ex)
        {
            status.Text = "Erreur taille";
            Log("Taille ecriture: " + ex.Message);
        }
    }

    Button MakeButton(string text, int x, int y)
    {
        Button b = new Button();
        b.Text = text;
        b.SetBounds(x, y, 355, 31);
        b.FlatStyle = FlatStyle.Flat;
        b.BackColor = Color.White;
        b.ForeColor = Color.FromArgb(255, 70, 35, 55);
        return b;
    }

    void Log(string message)
    {
        try { File.AppendAllText(Path.Combine(baseDir, "curseur.log"), DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine); }
        catch { }
    }

    void ApplyAll(bool useBunny)
    {
        Dictionary<string, string> source = useBunny ? bunny : originals;
        int done = 0;

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(CURSOR_KEY, true))
            {
                foreach (KeyValuePair<string, string> pair in source)
                {
                    string path = pair.Value;
                    if (!String.IsNullOrEmpty(path))
                    {
                        string real = Environment.ExpandEnvironmentVariables(path);
                        if (!File.Exists(real)) { Log("Manquant: " + real); continue; }
                        IntPtr handle = LoadCursorFromFile(real);
                        if (handle == IntPtr.Zero) { Log("Invalide: " + real); continue; }
                        DestroyCursor(handle);
                    }
                    key.SetValue(pair.Key, path, RegistryValueKind.String);
                    key.DeleteValue("(Default)", false);
                    done++;
                }
            }
        }
        catch (Exception ex)
        {
            status.Text = "Erreur registre";
            Log("Registre: " + ex.Message);
            return;
        }

        bool ok = SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE);
        status.Text = done + " curseurs " + (useBunny ? "PIEDS" : "Windows");
        Log("Apply bunny=" + useBunny + " count=" + done + " SPI=" + ok);
    }

    void ApplyFurry()
    {
        StopCycle();
        string path = Path.Combine(baseDir, "curseurs", "arrow.cur");
        if (!File.Exists(path))
        {
            status.Text = "Curseur introuvable";
            Log("Furry manquant: " + path);
            return;
        }

        IntPtr handle = LoadCursorFromFile(path);
        if (handle == IntPtr.Zero)
        {
            status.Text = "Curseur invalide";
            Log("Furry invalide: " + path);
            return;
        }
        DestroyCursor(handle);

        try
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(CURSOR_KEY, true))
            {
                key.SetValue("Arrow", path, RegistryValueKind.String);
                key.SetValue("", path, RegistryValueKind.String);
                key.DeleteValue("(Default)", false);
            }
        }
        catch (Exception ex)
        {
            status.Text = "Erreur registre";
            Log("Furry registre: " + ex.Message);
            return;
        }

        bool ok = SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, SPIF_SENDCHANGE);
        status.Text = "Curseur PIEDS actif";
        Log("Apply principal SPI=" + ok);
    }

    void ToggleCycle()
    {
        if (cycling) { StopCycle(); status.Text = "Cycle arrete"; }
        else { cycling = true; tick = 0; cycle.Start(); status.Text = "Cycle actif"; }
    }

    void StopCycle()
    {
        if (cycling) { cycle.Stop(); cycling = false; }
    }

    void Cycle_Tick(object sender, EventArgs e)
    {
        tick++;
        ApplyAll(tick % 2 == 1);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopCycle();
        base.OnFormClosing(e);
    }
}
