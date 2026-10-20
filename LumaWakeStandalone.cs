using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LumaWakeStandalone
{
    [DataContract]
    public class Device : INotifyPropertyChanged
    {
        private string state = "检测中";
        [DataMember] public string Name { get; set; }
        [DataMember] public string Mac { get; set; }
        [DataMember] public string Ip { get; set; }
        [DataMember] public string Group { get; set; }
        public string State { get { return state; } set { state = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("State")); } }
        public event PropertyChangedEventHandler PropertyChanged;
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        readonly List<Device> devices = new List<Device>();
        readonly FlowLayoutPanel cards = new FlowLayoutPanel();
        readonly TextBox search = new TextBox();
        readonly Label count = new Label();
        readonly Label empty = new Label();
        readonly Label footer = new Label();
        readonly Timer timer = new Timer();
        readonly string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LumaWake", "devices.json");
        bool refreshRunning;
        Color bg = Color.FromArgb(244, 245, 247), ink = Color.FromArgb(32, 35, 42), muted = Color.FromArgb(139, 144, 153), blue = Color.FromArgb(57, 120, 246);

        public MainForm()
        {
            Text = "LumaWake"; MinimumSize = new Size(960, 680); Size = new Size(1120, 780); StartPosition = FormStartPosition.CenterScreen; BackColor = bg; Font = new Font("Segoe UI", 9F); AutoScaleMode = AutoScaleMode.Dpi; DoubleBuffered = true;
            BuildUi(); LoadDevices(); RenderDevices();
            timer.Interval = 5000; timer.Tick += async delegate { await RefreshAsync(); }; timer.Start();
            Shown += async delegate { await RefreshAsync(); };
        }

        void BuildUi()
        {
            BackColor = Color.FromArgb(247, 248, 250);
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = BackColor };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(root);
            Panel sidebar = BuildSidebar(); root.Controls.Add(sidebar, 0, 0);

            Panel main = new Panel { Dock = DockStyle.Fill, BackColor = BackColor, Padding = new Padding(36, 28, 36, 22) }; root.Controls.Add(main, 1, 0);
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = main.BackColor, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); main.Controls.Add(layout);

            TableLayoutPanel heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = main.BackColor, Margin = Padding.Empty };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            heading.Controls.Add(new Label { Text = "设备中心", Font = new Font("Segoe UI", 23F, FontStyle.Bold), ForeColor = ink, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            Button add = Button("＋  添加设备", blue, Color.White, 132, 40); add.Anchor = AnchorStyles.Right; add.Click += delegate { EditDevice(null); }; heading.Controls.Add(add, 1, 0); layout.Controls.Add(heading, 0, 0);

            GradientPanel banner = new GradientPanel { Dock = DockStyle.Fill, Radius = 15, StartColor = Color.FromArgb(29, 48, 91), EndColor = Color.FromArgb(65, 112, 213), Margin = new Padding(0, 3, 0, 7) };
            TableLayoutPanel bannerContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent, Padding = new Padding(20, 13, 20, 11) };
            bannerContent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48)); bannerContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bannerContent.Controls.Add(new Label { Text = "✦", Font = new Font("Segoe UI Symbol", 24F), ForeColor = Color.FromArgb(188, 211, 255), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
            TableLayoutPanel bannerCopy = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
            bannerCopy.RowStyles.Add(new RowStyle(SizeType.Percent, 56)); bannerCopy.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            bannerCopy.Controls.Add(new Label { Text = "一键唤醒，随时连接", Font = new Font("Segoe UI", 13F, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            bannerCopy.Controls.Add(new Label { Text = "开启设备的 BIOS / 网卡 WOL 设置，即可从局域网唤醒。", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(215, 226, 250), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            bannerContent.Controls.Add(bannerCopy, 1, 0); banner.Controls.Add(bannerContent); layout.Controls.Add(banner, 0, 1);

            TableLayoutPanel section = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = main.BackColor, Margin = Padding.Empty };
            section.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); section.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236));
            section.Controls.Add(new Label { Text = "我的设备", Font = new Font("Segoe UI", 12F, FontStyle.Bold), ForeColor = ink, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoSize = true }, 0, 0);
            count.Text = "0 台"; count.ForeColor = muted; count.Font = new Font("Segoe UI", 8.5F); count.Dock = DockStyle.Fill; count.TextAlign = ContentAlignment.MiddleLeft; count.Margin = new Padding(10, 0, 0, 0); section.Controls.Add(count, 1, 0);
            RoundedPanel searchBox = BuildSearchBox(); section.Controls.Add(searchBox, 2, 0); layout.Controls.Add(section, 0, 2);

            Panel listHost = new Panel { Dock = DockStyle.Fill, BackColor = main.BackColor, Margin = Padding.Empty };
            cards.Dock = DockStyle.Fill; cards.FlowDirection = FlowDirection.TopDown; cards.WrapContents = false; cards.AutoScroll = true; cards.BackColor = main.BackColor; cards.Padding = new Padding(0, 3, 10, 5); listHost.Controls.Add(cards);
            empty.Dock = DockStyle.Fill; empty.TextAlign = ContentAlignment.MiddleCenter; empty.BackColor = main.BackColor; empty.ForeColor = muted; empty.Font = new Font("Segoe UI", 10F); listHost.Controls.Add(empty); empty.BringToFront();
            cards.ClientSizeChanged += delegate { ResizeCardWidths(); }; layout.Controls.Add(listHost, 0, 3);

            TableLayoutPanel bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = main.BackColor, Margin = Padding.Empty };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.Text = "状态每 5 秒自动检测 · 在线状态通过 Ping 判断"; footer.ForeColor = muted; footer.Font = new Font("Segoe UI", 8F); footer.Dock = DockStyle.Fill; footer.TextAlign = ContentAlignment.MiddleLeft; bottom.Controls.Add(footer, 0, 0);
            Button refresh = Button("↻  立即刷新", main.BackColor, blue, 105, 28); refresh.Anchor = AnchorStyles.Right; refresh.Click += async delegate { await RefreshAsync(); }; bottom.Controls.Add(refresh, 1, 0); layout.Controls.Add(bottom, 0, 4);
        }

        Panel BuildSidebar()
        {
            Panel sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(242, 244, 247), Padding = new Padding(20, 25, 18, 18) };
            RoundedPanel logo = new RoundedPanel { Location = new Point(1, 0), Size = new Size(36, 36), Radius = 11, BackColor = blue };
            logo.Controls.Add(new Label { Text = "◉", Font = new Font("Segoe UI", 19F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(5, 1) }); sidebar.Controls.Add(logo);
            sidebar.Controls.Add(new Label { Text = "LumaWake", Font = new Font("Segoe UI", 16F, FontStyle.Bold), ForeColor = ink, Location = new Point(46, 6), AutoSize = true });
            sidebar.Controls.Add(new Label { Text = "设备管理", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = muted, Location = new Point(4, 66), AutoSize = true });
            RoundedPanel nav = new RoundedPanel { Location = new Point(0, 89), Size = new Size(186, 44), Radius = 10, BackColor = Color.FromArgb(228, 236, 253) }; sidebar.Controls.Add(nav);
            nav.Controls.Add(new Label { Text = "▦", Font = new Font("Segoe UI", 12F), ForeColor = blue, Location = new Point(14, 10), AutoSize = true });
            nav.Controls.Add(new Label { Text = "我的设备", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = blue, Location = new Point(41, 13), AutoSize = true });
            sidebar.Controls.Add(new Panel { Location = new Point(0, 153), Size = new Size(186, 1), BackColor = Color.FromArgb(224, 227, 232) });
            sidebar.Controls.Add(new Label { Text = "网络状态", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = muted, Location = new Point(4, 174), AutoSize = true });
            RoundedPanel network = new RoundedPanel { Location = new Point(0, 198), Size = new Size(186, 68), Radius = 11, BackColor = Color.White, BorderColor = Color.FromArgb(231, 233, 237) }; sidebar.Controls.Add(network);
            network.Controls.Add(new Label { Text = "●", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(40, 168, 121), Location = new Point(13, 15), AutoSize = true });
            network.Controls.Add(new Label { Text = "本地网络已就绪", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = ink, Location = new Point(31, 14), AutoSize = true });
            network.Controls.Add(new Label { Text = "Wake-on-LAN · UDP 9", Font = new Font("Segoe UI", 7.5F), ForeColor = muted, Location = new Point(31, 38), AutoSize = true });
            sidebar.Controls.Add(new Label { Text = "LUMAWAKE  ·  WINDOWS", Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.FromArgb(164, 169, 178), Location = new Point(4, 0), AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Bottom });
            sidebar.Resize += delegate { Control version = sidebar.Controls[sidebar.Controls.Count - 1]; version.Top = sidebar.ClientSize.Height - version.Height - 2; };
            return sidebar;
        }

        RoundedPanel BuildSearchBox()
        {
            RoundedPanel box = new RoundedPanel { Dock = DockStyle.Fill, Radius = 10, BackColor = Color.White, BorderColor = Color.FromArgb(228, 231, 235), Margin = new Padding(0, 11, 0, 11) };
            Label glyph = new Label { Text = "⌕", Font = new Font("Segoe UI", 16F), ForeColor = muted, Location = new Point(10, 1), AutoSize = true }; box.Controls.Add(glyph);
            Label hint = new Label { Text = "搜索名称、IP 或 MAC", Location = new Point(38, 8), AutoSize = true, ForeColor = Color.FromArgb(160, 164, 171), Font = new Font("Segoe UI", 8.5F) };
            search.Location = new Point(37, 7); search.Width = 175; search.Height = 22; search.BorderStyle = BorderStyle.None; search.Font = new Font("Segoe UI", 9F); search.BackColor = Color.White; search.ForeColor = ink;
            search.TextChanged += delegate { hint.Visible = search.Text.Length == 0; RenderDevices(); }; box.Controls.Add(search); box.Controls.Add(hint); hint.BringToFront(); hint.Click += delegate { search.Focus(); };
            return box;
        }

        void ResizeCardWidths()
        {
            int width = Math.Max(520, cards.ClientSize.Width - 22);
            foreach (Control card in cards.Controls) card.Width = width;
        }

        Button Button(string text, Color back, Color fore, int width, int height)
        {
            Button b = new Button { Text = text, Width = width, Height = height, FlatStyle = FlatStyle.Flat, BackColor = back, ForeColor = fore, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = back == blue ? Color.FromArgb(45, 104, 220) : Color.FromArgb(236, 240, 247);
            b.Region = new Region(RoundedPanel.RoundedRectangle(new Rectangle(0, 0, width, height), 8));
            b.Resize += delegate { b.Region = new Region(RoundedPanel.RoundedRectangle(new Rectangle(0, 0, b.Width, b.Height), 8)); };
            return b;
        }

        void LoadDevices()
        {
            try { if (File.Exists(dataPath)) { DataContractJsonSerializer s = new DataContractJsonSerializer(typeof(List<Device>)); using (FileStream f = File.OpenRead(dataPath)) devices.AddRange((List<Device>)s.ReadObject(f)); } }
            catch (Exception ex) { MessageBox.Show("读取设备列表失败：" + ex.Message, "LumaWake", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        void SaveDevices()
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(dataPath)); DataContractJsonSerializer s = new DataContractJsonSerializer(typeof(List<Device>)); using (FileStream f = File.Create(dataPath)) s.WriteObject(f, devices); }
            catch (Exception ex) { MessageBox.Show("保存设备列表失败：" + ex.Message, "LumaWake", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void RenderDevices()
        {
            if (cards == null || search == null) return;
            cards.SuspendLayout(); cards.Controls.Clear();
            List<Device> shown = devices.Where(d => (d.Name + " " + d.Ip + " " + d.Mac + " " + d.Group).IndexOf(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            count.Text = shown.Count + " 台";
            empty.Text = devices.Count == 0 ? "还没有设备\r\n点击「添加设备」录入第一台局域网电脑" : "没有找到匹配设备\r\n试试设备名称、IP 地址或 MAC 地址";
            empty.Visible = shown.Count == 0;
            foreach (Device d in shown) { Control card = MakeCard(d); card.Width = Math.Max(520, cards.ClientSize.Width - 22); cards.Controls.Add(card); }
            cards.ResumeLayout();
        }

        Control MakeCard(Device d)
        {
            RoundedPanel p = new RoundedPanel { Width = Math.Max(520, cards.ClientSize.Width - 22), Height = 116, Radius = 13, BackColor = Color.White, BorderColor = Color.FromArgb(231, 233, 237), Margin = new Padding(1, 0, 1, 12), Padding = new Padding(13) };
            TableLayoutPanel content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = Padding.Empty };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); content.RowStyles.Add(new RowStyle(SizeType.Absolute, 1)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); p.Controls.Add(content);
            Color iconColor = d.State == "在线" ? Color.FromArgb(230, 247, 240) : Color.FromArgb(237, 242, 255);
            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            RoundedPanel icon = new RoundedPanel { Size = new Size(44, 44), Anchor = AnchorStyles.Left, Radius = 12, BackColor = iconColor };
            icon.Controls.Add(new Label { Text = "▣", Font = new Font("Segoe UI Symbol", 20F), ForeColor = d.State == "在线" ? Color.FromArgb(37, 159, 113) : blue, AutoSize = true, Location = new Point(8, 5) }); top.Controls.Add(icon, 0, 0);
            TableLayoutPanel description = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = new Padding(5, 7, 6, 5) };
            description.RowStyles.Add(new RowStyle(SizeType.Percent, 52)); description.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            description.Controls.Add(new Label { Text = d.Name, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = ink, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.BottomLeft }, 0, 0);
            description.Controls.Add(new Label { Text = d.Group + "  ·  " + d.Ip, Font = new Font("Segoe UI", 8.5F), ForeColor = muted, Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.TopLeft }, 0, 1); top.Controls.Add(description, 1, 0);
            Color stateColor = d.State == "在线" ? Color.FromArgb(40, 168, 121) : d.State == "离线" ? Color.FromArgb(223, 97, 103) : d.State == "正在唤醒" ? blue : muted;
            Color stateBg = d.State == "在线" ? Color.FromArgb(232, 247, 240) : d.State == "离线" ? Color.FromArgb(253, 238, 239) : Color.FromArgb(238, 243, 255);
            RoundedPanel badge = new RoundedPanel { Size = new Size(83, 27), Radius = 13, BackColor = stateBg, Anchor = AnchorStyles.None };
            badge.Controls.Add(new Label { Text = "●  " + d.State, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = stateColor, AutoSize = true, Location = new Point(10, 6) }); top.Controls.Add(badge, 2, 0);
            Button wake = Button("唤醒设备", blue, Color.White, 100, 34); wake.Anchor = AnchorStyles.Right; wake.Click += async delegate { await WakeAsync(d); }; top.Controls.Add(wake, 3, 0);
            content.Controls.Add(top, 0, 0);
            content.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(239, 241, 245), Margin = new Padding(1, 0, 1, 0) }, 0, 1);
            TableLayoutPanel metadata = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty };
            metadata.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            metadata.Controls.Add(MetadataLabel("MAC", d.Mac), 0, 0); metadata.Controls.Add(MetadataLabel("IP", d.Ip), 1, 0);
            Button more = Button("···", Color.White, muted, 30, 26); more.Anchor = AnchorStyles.Right; more.Click += delegate { ShowMenu(more, d); }; metadata.Controls.Add(more, 3, 0);
            content.Controls.Add(metadata, 0, 2);
            return p;
        }

        Control MetadataLabel(string key, string value)
        {
            TableLayoutPanel item = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = new Padding(3, 0, 22, 0) };
            item.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); item.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            item.Controls.Add(new Label { Text = key, ForeColor = Color.FromArgb(151, 156, 165), Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            item.Controls.Add(new Label { Text = value, ForeColor = Color.FromArgb(80, 86, 97), Font = new Font("Segoe UI", 8F), AutoSize = true, Margin = new Padding(7, 0, 0, 0), Anchor = AnchorStyles.Left }, 1, 0);
            return item;
        }

        void ShowMenu(Control owner, Device d)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("编辑设备", null, delegate { EditDevice(d); });
            menu.Items.Add("删除设备", null, delegate { if (MessageBox.Show("确定移除“" + d.Name + "”吗？", "删除设备", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { devices.Remove(d); SaveDevices(); RenderDevices(); } });
            menu.Show(owner, new Point(0, owner.Height));
        }

        void EditDevice(Device d)
        {
            Form f = new Form { Text = d == null ? "添加设备" : "编辑设备", ClientSize = new Size(430, 458), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, BackColor = Color.White, Font = new Font("Segoe UI", 9F), AutoScaleMode = AutoScaleMode.Dpi };
            TableLayoutPanel form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(25, 21, 25, 20), BackColor = Color.White };
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); for (int i = 0; i < 4; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 72)); form.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); f.Controls.Add(form);
            form.Controls.Add(new Label { Text = d == null ? "添加设备" : "编辑设备", Font = new Font("Segoe UI", 17F, FontStyle.Bold), ForeColor = ink, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            string[] labels = { "设备名称", "MAC 地址", "IP 地址", "分组" }; string[] values = d == null ? new string[4] : new[] { d.Name, d.Mac, d.Ip, d.Group }; TextBox[] fields = new TextBox[4];
            for (int i = 0; i < 4; i++)
            {
                Panel field = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };
                field.Controls.Add(new Label { Text = labels[i], Location = new Point(0, 0), AutoSize = true, ForeColor = Color.FromArgb(91, 97, 108), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) });
                fields[i] = new TextBox { Text = values[i], Dock = DockStyle.Bottom, Height = 35, Font = new Font("Segoe UI", 9.5F), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(248, 249, 251) };
                field.Controls.Add(fields[i]); form.Controls.Add(field, 0, i + 1);
            }
            Label error = new Label { ForeColor = Color.Firebrick, Font = new Font("Segoe UI", 8F), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }; form.Controls.Add(error, 0, 5);
            Button save = Button("保存设备", blue, Color.White, 100, 38); save.Dock = DockStyle.Fill; save.Click += delegate { string mac = Regex.Replace(fields[1].Text, "[^0-9A-Fa-f]", ""); IPAddress address; if (String.IsNullOrWhiteSpace(fields[0].Text) || mac.Length != 12 || !IPAddress.TryParse(fields[2].Text.Trim(), out address)) { error.Text = "请填写名称、有效 MAC 地址和 IP 地址。"; return; }
                mac = String.Join(":", Enumerable.Range(0, 6).Select(i => mac.Substring(i * 2, 2))).ToUpperInvariant();
                Device result = d ?? new Device(); result.Name = fields[0].Text.Trim(); result.Mac = mac; result.Ip = fields[2].Text.Trim(); result.Group = String.IsNullOrWhiteSpace(fields[3].Text) ? "未分组" : fields[3].Text.Trim();
                if (d == null) devices.Add(result); SaveDevices(); f.DialogResult = DialogResult.OK; f.Close(); };
            form.Controls.Add(save, 0, 6); f.AcceptButton = save; f.ShowDialog(this); RenderDevices();
        }

        async Task WakeAsync(Device d)
        {
            d.State = "正在唤醒"; RenderDevices();
            try { byte[] packet = MagicPacket(d.Mac); await Task.Run(() => { using (UdpClient udp = new UdpClient()) { udp.EnableBroadcast = true; udp.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9)); } }); await Task.Delay(1500); await ProbeAsync(d); }
            catch (Exception ex) { d.State = "未知"; RenderDevices(); MessageBox.Show("发送唤醒包失败：" + ex.Message, "LumaWake", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        static byte[] MagicPacket(string mac)
        {
            string raw = Regex.Replace(mac, "[^0-9A-Fa-f]", ""); if (raw.Length != 12) throw new FormatException("MAC 地址格式无效。");
            byte[] address = Enumerable.Range(0, 6).Select(i => Convert.ToByte(raw.Substring(i * 2, 2), 16)).ToArray(); byte[] packet = new byte[102];
            for (int i = 0; i < 6; i++) packet[i] = 255;
            for (int i = 6; i < packet.Length; i += 6) Buffer.BlockCopy(address, 0, packet, i, 6);
            return packet;
        }

        async Task RefreshAsync()
        {
            if (refreshRunning || IsDisposed) return; refreshRunning = true;
            try { await Task.WhenAll(devices.Select(ProbeAsync)); }
            finally { refreshRunning = false; }
        }

        async Task ProbeAsync(Device d)
        {
            try { using (Ping ping = new Ping()) { PingReply reply = await ping.SendPingAsync(d.Ip, 1200); d.State = reply.Status == IPStatus.Success ? "在线" : "离线"; } }
            catch { d.State = "离线"; }
            if (!IsDisposed) BeginInvoke((Action)RenderDevices);
        }
    }

    public class RoundedPanel : Panel
    {
        public int Radius { get; set; }
        public Color BorderColor { get; set; }
        public RoundedPanel() { Radius = 10; BorderColor = Color.Transparent; DoubleBuffered = true; }
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width > 0 && Height > 0) Region = new Region(RoundedRectangle(new Rectangle(0, 0, Width, Height), Radius));
            Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(BackColor)) e.Graphics.FillPath(brush, RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius));
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (BorderColor != Color.Transparent)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(BorderColor)) e.Graphics.DrawPath(pen, RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius));
            }
        }
        public static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath(); int diameter = Math.Max(2, radius * 2);
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90); path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90); path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90); path.CloseFigure(); return path;
        }
    }

    public class GradientPanel : RoundedPanel
    {
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public GradientPanel() { StartColor = Color.FromArgb(31, 53, 102); EndColor = Color.FromArgb(62, 111, 218); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, StartColor, EndColor, LinearGradientMode.Horizontal))
                e.Graphics.FillPath(brush, RoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), Radius));
        }
    }
}
