using System;
using System.Diagnostics;
using System.Drawing;
using System.Resources;
using System.Windows.Forms;

namespace Ruler
{
	sealed public class MainForm : Form, IRulerInfo
	{
		private const double PixelsPerMillimeter = 96/25.4;

		#region ResizeRegion enum

		private enum ResizeRegion
		{
			None, N, NE, E, SE, S, SW, W, NW
		}

		#endregion ResizeRegion enum

		private ToolTip _toolTip = new ToolTip();
		private Point _offset;
		private Rectangle _mouseDownRect;
		private int _resizeBorderWidth = 5;
		private Point _mouseDownPoint;
		private ResizeRegion _resizeRegion = ResizeRegion.None;
		private ContextMenu _menu = new ContextMenu();
		private MenuItem _verticalMenuItem;
		private MenuItem _toolTipMenuItem;
		private MenuItem _lockedMenuItem;

		public MainForm()
		{
			RulerInfo rulerInfo = RulerInfo.GetDefaultRulerInfo();

			this.Init(rulerInfo);
		}

		public MainForm(RulerInfo rulerInfo)
		{
			this.Init(rulerInfo);
		}

		public bool IsVertical
		{
			get { return this._verticalMenuItem.Checked; }
			set { this._verticalMenuItem.Checked = value; }
		}

		public bool IsLocked
		{
			get;
			set;
		}

		public bool ShowToolTip
		{
			get
			{
				return this._toolTipMenuItem.Checked;
			}
			set
			{
				this._toolTipMenuItem.Checked = value;

				if (value)
				{
					this.SetToolTip();
				}
			}
		}

		private void Init(RulerInfo rulerInfo)
		{
			this.SetStyle(ControlStyles.ResizeRedraw, true);
			this.UpdateStyles();

			ResourceManager resources = new ResourceManager(typeof(MainForm));
			this.Icon = ((Icon)(resources.GetObject("$this.Icon")));

			this.SetUpMenu();

			this.Text = "Ruler";
			this.BackColor = Color.White;

			rulerInfo.CopyInto(this);

			this.FormBorderStyle = FormBorderStyle.None;

			this.ContextMenu = _menu;
			this.Font = new Font("Tahoma", 10);

			this.SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

			MouseDoubleClick += (sender, args) =>
				{ IsVertical = !IsVertical; };

			_toolTipMenuItem.Checked = true;
			this._toolTip.AutoPopDelay = 9999;
		}

		private RulerInfo GetRulerInfo()
		{
			RulerInfo rulerInfo = new RulerInfo();

			this.CopyInto(rulerInfo);

			return rulerInfo;
		}

		private void SetUpMenu()
		{
			this.AddMenuItem("Stay On Top");
			this._verticalMenuItem = this.AddMenuItem("Vertical");
			this._toolTipMenuItem = this.AddMenuItem("Tool Tip");
			MenuItem opacityMenuItem = this.AddMenuItem("Opacity");
			this._lockedMenuItem = this.AddMenuItem("Lock resizing", Shortcut.None, this.LockHandler);
			this.AddMenuItem("Set size...", Shortcut.None, this.SetWidthHeightHandler);
			this.AddMenuItem("Duplicate", Shortcut.None, this.DuplicateHandler);
			this.AddMenuItem("-");
			this.AddMenuItem("About...");
			this.AddMenuItem("-");
			this.AddMenuItem("Exit");

			for (int i = 10; i <= 100; i += 10)
			{
				MenuItem subMenu = new MenuItem(i + "%");
				subMenu.Click += new EventHandler(OpacityMenuHandler);
				opacityMenuItem.MenuItems.Add(subMenu);
			}
		}

		private void SetWidthHeightHandler(object sender, EventArgs e)
		{
			SetSizeForm form = new SetSizeForm(this.Width, this.Height);

			if (this.TopMost)
			{
				form.TopMost = true;
			}

			if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				Size size = form.GetNewSize();

				this.Width = size.Width;
				this.Height = size.Height;
			}
		}

		private void LockHandler(object sender, EventArgs e)
		{
			this.IsLocked = !this.IsLocked;
			this._lockedMenuItem.Checked = this.IsLocked;
		}

		private void DuplicateHandler(object sender, EventArgs e)
		{
			string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;

			RulerInfo rulerInfo = this.GetRulerInfo();

			ProcessStartInfo startInfo = new ProcessStartInfo(exe, rulerInfo.ConvertToParameters());

			Process process = new Process();
			process.StartInfo = startInfo;
			process.Start();
		}

		private MenuItem AddMenuItem(string text)
		{
			return AddMenuItem(text, Shortcut.None, this.MenuHandler);
		}

