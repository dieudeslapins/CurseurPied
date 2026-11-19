using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

class ApercuCurseur : Form
{
    class Item
    {
        public string Label;
        public string Resource;
        public byte[] Raw;
    }

    class Frame
    {
        public Image Img;
        public int Hx;
        public int Hy;
        public int Rate;
    }

    ListBox list;
    Panel preview;
    NumericUpDown zoom;
    CheckBox realCursor;
    Label info;
    Timer timer;
    List<Item> items = new List<Item>();
    List<Frame> frames = new List<Frame>();
    Dictionary<string, Cursor> realCursors = new Dictionary<string, Cursor>();
    Dictionary<string, Point> customHotspots = new Dictionary<string, Point>();
    int currentFrame;

    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new ApercuCurseur());
    }

    ApercuCurseur()
    {
        items.Add(new Item { Label = "Selection (arrow)", Resource = "arrow.cur" });
        items.Add(new Item { Label = "Traitement en arriere-plan", Resource = "appstarting.ani" });
        items.Add(new Item { Label = "Precision (crosshair)", Resource = "crosshair.cur" });
        items.Add(new Item { Label = "Lien (hand)", Resource = "hand.cur" });
        items.Add(new Item { Label = "Aide (help)", Resource = "help.cur" });
        items.Add(new Item { Label = "Texte / stylo (ibeam)", Resource = "ibeam.cur" });
        items.Add(new Item { Label = "Interdit (no)", Resource = "no.cur" });
        items.Add(new Item { Label = "Stylo = Texte (nwpen)", Resource = "nwpen.cur" });
        items.Add(new Item { Label = "Deplacement (sizeall)", Resource = "sizeall.cur" });
        items.Add(new Item { Label = "Redim. NE-SO", Resource = "sizenesw.cur" });
        items.Add(new Item { Label = "Redim. vertical", Resource = "sizens.cur" });
        items.Add(new Item { Label = "Redim. NO-SE", Resource = "sizenwse.cur" });
        items.Add(new Item { Label = "Redim. horizontal", Resource = "sizewe.cur" });
        items.Add(new Item { Label = "Fleche haut", Resource = "uparrow.cur" });
        items.Add(new Item { Label = "Occupee (wait)", Resource = "wait.ani" });
        items.Add(new Item { Label = "GIF attente sans jambes (30 images)", Resource = "attente_sans_jambes.gif" });

        Text = "Apercu des curseurs PIED";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, 560);
        Font = new Font("Segoe UI", 9);
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Label title = new Label();
        title.Text = "Etat du curseur - apercu sans installation";
        title.Font = new Font("Segoe UI", 13, FontStyle.Bold);
        title.ForeColor = Color.FromArgb(255, 120, 40, 80);
        title.SetBounds(16, 12, 680, 28);
        Controls.Add(title);

        list = new ListBox();
        list.SetBounds(16, 48, 260, 390);
        foreach (Item it in items) list.Items.Add(it.Label);
        list.SelectedIndexChanged += List_SelectedIndexChanged;
        Controls.Add(list);

        preview = new Panel();
        preview.SetBounds(292, 48, 412, 412);
        preview.BackColor = Color.White;
        preview.BorderStyle = BorderStyle.FixedSingle;
        preview.Paint += Preview_Paint;
        preview.MouseClick += Preview_MouseClick;
        Controls.Add(preview);

        info = new Label();
        info.SetBounds(292, 466, 412, 26);
        info.ForeColor = Color.FromArgb(255, 70, 70, 70);
        Controls.Add(info);

        Label zl = new Label();
        zl.Text = "Zoom";
        zl.SetBounds(16, 452, 50, 24);
        Controls.Add(zl);
        zoom = new NumericUpDown();
        zoom.SetBounds(66, 448, 70, 28);
        zoom.Minimum = 1;
        zoom.Maximum = 6;
        zoom.Value = 3;
        zoom.ValueChanged += delegate { preview.Invalidate(); };
        Controls.Add(zoom);

        Button reset = new Button();
        reset.Text = "Reset point chaud";
        reset.SetBounds(150, 448, 150, 28);
        reset.FlatStyle = FlatStyle.Flat;
        reset.Click += Reset_Click;
        Controls.Add(reset);

        realCursor = new CheckBox();
        realCursor.Text = "Voir aussi comme vrai curseur sur la fenetre";
        realCursor.SetBounds(16, 490, 420, 24);
        realCursor.CheckedChanged += RealCursor_CheckedChanged;
        Controls.Add(realCursor);

        Label note = new Label();
        note.Text = "Aucun fichier ecrit, aucun registre modifie. Supprimez simplement l'EXE.";
        note.SetBounds(16, 522, 680, 20);
        note.ForeColor = Color.FromArgb(255, 120, 120, 120);
        Controls.Add(note);

        timer = new Timer();
        timer.Interval = 100;
        timer.Tick += Timer_Tick;

        LoadItem(0);
    }

    byte[] Resource(Item it)
    {
        if (it.Raw == null)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(it.Resource))
            {
                if (s == null) return new byte[0];
                it.Raw = new byte[s.Length];
                s.Read(it.Raw, 0, it.Raw.Length);
            }
        }
        return it.Raw;
    }

    static int U32(byte[] d, int p)
    {
        return d[p] | (d[p + 1] << 8) | (d[p + 2] << 16) | (d[p + 3] << 24);
    }

    static Frame FrameFromCur(byte[] d, int rate)
    {
        Frame f = new Frame();
        if (d.Length < 22) return f;
        f.Hx = d[10] | (d[11] << 8);
        f.Hy = d[12] | (d[13] << 8);
        int len = U32(d, 14);
        int off = U32(d, 18);
        if (off > 0 && off + len <= d.Length && len > 8)
        {
            using (MemoryStream ms = new MemoryStream(d, off, len))
                f.Img = Image.FromStream(ms);
        }
        else if (d.Length > 22)
        {
            using (MemoryStream ms = new MemoryStream(d, 22, d.Length - 22))
                f.Img = Image.FromStream(ms);
        }
        f.Rate = rate;
        return f;
    }

    static List<Frame> Parse(byte[] d)
    {
        List<Frame> result = new List<Frame>();
        if (d.Length < 4) return result;

        if (d[0] == (byte)'G' && d[1] == (byte)'I' && d[2] == (byte)'F')
        {
            using (MemoryStream ms = new MemoryStream(d))
            using (Image im = Image.FromStream(ms))
            {
                System.Drawing.Imaging.FrameDimension dim =
                    new System.Drawing.Imaging.FrameDimension(im.FrameDimensionsList[0]);
                int cnt = im.GetFrameCount(dim);
                for (int i = 0; i < cnt; i++)
                {
                    im.SelectActiveFrame(dim, i);
                    Bitmap bm = new Bitmap(im);
                    Frame fr = new Frame();
                    fr.Img = bm;
                    fr.Hx = bm.Width / 2;
                    fr.Hy = bm.Height / 2;
                    fr.Rate = 100;
                    result.Add(fr);
                }
            }
            return result;
        }

        if (d[0] == (byte)'R' && d[1] == (byte)'I' && d[2] == (byte)'F' && d[3] == (byte)'F')
        {
            int rate = 100;
            List<byte[]> icons = new List<byte[]>();
            int pos = 12;
            while (pos + 8 <= d.Length)
            {
                string id = Encoding.ASCII.GetString(d, pos, 4);
                int size = U32(d, pos + 4);
                int data = pos + 8;
                if (data + size > d.Length) break;

                if (id == "anih" && size >= 32)
                {
                    int jiffies = U32(d, data + 28);
                    rate = (int)Math.Round(jiffies * 1000.0 / 60.0);
                    if (rate < 30) rate = 30;
                }
                if (id == "LIST" && size >= 4 &&
                    Encoding.ASCII.GetString(d, data, 4) == "fram")
                {
                    int p = data + 4;
                    int end = data + size;
                    while (p + 8 <= end)
                    {
                        string sub = Encoding.ASCII.GetString(d, p, 4);
                        int sz = U32(d, p + 4);
                        if (sub == "icon" && p + 8 + sz <= d.Length)
                        {
                            byte[] cur = new byte[sz];
                            Array.Copy(d, p + 8, cur, 0, sz);
                            icons.Add(cur);
                        }
                        p += 8 + sz + (sz % 2);
                    }
                }
                pos = data + size + (size % 2);
            }
            foreach (byte[] cur in icons) result.Add(FrameFromCur(cur, rate));
            if (result.Count == 0) result.Add(FrameFromCur(d, rate));
        }
        else
        {
            result.Add(FrameFromCur(d, 100));
        }
        return result;
    }

    void LoadItem(int index)
    {
        foreach (Frame f in frames) if (f.Img != null) f.Img.Dispose();
        Item it = items[index];
        frames = Parse(Resource(it));
        Point cp;
        if (customHotspots.TryGetValue(it.Resource, out cp))
            foreach (Frame f in frames) { f.Hx = cp.X; f.Hy = cp.Y; }
        currentFrame = 0;
        timer.Stop();
        if (frames.Count > 1)
        {
            timer.Interval = frames[0].Rate;
            timer.Start();
        }
        UpdateInfo();
        preview.Invalidate();
        ApplyRealCursor();
    }

    void UpdateInfo()
    {
        if (list.SelectedIndex < 0) return;
        Item it = items[list.SelectedIndex];
        bool custom = customHotspots.ContainsKey(it.Resource);
        info.Text = it.Resource + "  |  " + frames.Count + " image(s)  |  point chaud "
            + (frames.Count > 0 ? frames[0].Hx + "," + frames[0].Hy : "?")
            + (custom ? " ( PERSONNALISE )" : "  - cliquez sur l'image pour le deplacer");
    }

    void List_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (list.SelectedIndex >= 0) LoadItem(list.SelectedIndex);
    }

    void Timer_Tick(object sender, EventArgs e)
    {
        if (frames.Count < 2) return;
        currentFrame = (currentFrame + 1) % frames.Count;
        preview.Invalidate();
    }

    void Preview_Paint(object sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        int cell = 16;
        for (int y = 0; y < preview.Height; y += cell)
            for (int x = 0; x < preview.Width; x += cell)
            {
                bool dark = ((x / cell) + (y / cell)) % 2 == 0;
                using (SolidBrush b = new SolidBrush(dark ? Color.FromArgb(255, 238, 238, 238) : Color.White))
                    g.FillRectangle(b, x, y, cell, cell);
            }

        if (frames.Count == 0 || frames[currentFrame].Img == null) return;
        Frame f = frames[currentFrame];
        int sc = (int)zoom.Value;
        int w = f.Img.Width * sc;
        int h = f.Img.Height * sc;
        int dx = (preview.Width - w) / 2;
        int dy = (preview.Height - h) / 2;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(f.Img, dx, dy, w, h);

        int hx = dx + f.Hx * sc;
        int hy = dy + f.Hy * sc;
        using (Pen p = new Pen(Color.FromArgb(220, 220, 30, 30), 2))
        {
            g.DrawLine(p, hx - 10, hy, hx + 10, hy);
            g.DrawLine(p, hx, hy - 10, hx, hy + 10);
            g.DrawEllipse(p, hx - 5, hy - 5, 10, 10);
        }
    }

    void Preview_MouseClick(object sender, MouseEventArgs e)
    {
        if (list.SelectedIndex < 0 || frames.Count == 0 || frames[currentFrame].Img == null) return;
        Frame f = frames[currentFrame];
        int sc = (int)zoom.Value;
        int w = f.Img.Width * sc;
        int h = f.Img.Height * sc;
        int dx = (preview.Width - w) / 2;
        int dy = (preview.Height - h) / 2;
        int hx = (e.X - dx) / sc;
        int hy = (e.Y - dy) / sc;
        if (hx < 0) hx = 0;
        if (hy < 0) hy = 0;
        if (hx >= f.Img.Width) hx = f.Img.Width - 1;
        if (hy >= f.Img.Height) hy = f.Img.Height - 1;

        Item it = items[list.SelectedIndex];
        customHotspots[it.Resource] = new Point(hx, hy);
        foreach (Frame fr in frames) { fr.Hx = hx; fr.Hy = hy; }
        UpdateInfo();
        preview.Invalidate();
        ApplyRealCursor();
    }

    void Reset_Click(object sender, EventArgs e)
    {
        if (list.SelectedIndex < 0) return;
        customHotspots.Remove(items[list.SelectedIndex].Resource);
        LoadItem(list.SelectedIndex);
    }

    Cursor BuildCursor(Frame f, int hx, int hy)
    {
        if (f == null || f.Img == null) return Cursors.Default;
        try
        {
            byte[] png;
            using (MemoryStream ms = new MemoryStream()) { f.Img.Save(ms, System.Drawing.Imaging.ImageFormat.Png); png = ms.ToArray(); }
            byte[] cur = new byte[22 + png.Length];
            cur[2] = 2;
            cur[4] = 1;
            cur[6] = 32;
            cur[7] = 32;
            cur[10] = (byte)(hx & 255); cur[11] = (byte)(hx >> 8);
            cur[12] = (byte)(hy & 255); cur[13] = (byte)(hy >> 8);
            cur[14] = (byte)(png.Length & 255); cur[15] = (byte)((png.Length >> 8) & 255);
            cur[16] = (byte)((png.Length >> 16) & 255); cur[17] = (byte)((png.Length >> 24) & 255);
            cur[18] = 22;
            Array.Copy(png, 0, cur, 22, png.Length);
            return new Cursor(new MemoryStream(cur));
        }
        catch { return Cursors.Default; }
    }

    void ApplyRealCursor()
    {
        if (!realCursor.Checked || list.SelectedIndex < 0)
        {
            Cursor = Cursors.Default;
            preview.Cursor = Cursors.Default;
            return;
        }
        Item it = items[list.SelectedIndex];
        Point cp;
        bool custom = customHotspots.TryGetValue(it.Resource, out cp);
        Frame first = frames.Count > 0 ? frames[0] : null;
        int hx = custom ? cp.X : (first != null ? first.Hx : 16);
        int hy = custom ? cp.Y : (first != null ? first.Hy : 16);
        string key = it.Resource + "|" + hx + "," + hy + "|" + (custom ? "custom" : "raw");

        Cursor c;
        if (!realCursors.TryGetValue(key, out c))
        {
            if (custom)
            {
                c = BuildCursor(first, hx, hy);
            }
            else
            {
                try { c = new Cursor(new MemoryStream(Resource(it))); }
                catch { c = BuildCursor(first, hx, hy); }
            }
            realCursors[key] = c;
        }
        Cursor = c;
        preview.Cursor = c;
    }

    void RealCursor_CheckedChanged(object sender, EventArgs e)
    {
        ApplyRealCursor();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        timer.Stop();
        base.OnFormClosing(e);
    }
}
