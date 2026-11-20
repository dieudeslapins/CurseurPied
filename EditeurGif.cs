using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

class FrameData
{
    public Bitmap Bmp;
    public int Delay;
}

class CanvasCtrl : Control
{
    public EditeurGif Owner;
    public CanvasCtrl() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true); }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        int cell = 16;
        for (int y = 0; y < Height; y += cell)
            for (int x = 0; x < Width; x += cell)
            {
                bool dark = ((x / cell) + (y / cell)) % 2 == 0;
                using (SolidBrush b = new SolidBrush(dark ? Color.FromArgb(255, 225, 225, 225) : Color.White))
                    g.FillRectangle(b, x, y, cell, cell);
            }
        if (Owner != null) Owner.PaintFrame(g);
    }

    protected override void OnMouseDown(MouseEventArgs e) { if (Owner != null) Owner.HandleMouseDown(e); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (Owner != null) Owner.HandleMouseMove(e); base.OnMouseMove(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (Owner != null) Owner.HandleMouseUp(e); base.OnMouseUp(e); }
}

class EditeurGif : Form
{
    enum Tool { Crayon, Gomme, Pipette, Remplir, Rectangle, Recadrer }
    enum Scope { Courante, Cochees, Toutes }

    List<FrameData> frames = new List<FrameData>();
    List<Dictionary<int, Bitmap>> undo = new List<Dictionary<int, Bitmap>>();
    int current;
    Tool tool = Tool.Crayon;
    Scope scope = Scope.Courante;
    Color brushColor = Color.FromArgb(216, 190, 179);
    int brushSize = 4;
    int tolerance = 30;
    int zoom = 1;
    bool drawing;
    Point start, last;
    List<Point> stroke = new List<Point>();
    Rectangle cropRect = Rectangle.Empty;

