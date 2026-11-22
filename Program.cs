using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace QuickShelf;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, @"Local\QuickShelf.DesktopLauncher", out bool first);
        if (!first) { MessageBox.Show("Quick Shelf is already running. Click its icon beside the clock.", "Quick Shelf"); return; }
        try { using var context = new ShelfContext(); Application.Run(context); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Quick Shelf", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
public sealed class ShelfItem
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public ShelfItem Copy() => new() { Name = Name, Target = Target };
}
public sealed class ShelfData
{
    public List<ShelfItem> Items { get; set; } = new();
}
internal sealed class ShelfStore
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShelf");
    public static readonly string FilePath = Path.Combine(Folder, "shortcuts.json");
    public List<ShelfItem> Items { get; private set; } = new();
    public bool CanWrite { get; private set; } = true;
    public string? Warning { get; private set; }
    public ShelfStore()
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            var saved = JsonSerializer.Deserialize<ShelfData>(File.ReadAllText(FilePath)) ?? throw new IOException("The saved file is empty.");
            Items = (saved.Items ?? new()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Target)).ToList();
            foreach (var item in Items) if (string.IsNullOrWhiteSpace(item.Name)) item.Name = item.Target;
        }
        catch (Exception ex)
        {
            CanWrite = false;
            Warning = "Quick Shelf could not read its saved shortcuts. To avoid overwriting them, changes are disabled.\n\n" + FilePath + "\n\n" + ex.Message + "\n\nExit Quick Shelf and restore or rename that file before trying again.";
        }
    }
    public bool Commit(List<ShelfItem> proposed, IWin32Window owner)
    {
        if (!CanWrite) { MessageBox.Show(owner, Warning, "Saved shortcuts unavailable"); return false; }
        string temp = Path.Combine(Folder, "shortcuts-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(Folder);
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new ShelfData { Items = proposed }, new JsonSerializerOptions { WriteIndented = true }));
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath);
            Items = proposed;
            return true;
        }
        catch (Exception ex) { MessageBox.Show(owner, "Nothing was changed because the shortcuts could not be saved.\n\n" + ex.Message, "Quick Shelf", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
    }
}
internal static class Targets
{
    public static bool Web(string text, out Uri? uri)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
    public static string Normalize(string value)
    {
        value = value.Trim().Trim('"');
        if (Web(value, out var uri)) return uri!.AbsoluteUri;
        value = Environment.ExpandEnvironmentVariables(value);
        if (!Path.IsPathFullyQualified(value)) throw new IOException("Choose a file/folder, or enter a full path or an https:// website address.");
        value = Path.GetFullPath(value);
        if (!File.Exists(value) && !Directory.Exists(value)) throw new IOException("That file or folder does not exist. Choose its current location.");
        return value;
    }
    public static string SuggestedName(string value)
    {
        if (Web(value, out var uri)) return uri!.Host;
        string name = Path.GetFileName(value.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(name)) return value;
        return Directory.Exists(value) ? name : Path.GetFileNameWithoutExtension(name);
    }
}
internal sealed class ItemEditor : Form
{
    readonly TextBox name = new() { Dock = DockStyle.Fill, MaxLength = 100 };
    readonly TextBox target = new() { Dock = DockStyle.Fill };
    public ShelfItem Result { get; private set; } = new();
    public ItemEditor(Icon icon, ShelfItem? original = null, string? initial = null)
    {
        Text = original == null ? "Add to Quick Shelf" : "Edit shortcut";
        Icon = icon; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false; MaximizeBox = false; ClientSize = new Size(550, 295);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 37));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label { Text = "Name shown on your shelf", AutoSize = true }, 0, 0); layout.Controls.Add(name, 0, 1);
        layout.Controls.Add(new Label { Text = "File/folder path or website address", AutoSize = true }, 0, 2); layout.Controls.Add(target, 0, 3);
        var browse = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var file = new Button { Text = "Choose file...", AutoSize = true };
        var folder = new Button { Text = "Choose folder...", AutoSize = true };
        file.Click += (_, _) => { using var d = new OpenFileDialog { Filter = "All files (*.*)|*.*", DereferenceLinks = false }; if (d.ShowDialog(this) == DialogResult.OK) SetTarget(d.FileName); };
        folder.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) SetTarget(d.SelectedPath); };
        browse.Controls.AddRange(new Control[] { file, folder }); layout.Controls.Add(browse, 0, 4);
        layout.Controls.Add(new Label { Text = "Adding does not open or move the original. Websites must start with http:// or https://. Add only items you trust.", Dock = DockStyle.Fill }, 0, 5);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var save = new Button { Text = "Save", AutoSize = true };
        save.Click += (_, _) =>
        {
            try
            {
                string n = name.Text.Trim();
                if (n.Length == 0 || n.Any(char.IsControl)) throw new IOException("Enter a name without line breaks or other control characters.");
                Result = new ShelfItem { Name = n, Target = Targets.Normalize(target.Text) };
                DialogResult = DialogResult.OK; Close();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Check shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        buttons.Controls.AddRange(new Control[] { cancel, save }); layout.Controls.Add(buttons, 0, 6);
        Controls.Add(layout); AcceptButton = save; CancelButton = cancel;
        if (original != null) { name.Text = original.Name; target.Text = original.Target; }
        else if (initial != null) SetTarget(initial);
        Shown += (_, _) => { name.Focus(); name.SelectAll(); };
    }
    void SetTarget(string value) { target.Text = value; if (string.IsNullOrWhiteSpace(name.Text)) name.Text = Targets.SuggestedName(value); }
}
internal sealed class ShelfForm : Form
{
    readonly ShelfStore store;
    readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HeaderStyle = ColumnHeaderStyle.None, HideSelection = false, AllowDrop = true, ShowItemToolTips = true, Activation = ItemActivation.OneClick };
    readonly TextBox search = new() { Dock = DockStyle.Fill, PlaceholderText = "Search your shortcuts..." };
    readonly Label hint = new() { Dock = DockStyle.Fill, Text = "Drag files/folders here. Click a shortcut to open it.", AutoEllipsis = true };
    readonly ContextMenuStrip itemMenu = new();
    readonly Button add = new() { Text = "+ Add", AutoSize = true };
    int hold;
    bool quitting;
    public ShelfForm(ShelfStore store, Icon icon)
    {
        this.store = store; Icon = icon;
        Text = "Quick Shelf"; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.SizableToolWindow; ClientSize = new Size(410, 435); MinimumSize = new Size(320, 300);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 34, 37 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 41)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
        root.Controls.Add(new Label { Text = "Quick Shelf", Font = new Font("Segoe UI", 14, FontStyle.Bold), AutoSize = true }, 0, 0);
        root.Controls.Add(search, 0, 1); root.Controls.Add(list, 0, 2);
        list.Columns.Add("Shortcut", 340);
        list.Resize += (_, _) => list.Columns[0].Width = Math.Max(80, list.ClientSize.Width - 24);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
        var edit = new Button { Text = "Edit", AutoSize = true }; var remove = new Button { Text = "Remove", AutoSize = true };
        add.Click += (_, _) => AddItem(); edit.Click += (_, _) => EditItem(); remove.Click += (_, _) => RemoveItem();
        buttons.Controls.AddRange(new Control[] { add, edit, remove }); root.Controls.Add(buttons, 0, 3); root.Controls.Add(hint, 0, 4); Controls.Add(root);
        add.Enabled = edit.Enabled = remove.Enabled = store.CanWrite;
        itemMenu.Items.Add("Open", null, (_, _) => OpenSelected());
        itemMenu.Items.Add("Edit / rename", null, (_, _) => EditItem());
        itemMenu.Items.Add("Move up", null, (_, _) => MoveSelected(-1));
        itemMenu.Items.Add("Move down", null, (_, _) => MoveSelected(1));
        itemMenu.Items.Add(new ToolStripSeparator()); itemMenu.Items.Add("Remove from shelf", null, (_, _) => RemoveItem());
        itemMenu.Opening += (_, e) => { if (Selected == null) { e.Cancel = true; return; } hold++; };
        itemMenu.Closed += (_, _) => hold = Math.Max(0, hold - 1);
        list.ContextMenuStrip = itemMenu;
        list.MouseDown += (_, e) => { if (e.Button == MouseButtons.Right) { var hit = list.GetItemAt(e.X, e.Y); if (hit != null) { list.SelectedItems.Clear(); hit.Selected = true; } else list.SelectedItems.Clear(); } };
        list.ItemActivate += (_, _) => OpenSelected();
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) { e.SuppressKeyPress = true; RemoveItem(); } else if (e.KeyCode == Keys.F2) { e.SuppressKeyPress = true; EditItem(); } };
        search.TextChanged += (_, _) => RefreshItems();
        AllowDrop = true;
        foreach (Control control in new Control[] { this, list })
        {
            control.DragEnter += (_, e) => e.Effect = store.CanWrite && e.Data != null && (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText) || e.Data.GetDataPresent(DataFormats.Text)) ? DragDropEffects.Copy : DragDropEffects.None;
            control.DragDrop += (_, e) => HandleDrop(e);
        }
        Deactivate += (_, _) => { if (hold == 0 && !itemMenu.Visible) Hide(); };
        FormClosing += (_, e) => { if (!quitting) { e.Cancel = true; Hide(); } };
        KeyPreview = true; KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) { Hide(); e.Handled = true; } };
        RefreshItems();
    }
    ShelfItem? Selected => list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as ShelfItem : null;
    void RefreshItems(ShelfItem? select = null)
    {
        list.BeginUpdate(); list.Items.Clear();
        foreach (var item in store.Items)
        {
            if (!item.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) && !item.Target.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            string category = Targets.Web(item.Target, out _) ? "Website" : Directory.Exists(item.Target) ? "Folder" : "File / app";
            var row = new ListViewItem(item.Name) { Tag = item, ToolTipText = category + "\n" + item.Target };
            list.Items.Add(row); if (ReferenceEquals(item, select)) row.Selected = true;
        }
        list.EndUpdate();
        hint.Text = store.Items.Count == 0 ? "Empty shelf. Use + Add or drag files/folders here." : $"{store.Items.Count} saved. Right-click an item for options.";
    }
    public void ShowAt(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(anchor.X - Width + 22, area.Left, Math.Max(area.Left, area.Right - Width)), Math.Clamp(anchor.Y - Height - 12, area.Top, Math.Max(area.Top, area.Bottom - Height)));
        RefreshItems(); Show(); Activate(); search.Focus();
    }
    public void Toggle(Point anchor) { if (Visible) Hide(); else ShowAt(anchor); }
    void AddItem(string? initial = null)
    {
        if (!store.CanWrite) return;
        hold++;
        try
        {
            using var dialog = new ItemEditor(Icon!, initial: initial);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var proposed = new List<ShelfItem>(store.Items) { dialog.Result };
            if (store.Commit(proposed, this)) RefreshItems(dialog.Result);
        }
        finally { hold--; if (Visible) Activate(); }
    }
    void EditItem()
    {
        var selected = Selected; if (selected == null || !store.CanWrite) return;
        hold++;
        try
        {
            using var dialog = new ItemEditor(Icon!, selected.Copy());
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var proposed = new List<ShelfItem>(store.Items); proposed[proposed.IndexOf(selected)] = dialog.Result;
            if (store.Commit(proposed, this)) RefreshItems(dialog.Result);
        }
        finally { hold--; if (Visible) Activate(); }
    }
    void RemoveItem()
    {
        var selected = Selected; if (selected == null || !store.CanWrite) return;
        hold++;
        try
        {
            if (MessageBox.Show(this, $"Remove '{selected.Name}' from Quick Shelf?\n\nThe original file or folder will NOT be deleted.", "Remove shortcut", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            var proposed = new List<ShelfItem>(store.Items); proposed.Remove(selected);
            if (store.Commit(proposed, this)) RefreshItems();
        }
        finally { hold--; if (Visible) Activate(); }
    }
    void MoveSelected(int direction)
    {
        var selected = Selected; if (selected == null || !store.CanWrite) return;
        var proposed = new List<ShelfItem>(store.Items); int index = proposed.IndexOf(selected), next = index + direction;
        if (next < 0 || next >= proposed.Count) return;
        (proposed[index], proposed[next]) = (proposed[next], proposed[index]);
        if (store.Commit(proposed, this)) RefreshItems(selected);
    }
    void OpenSelected()
    {
        var item = Selected; if (item == null) return;
        hold++;
        try
        {
            string target = Targets.Normalize(item.Target);
            var info = new ProcessStartInfo(target) { UseShellExecute = true };
            if (File.Exists(target)) info.WorkingDirectory = Path.GetDirectoryName(target)!;
            Process.Start(info); Hide();
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not open this shortcut. If the original moved, edit its path.\n\n" + ex.Message, "Quick Shelf", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { hold--; }
    }
    void HandleDrop(DragEventArgs e)
    {
        if (!store.CanWrite || e.Data == null) return;
        hold++;
        try
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                if (files.Length == 1) { AddItem(files[0]); return; }
                if (MessageBox.Show(this, $"Add shortcuts for {files.Length} items?\n\nNothing will be opened or moved.", "Quick Shelf", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                var proposed = new List<ShelfItem>(store.Items);
                foreach (string path in files) { string target = Targets.Normalize(path); proposed.Add(new ShelfItem { Name = Targets.SuggestedName(target), Target = target }); }
                if (store.Commit(proposed, this)) RefreshItems();
            }
            else
            {
                string text = (e.Data.GetData(DataFormats.UnicodeText) as string ?? e.Data.GetData(DataFormats.Text) as string ?? "").Trim();
                if (Targets.Web(text, out _)) AddItem(text);
                else MessageBox.Show(this, "Drop a file/folder or a complete http:// or https:// address. You can also use + Add.", "Quick Shelf");
            }
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not add these items.\n\n" + ex.Message, "Quick Shelf"); }
        finally { hold--; if (Visible) Activate(); }
    }
    public void Quit() { quitting = true; Close(); }
    protected override void Dispose(bool disposing) { if (disposing) itemMenu.Dispose(); base.Dispose(disposing); }
}
internal sealed class ShelfContext : ApplicationContext
{
    readonly ShelfStore store = new();
    readonly Icon icon;
    readonly ShelfForm shelf;
    readonly NotifyIcon tray;
    readonly ContextMenuStrip menu = new();
    public ShelfContext()
    {
        string exe = Environment.ProcessPath ?? throw new IOException("Cannot locate Quick Shelf.");
        icon = Icon.ExtractAssociatedIcon(exe) ?? (Icon)SystemIcons.Application.Clone();
        shelf = new ShelfForm(store, icon);
        menu.Items.Add("Open Quick Shelf", null, (_, _) => shelf.ShowAt(Cursor.Position));
        menu.Items.Add("Open saved-data folder", null, (_, _) =>
        {
            try { Directory.CreateDirectory(ShelfStore.Folder); Process.Start(new ProcessStartInfo(ShelfStore.Folder) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Quick Shelf"); }
        });
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("Exit Quick Shelf", null, (_, _) => ExitThread());
        tray = new NotifyIcon { Icon = icon, Text = "Quick Shelf - click for shortcuts", Visible = true, ContextMenuStrip = menu };
        tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) shelf.Toggle(Cursor.Position); };
        if (store.Warning != null) MessageBox.Show(store.Warning, "Quick Shelf", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        shelf.ShowAt(Cursor.Position);
    }
    protected override void ExitThreadCore() { tray.Visible = false; shelf.Quit(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tray.Visible = false; tray.Dispose(); menu.Dispose(); shelf.Dispose(); icon.Dispose(); }
        base.Dispose(disposing);
    }
}