		private MenuItem AddMenuItem(string text, Shortcut shortcut, EventHandler handler)
		{
			MenuItem mi = new MenuItem(text);
			mi.Click += new EventHandler(handler);
			mi.Shortcut = shortcut;
			_menu.MenuItems.Add(mi);

			return mi;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			_offset = new Point(MousePosition.X - Location.X, MousePosition.Y - Location.Y);
			_mouseDownPoint = MousePosition;
			_mouseDownRect = ClientRectangle;

			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			_resizeRegion = ResizeRegion.None;
			base.OnMouseUp(e);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (_resizeRegion != ResizeRegion.None)
			{
				HandleResize();
				return;
			}

			Point clientCursorPos = PointToClient(MousePosition);
			Rectangle resizeInnerRect = ClientRectangle;
			resizeInnerRect.Inflate(-_resizeBorderWidth, -_resizeBorderWidth);

			bool inResizableArea = ClientRectangle.Contains(clientCursorPos) && !resizeInnerRect.Contains(clientCursorPos);

			if (inResizableArea)
			{
				ResizeRegion resizeRegion = GetResizeRegion(clientCursorPos);
				SetResizeCursor(resizeRegion);

				if (e.Button == MouseButtons.Left)
				{
					_resizeRegion = resizeRegion;
					HandleResize();
				}
			}
			else
			{
				Cursor = Cursors.Default;

				if (e.Button == MouseButtons.Left)
				{
					Location = new Point(MousePosition.X - _offset.X, MousePosition.Y - _offset.Y);
				}
			}

			base.OnMouseMove(e);
		}

		protected override void OnResize(EventArgs e)
		{
			if (this.ShowToolTip)
			{
				this.SetToolTip();
			}

			base.OnResize(e);
		}

		private void SetToolTip()
		{
			_toolTip.SetToolTip(this, string.Format("Width: {0}\nHeight: {1}", SizeToString(Width), SizeToString(Height)));
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Right:
				case Keys.Left:
				case Keys.Up:
				case Keys.Down:
					HandleMoveResizeKeystroke(e);
					break;

				case Keys.Space:
					ChangeOrientation();
					break;
				case Keys.Enter:
					MessageBox.Show(string.Format("Width: {0}\nHeight: {1}", SizeToString(Width), SizeToString(Height)));
					break;
			}