    CanvasCtrl canvas;
    Panel scroll;
    CheckedListBox frameList;
    ComboBox scopeBox;
    NumericUpDown sizeBox, tolBox, zoomBox;
    Button colorButton;
    Label info;
    string currentPath = "";

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new EditeurGif());
    }

    EditeurGif()
    {
        Text = "Editeur GIF - frame par frame";
        ClientSize = new Size(1060, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        AllowDrop = true;
        DragEnter += Files_DragEnter;
        DragDrop += Files_DragDrop;
        try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        MenuStrip menu = new MenuStrip();
        ToolStripMenuItem fm = new ToolStripMenuItem("Fichier");
        fm.DropDownItems.Add("Ouvrir un GIF... (ou glisser le fichier)", null, Ouvrir_Click);
        fm.DropDownItems.Add("Enregistrer le GIF...", null, Enregistrer_Click);
        fm.DropDownItems.Add("Exporter les frames en PNG...", null, ExporterPng_Click);
        fm.DropDownItems.Add("Quitter", null, delegate { Close(); });
        menu.Items.Add(fm);
        Controls.Add(menu);

        ToolStrip bar = new ToolStrip();
        bar.SetBounds(0, menu.Height, Width, 34);
        AddTool(bar, "Crayon", Tool.Crayon);
        AddTool(bar, "Gomme", Tool.Gomme);
        AddTool(bar, "Pipette", Tool.Pipette);
        AddTool(bar, "Remplir", Tool.Remplir);
        AddTool(bar, "Rectangle", Tool.Rectangle);
        AddTool(bar, "Recadrer", Tool.Recadrer);
        bar.Items.Add(new ToolStripSeparator());
        colorButton = new Button();
        colorButton.Text = "Couleur";
        colorButton.Width = 80;
        colorButton.Click += Couleur_Click;
        ToolStripControlHost colorHost = new ToolStripControlHost(colorButton);
        bar.Items.Add(colorHost);
        bar.Items.Add(new ToolStripLabel("Taille"));
        sizeBox = new NumericUpDown(); sizeBox.Minimum = 1; sizeBox.Maximum = 60; sizeBox.Value = brushSize; sizeBox.Width = 52;
        sizeBox.ValueChanged += delegate { brushSize = (int)sizeBox.Value; };
        bar.Items.Add(new ToolStripControlHost(sizeBox));
        bar.Items.Add(new ToolStripLabel("Tol."));
        tolBox = new NumericUpDown(); tolBox.Minimum = 0; tolBox.Maximum = 255; tolBox.Value = tolerance; tolBox.Width = 52;
        tolBox.ValueChanged += delegate { tolerance = (int)tolBox.Value; };
        bar.Items.Add(new ToolStripControlHost(tolBox));
        bar.Items.Add(new ToolStripLabel("Zoom"));
        zoomBox = new NumericUpDown(); zoomBox.Minimum = 1; zoomBox.Maximum = 6; zoomBox.Value = 1; zoomBox.Width = 52;
        zoomBox.ValueChanged += delegate { zoom = (int)zoomBox.Value; ResizeCanvas(); };
        bar.Items.Add(new ToolStripControlHost(zoomBox));
        Controls.Add(bar);

        int top = menu.Height + bar.Height + 6;

        frameList = new CheckedListBox();
        frameList.SetBounds(10, top, 220, 480);
        frameList.SelectedIndexChanged += FrameList_SelectedIndexChanged;
        Controls.Add(frameList);

        Label l1 = new Label(); l1.Text = "Appliquer a"; l1.SetBounds(10, top + 488, 100, 20); Controls.Add(l1);
        scopeBox = new ComboBox();
        scopeBox.Items.AddRange(new object[] { "Frame courante", "Frames cochees", "Toutes les frames" });
        scopeBox.SelectedIndex = 0;
        scopeBox.SetBounds(10, top + 508, 220, 26);
        scopeBox.SelectedIndexChanged += delegate { scope = (Scope)scopeBox.SelectedIndex; };
        Controls.Add(scopeBox);

        Button btTrans = new Button();
        btTrans.Text = "Rendre transparent (pipette)";
        btTrans.SetBounds(10, top + 542, 220, 32);
        btTrans.Click += Transparent_Click;
        Controls.Add(btTrans);

        Button btUndo = new Button();
        btUndo.Text = "Annuler";
        btUndo.SetBounds(10, top + 580, 105, 30);
        btUndo.Click += Undo_Click;
        Controls.Add(btUndo);

        Button btCheck = new Button();
        btCheck.Text = "Tout cocher";
        btCheck.SetBounds(125, top + 580, 105, 30);
        btCheck.Click += delegate { for (int i = 0; i < frameList.Items.Count; i++) frameList.SetItemChecked(i, true); };
        Controls.Add(btCheck);

        scroll = new Panel();
        scroll.SetBounds(240, top, 620, 580);
        scroll.AutoScroll = true;
        scroll.BackColor = Color.FromArgb(255, 200, 200, 200);
        Controls.Add(scroll);
        canvas = new CanvasCtrl();
        canvas.Owner = this;
        HookDrop(scroll);
        HookDrop(canvas);
        scroll.Controls.Add(canvas);

        info = new Label();
        info.SetBounds(240, top + 590, 800, 40);
        info.ForeColor = Color.FromArgb(255, 80, 80, 80);
        Controls.Add(info);

        UpdateColor();

        // Ouverture par double-clic / "Ouvrir avec" (n'importe quel GIF en argument).
        string argPath = "";
        foreach (string a in Environment.GetCommandLineArgs())
        {
            if (a != Application.ExecutablePath && File.Exists(a)) { argPath = a; break; }
        }
        if (argPath.Length > 0) { LoadGif(argPath); return; }

        string p = Path.Combine(Application.StartupPath, "attente_sans_jambes.gif");
        if (!File.Exists(p)) p = @"C:\Users\Eleve\Downloads\Sans titre.gif";
        if (File.Exists(p)) LoadGif(p); else UpdateInfo();
    }

    void HookDrop(Control c)
    {
        c.AllowDrop = true;
        c.DragEnter += Files_DragEnter;
        c.DragDrop += Files_DragDrop;
    }

    void Files_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
    }

    void Files_DragDrop(object sender, DragEventArgs e)
    {
        string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;
        LoadGif(files[0]);
    }

    void AddTool(ToolStrip bar, string label, Tool t)
    {
        ToolStripButton b = new ToolStripButton(label);
        b.Tag = t;
        b.Click += delegate { tool = t; UpdateInfo(); };
        bar.Items.Add(b);
    }

    void UpdateColor()
    {
        colorButton.BackColor = brushColor;
        colorButton.ForeColor = (brushColor.R + brushColor.G + brushColor.B > 380) ? Color.Black : Color.White;
    }

    void UpdateInfo()
    {
        info.Text = (frames.Count > 0 ? "Frame " + (current + 1) + "/" + frames.Count : "Aucune frame")
            + "  |  outil: " + tool + "  |  couleur: " + brushColor.R + "," + brushColor.G + "," + brushColor.B
            + "  |  anime " + (frames.Count > 0 ? frames[current].Delay : 0) + " ms"
            + (currentPath.Length > 0 ? "  |  " + Path.GetFileName(currentPath) : "");
    }

    void ResizeCanvas()
    {
        if (frames.Count == 0) { canvas.Size = new Size(1, 1); return; }
        canvas.Size = new Size(frames[current].Bmp.Width * zoom, frames[current].Bmp.Height * zoom);
        canvas.Invalidate();
    }

    void ClearFrames()
    {
        foreach (FrameData f in frames) if (f.Bmp != null) f.Bmp.Dispose();
        frames.Clear();
        foreach (Dictionary<int, Bitmap> snap in undo)
            foreach (Bitmap b in snap.Values) b.Dispose();
        undo.Clear();
    }

    void LoadGif(string path)
    {
        if (!File.Exists(path)) return;
        List<FrameData> loaded = new List<FrameData>();
        try
        {
            // FileStream evite de verrouiller le fichier et accepte tout GIF lisible.
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image img = Image.FromStream(fs))
            {
                FrameDimension dim = null;
                int n = 1;
                foreach (Guid g in img.FrameDimensionsList)
                {
                    FrameDimension d = new FrameDimension(g);
                    int count = img.GetFrameCount(d);
                    if (count > n) { n = count; dim = d; }
                }
                if (dim == null)
                    dim = img.FrameDimensionsList.Length > 0
                        ? new FrameDimension(img.FrameDimensionsList[0]) : new FrameDimension(FrameDimension.Time.Guid);

                // Delais propres a chaque frame (propriete GIF 0x5100, en 1/100 s).
                byte[] delays = null;
                try
                {
                    PropertyItem pi = img.GetPropertyItem(0x5100);
                    if (pi != null && pi.Value != null) delays = pi.Value;
                }
                catch { }

                for (int i = 0; i < n; i++)
                {
                    int ms = 100;
                    if (delays != null && delays.Length >= (i + 1) * 4)
                    {
                        int j = BitConverter.ToUInt16(delays, i * 4);
                        if (j > 0) ms = j * 10;
                    }
                    if (ms < 20) ms = 100;
                    try { if (n > 1) img.SelectActiveFrame(dim, i); } catch { }
                    loaded.Add(new FrameData { Bmp = new Bitmap(img), Delay = ms });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Impossible d'ouvrir ce GIF :\n" + ex.Message, "Editeur GIF",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (loaded.Count == 0) return;

        ClearFrames();
        frames = loaded;
        currentPath = path;
        current = 0;
        frameList.Items.Clear();
        for (int i = 0; i < frames.Count; i++)
            frameList.Items.Add("Frame " + (i + 1) + "  (" + frames[i].Delay + " ms)");
        frameList.SelectedIndex = 0;
        Text = "Editeur GIF - " + Path.GetFileName(path) + " - frame par frame";
        zoom = 1;
        try { zoomBox.Value = 1; } catch { }
        ResizeCanvas();
        UpdateInfo();
    }

    void FrameList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (frameList.SelectedIndex < 0 || frameList.SelectedIndex >= frames.Count) return;
        current = frameList.SelectedIndex;
        ResizeCanvas();
        UpdateInfo();
    }

    void Ouvrir_Click(object sender, EventArgs e)
    {
        OpenFileDialog d = new OpenFileDialog();
        d.Filter = "Tous les fichiers|*.*|GIF (*.gif)|*.gif";
        d.Title = "Ouvrir un GIF (n'importe quel fichier)";
        if (d.ShowDialog() == DialogResult.OK) LoadGif(d.FileName);
    }

    void Couleur_Click(object sender, EventArgs e)
    {
        ColorDialog d = new ColorDialog();
        d.Color = brushColor;
        if (d.ShowDialog() == DialogResult.OK) { brushColor = d.Color; UpdateColor(); UpdateInfo(); }
    }

    List<int> Targets()
    {
        List<int> res = new List<int>();
        if (scope == Scope.Courante) res.Add(current);
        else if (scope == Scope.Toutes) { for (int i = 0; i < frames.Count; i++) res.Add(i); }
        else
        {
            for (int i = 0; i < frames.Count && i < frameList.Items.Count; i++)
                if (frameList.GetItemChecked(i)) res.Add(i);
            if (res.Count == 0) res.Add(current);
        }
        return res;
    }

    void PushUndo(List<int> idx)
    {
        Dictionary<int, Bitmap> snap = new Dictionary<int, Bitmap>();
        foreach (int i in idx) snap[i] = (Bitmap)frames[i].Bmp.Clone();
        undo.Add(snap);
        if (undo.Count > 25) { foreach (Bitmap b in undo[0].Values) b.Dispose(); undo.RemoveAt(0); }
    }

    void Undo_Click(object sender, EventArgs e)
    {
        if (undo.Count == 0) return;
        Dictionary<int, Bitmap> snap = undo[undo.Count - 1];
        foreach (KeyValuePair<int, Bitmap> kv in snap)
        {
            if (kv.Key >= frames.Count) { kv.Value.Dispose(); continue; }
            frames[kv.Key].Bmp.Dispose();
            frames[kv.Key].Bmp = kv.Value;
        }
        undo.RemoveAt(undo.Count - 1);
        canvas.Invalidate();
        UpdateInfo();
    }

    public void PaintFrame(Graphics g)
    {
        if (frames.Count == 0 || current >= frames.Count) return;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(frames[current].Bmp, 0, 0, canvas.Width, canvas.Height);
        if (tool == Tool.Recadrer && !cropRect.IsEmpty)
        {
            using (Pen p = new Pen(Color.Red, 2)) { p.DashStyle = DashStyle.Dash; g.DrawRectangle(p, cropRect.X * zoom, cropRect.Y * zoom, cropRect.Width * zoom, cropRect.Height * zoom); }
        }
        if (drawing && (tool == Tool.Rectangle) && stroke.Count > 0)
        {
            using (Pen p = new Pen(brushColor, brushSize)) g.DrawRectangle(p, start.X * zoom, start.Y * zoom, (last.X - start.X) * zoom, (last.Y - start.Y) * zoom);
        }
    }

    Point Pt(MouseEventArgs e)
    {
        int x = e.X / zoom, y = e.Y / zoom;
        if (frames.Count == 0) return new Point(x, y);
        x = Math.Max(0, Math.Min(x, frames[current].Bmp.Width - 1));
        y = Math.Max(0, Math.Min(y, frames[current].Bmp.Height - 1));
        return new Point(x, y);
    }

    public void HandleMouseDown(MouseEventArgs e)
    {
        if (frames.Count == 0) return;
        Point p = Pt(e);
        if (tool == Tool.Pipette)
        {
            brushColor = frames[current].Bmp.GetPixel(p.X, p.Y);
            UpdateColor(); UpdateInfo(); canvas.Invalidate();
            return;
        }
        if (tool == Tool.Remplir)
        {
            List<int> tg = Targets();
            PushUndo(tg);
            foreach (int i in tg) Flood(frames[i].Bmp, p.X, p.Y, brushColor, tolerance);
            canvas.Invalidate(); return;
        }
        drawing = true;
        start = p; last = p;
        stroke = new List<Point>(); stroke.Add(p);
        cropRect = Rectangle.Empty;
    }

    public void HandleMouseMove(MouseEventArgs e)
    {
        if (!drawing) return;
        Point p = Pt(e);
        last = p;
        if (tool == Tool.Crayon || tool == Tool.Gomme) stroke.Add(p);
        canvas.Invalidate();
    }

    public void HandleMouseUp(MouseEventArgs e)
    {
        if (!drawing) return;
        drawing = false;
        Point p = Pt(e);
        List<int> tg = Targets();
        PushUndo(tg);
        if (tool == Tool.Recadrer)
        {
            Rectangle r = Rectangle.FromLTRB(Math.Min(start.X, p.X), Math.Min(start.Y, p.Y), Math.Max(start.X, p.X), Math.Max(start.Y, p.Y));
            if (r.Width < 4 || r.Height < 4) { undo.RemoveAt(undo.Count - 1); canvas.Invalidate(); return; }
            foreach (int i in tg)
            {
                Bitmap b = frames[i].Bmp;
                if (r.X + r.Width > b.Width || r.Y + r.Height > b.Height) continue;
                Bitmap c = b.Clone(r, b.PixelFormat);
                b.Dispose(); frames[i].Bmp = c;
            }
            cropRect = Rectangle.Empty;
            ResizeCanvas(); UpdateInfo(); return;
        }
        foreach (int i in tg)
        {
            Bitmap b = frames[i].Bmp;
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (tool == Tool.Gomme)
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    using (Pen pen = new Pen(Color.FromArgb(0, 0, 0, 0), brushSize)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; DrawStroke(g, pen); }
                }
                else if (tool == Tool.Crayon)
                {
                    using (Pen pen = new Pen(brushColor, brushSize)) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round; DrawStroke(g, pen); }
                }
                else if (tool == Tool.Rectangle)
                {
                    using (Pen pen = new Pen(brushColor, brushSize))
                        g.DrawRectangle(pen, Math.Min(start.X, p.X), Math.Min(start.Y, p.Y), Math.Abs(p.X - start.X), Math.Abs(p.Y - start.Y));
                }
            }
        }
        stroke.Clear();
        canvas.Invalidate();
        UpdateInfo();
    }

    void DrawStroke(Graphics g, Pen pen)
    {
        if (stroke.Count == 1) { g.DrawRectangle(pen, stroke[0].X, stroke[0].Y, 1, 1); return; }
        for (int i = 1; i < stroke.Count; i++) g.DrawLine(pen, stroke[i - 1], stroke[i]);
    }

    void Flood(Bitmap b, int x0, int y0, Color newColor, int tol)
    {
        int w = b.Width, h = b.Height;
        Color target = b.GetPixel(x0, y0);
        bool[,] seen = new bool[w, h];
        Stack<Point> q = new Stack<Point>();
        q.Push(new Point(x0, y0));
        while (q.Count > 0)
        {
            Point p = q.Pop();
            if (p.X < 0 || p.Y < 0 || p.X >= w || p.Y >= h || seen[p.X, p.Y]) continue;
            seen[p.X, p.Y] = true;
            Color c = b.GetPixel(p.X, p.Y);
            int d = Math.Abs(c.R - target.R) + Math.Abs(c.G - target.G) + Math.Abs(c.B - target.B) + Math.Abs(c.A - target.A);
            if (d > tol) continue;
            b.SetPixel(p.X, p.Y, newColor);
            q.Push(new Point(p.X + 1, p.Y)); q.Push(new Point(p.X - 1, p.Y));
            q.Push(new Point(p.X, p.Y + 1)); q.Push(new Point(p.X, p.Y - 1));
        }
    }

    void Transparent_Click(object sender, EventArgs e)
    {
        if (frames.Count == 0) return;
        List<int> tg = Targets();
        PushUndo(tg);
        foreach (int i in tg)
        {
            Bitmap b = frames[i].Bmp;
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                {
                    Color c = b.GetPixel(x, y);
                    int d = Math.Abs(c.R - brushColor.R) + Math.Abs(c.G - brushColor.G) + Math.Abs(c.B - brushColor.B);
                    if (d <= tolerance) b.SetPixel(x, y, Color.FromArgb(0, c.R, c.G, c.B));
                }
        }
        canvas.Invalidate();
        UpdateInfo();
    }

    void Enregistrer_Click(object sender, EventArgs e)
    {
        if (frames.Count == 0) return;
        SaveFileDialog d = new SaveFileDialog();
        d.Filter = "GIF anime (*.gif)|*.gif";
        d.FileName = "gif_edite.gif";
        if (d.ShowDialog() != DialogResult.OK) return;
        try { SaveGif(frames, d.FileName); MessageBox.Show("GIF enregistre : " + d.FileName); }
        catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
    }

    void ExporterPng_Click(object sender, EventArgs e)
    {
        if (frames.Count == 0) return;
        FolderBrowserDialog d = new FolderBrowserDialog();
        if (d.ShowDialog() != DialogResult.OK) return;
        for (int i = 0; i < frames.Count; i++)
            frames[i].Bmp.Save(Path.Combine(d.SelectedPath, "frame_" + i.ToString("00") + ".png"), ImageFormat.Png);
        MessageBox.Show(frames.Count + " frames PNG exportees.");
    }

    static readonly Encoder FrameDelayEncoder =
        new Encoder(new Guid("6aedbd6d-3fb5-418a-83a6-7f45229dc872"));

    static ImageCodecInfo GifCodec()
    {
        foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
            if (c.FormatID == ImageFormat.Gif.Guid) return c;
        return null;
    }

    static void SaveGif(List<FrameData> frames, string path)
    {
        ImageCodecInfo codec = GifCodec();
        if (codec == null) throw new Exception("Encodeur GIF introuvable");
        EncoderParameters ep = new EncoderParameters(2);
        ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
        ep.Param[1] = new EncoderParameter(FrameDelayEncoder, 100L);
        frames[0].Bmp.Save(path, codec, ep);
        for (int i = 1; i < frames.Count; i++)
        {
            ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
            ep.Param[1] = new EncoderParameter(FrameDelayEncoder, (long)frames[i].Delay);
            frames[0].Bmp.SaveAdd(frames[i].Bmp, ep);
        }
        ep.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
        frames[0].Bmp.SaveAdd(ep);

        // L'encodeur GIF du framework n'ecrit pas les durees : on patch les blocs GCE.
        byte[] data = File.ReadAllBytes(path);
        int n = 0;
        for (int i = 0; i + 7 < data.Length; i++)
        {
            if (data[i] != 0x21 || data[i + 1] != 0xF9 || data[i + 2] != 0x04) continue;
            int jiffies = 1;
            if (n < frames.Count) jiffies = Math.Max(1, frames[n].Delay / 10);
            data[i + 4] = (byte)(jiffies & 0xFF);
            data[i + 5] = (byte)((jiffies >> 8) & 0xFF);
            n++;
            i += 7;
        }
        File.WriteAllBytes(path, data);
    }
}