			base.OnKeyDown(e);
		}

		private void HandleMoveResizeKeystroke(KeyEventArgs e)
		{
			var size = e.Control ? 1 : 10;
			var shouldResize = e.Shift;
			if (e.KeyCode == Keys.Right)
			{
				if (shouldResize)
				{
					Width += size;
				}
				else
				{
					Left += size;
				}
			}
			else if (e.KeyCode == Keys.Left)
			{
				if (shouldResize)
				{
					Width -= size;
				}
				else
				{
					Left -= size;
				}
			}
			else if (e.KeyCode == Keys.Up)
			{
				if (shouldResize)
				{
					Height -= size;
				}
				else
				{
					Top -= size;
				}
			}
			else if (e.KeyCode == Keys.Down)
			{
				if (shouldResize)
				{
					Height += size;
				}
				else
				{
					Top += size;
				}
			}
		}

		private void HandleResize()
		{
			if (this.IsLocked)
			{
				return;
			}

			switch (_resizeRegion)
			{
				case ResizeRegion.E:
					{
						int diff = MousePosition.X - _mouseDownPoint.X;
						Width = _mouseDownRect.Width + diff;
						break;
					}
				case ResizeRegion.S:
					{
						int diff = MousePosition.Y - _mouseDownPoint.Y;
						Height = _mouseDownRect.Height + diff;
						break;
					}
				case ResizeRegion.SE:
					{
						Width = _mouseDownRect.Width + MousePosition.X - _mouseDownPoint.X;
						Height = _mouseDownRect.Height + MousePosition.Y - _mouseDownPoint.Y;
						break;
					}
			}
		}

		private void SetResizeCursor(ResizeRegion region)
		{
			switch (region)
			{
				case ResizeRegion.N:
				case ResizeRegion.S:
					Cursor = Cursors.SizeNS;
					break;

				case ResizeRegion.E:
				case ResizeRegion.W:
					Cursor = Cursors.SizeWE;
					break;

				case ResizeRegion.NW:
				case ResizeRegion.SE:
					Cursor = Cursors.SizeNWSE;
					break;

				default:
					Cursor = Cursors.SizeNESW;
					break;
			}
		}

		private ResizeRegion GetResizeRegion(Point clientCursorPos)
		{
			if (clientCursorPos.Y <= _resizeBorderWidth)
			{
				if (clientCursorPos.X <= _resizeBorderWidth) return ResizeRegion.NW;
				else if (clientCursorPos.X >= Width - _resizeBorderWidth) return ResizeRegion.NE;
				else return ResizeRegion.N;
			}
			else if (clientCursorPos.Y >= Height - _resizeBorderWidth)
			{
				if (clientCursorPos.X <= _resizeBorderWidth) return ResizeRegion.SW;
				else if (clientCursorPos.X >= Width - _resizeBorderWidth) return ResizeRegion.SE;
				else return ResizeRegion.S;
			}
			else
			{
				if (clientCursorPos.X <= _resizeBorderWidth) return ResizeRegion.W;
				else return ResizeRegion.E;
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;

			int height = Height;
			int width = Width;

			if (IsVertical)
			{
				graphics.RotateTransform(90);
				graphics.TranslateTransform(0, -Width + 1);
				height = Width;
				width = Height;
			}

			DrawRuler(graphics, width, height);

			base.OnPaint(e);
		}

		private void DrawRuler(Graphics g, int formWidth, int formHeight)
		{
			// Border
			g.DrawRectangle(Pens.Black, 0, 0, formWidth - 1, formHeight - 1);

			// Width
			g.DrawString(SizeToString(formWidth), Font, Brushes.Black, 10, (formHeight / 2) - (Font.Height / 2));

			// Millimeter Ticks
			for (int i = 0; i < formWidth/PixelsPerMillimeter; i++)
			{
				double x = i * PixelsPerMillimeter;
				int tickHeight;
				if (i % 50 == 0)
				{
					tickHeight = 15;
					DrawTickLabel(g, i.ToString() + "mm", (int)x, formHeight, tickHeight, true);
				}
				else if (i % 10 == 0)
				{
					tickHeight = 10;
				}
				else
				{
					tickHeight = 5;
				}

				DrawTick(g, (int)x, formHeight, tickHeight, true);
			}

			// Pixel Ticks
			for (int i = 0; i < formWidth; i++)
			{
				if (i % 2 == 0)
				{
					double x = i * PixelsPerMillimeter;
					int tickHeight;
					if (i % 100 == 0)
					{
						tickHeight = 15;
						DrawTickLabel(g, i.ToString() + "px", i, formHeight, tickHeight);
					}
					else if (i % 10 == 0)
					{
						tickHeight = 10;
					}
					else
					{
						tickHeight = 5;
					}

					DrawTick(g, i, formHeight, tickHeight);
				}
			}
		}

		private static string SizeToString(int formWidth)
		{
			return string.Format("{0}px, {1,2:F}mm", formWidth, formWidth / PixelsPerMillimeter);
		}

		private static void DrawTick(Graphics g, int xPos, int formHeight, int tickHeight, bool isTop = false)
		{
			if (isTop)
			{
				g.DrawLine(Pens.Black, xPos, 0, xPos, tickHeight);
			}
			else
			{
				g.DrawLine(Pens.Black, xPos, formHeight, xPos, formHeight - tickHeight);
			}
		}

		private void DrawTickLabel(Graphics g, string text, int xPos, int formHeight, int height, bool isTop = false)
		{
			if (isTop)
			{
				g.DrawString(text, Font, Brushes.Black, xPos, height);
			}
			else
			{
				g.DrawString(text, Font, Brushes.Black, xPos, formHeight - height - Font.Height);
			}
		}

		private static void Main(params string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);

			MainForm mainForm;

			if (args.Length == 0)
			{
				mainForm = new MainForm();
			}
			else
			{
				mainForm = new MainForm(RulerInfo.CovertToRulerInfo(args));
			}

			Application.Run(mainForm);
		}

		private void OpacityMenuHandler(object sender, EventArgs e)
		{
			MenuItem mi = (MenuItem)sender;
			Opacity = double.Parse(mi.Text.Replace("%", "")) / 100;
		}

		private void MenuHandler(object sender, EventArgs e)
		{
			MenuItem mi = (MenuItem)sender;

			switch (mi.Text)
			{
				case "Exit":
					Close();
					break;

				case "Tool Tip":
					ShowToolTip = !ShowToolTip;
					break;

				case "Vertical":
					ChangeOrientation();
					break;

				case "Stay On Top":
					mi.Checked = !mi.Checked;
					TopMost = mi.Checked;
					break;

				case "About...":
					string message = string.Format("Ruler v{0} by Jeff Key\nwww.sliver.com\nIcon by Kristen Magee @ www.kbecca.com", Application.ProductVersion);
					MessageBox.Show(message, "About Ruler", MessageBoxButtons.OK, MessageBoxIcon.Information);
					break;

				default:
					MessageBox.Show("Unknown menu item.");
					break;
			}
		}

		private void ChangeOrientation()
		{
			this.IsVertical = !IsVertical;
			int width = Width;
			this.Width = Height;
			this.Height = width;
		}
	}
}
