using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.IO;

namespace WinDiv
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("User32.dll")]
        public static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

        [DllImport("User32.dll")]
        public static extern IntPtr GetTopWindow(IntPtr handle);

        private SystemHotkey WinDivSystemHotkey;

        LinearGradientBrush NormalFillBrush;
        LinearGradientBrush SelectedFillBrush;
        Rectangle FirstRectangleSelected = null;
        bool MouseIsDown = false;
        Rectangle LastRectangle = null;
        IntPtr ForegroundWindow;

        public MainWindow()
        {
            InitializeComponent();
            System.Windows.Forms.NotifyIcon notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = WinDiv.Properties.Resources.Grid;
            notifyIcon.Visible = true;
            notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
            var menuItem = new System.Windows.Forms.MenuItem("Exit");
            notifyIcon.ContextMenu.MenuItems.Add(menuItem);
            notifyIcon.Text = "WinDiv";
            menuItem.Click += new EventHandler(menuItem_Click);

            // Set the height and width of this window in proportion to the screen dimensions
            Height = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height / 4;
            Width = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width / 4;

            ctlSizeGrid.Height = this.Height;
            ctlSizeGrid.Width = this.Width;

            int maxColumns = 8;
            int maxRows = 8;

            var solidBlackBrush = new SolidColorBrush();
            solidBlackBrush.Color = Colors.Black;

            NormalFillBrush = new LinearGradientBrush();
            NormalFillBrush.GradientStops.Add(new GradientStop(Colors.Black, 0.0));
            NormalFillBrush.GradientStops.Add(new GradientStop(Colors.Gray, 0.5));
            NormalFillBrush.GradientStops.Add(new GradientStop(Colors.Black, 1.0));

            SelectedFillBrush = new LinearGradientBrush();
            SelectedFillBrush.GradientStops.Add(new GradientStop(Colors.Gray, 0.0));
            SelectedFillBrush.GradientStops.Add(new GradientStop(Colors.White, 0.5));
            SelectedFillBrush.GradientStops.Add(new GradientStop(Colors.Gray, 1.0));

            for (int row = 0; row < maxRows; row++)
            {
                var rowDefinition = new RowDefinition();
                rowDefinition.Height = new GridLength(ctlSizeGrid.Height/maxRows);
                ctlSizeGrid.RowDefinitions.Add(rowDefinition);

                for (int col = 0; col < maxColumns; col++)
                {
                    if (ctlSizeGrid.ColumnDefinitions.Count <= col)
                    {
                        var colDefinition = new ColumnDefinition();
                        colDefinition.Width = new GridLength(ctlSizeGrid.Width/maxColumns);
                        ctlSizeGrid.ColumnDefinitions.Add(colDefinition);
                    }
                    Rectangle rectangle = new Rectangle();
                    rectangle.RadiusX = 4;
                    rectangle.RadiusY = 4;
                    rectangle.StrokeThickness = 1;
                    rectangle.Stroke = solidBlackBrush;
                    rectangle.Fill = NormalFillBrush;
                    rectangle.Margin = new Thickness(2);
                    Grid.SetRow(rectangle, row);
                    Grid.SetColumn(rectangle, col);
                    ctlSizeGrid.Children.Add(rectangle);
                }
            }
            ctlSizeGrid.MouseUp += new MouseButtonEventHandler(ctlSizeGrid_MouseUp);
            ctlSizeGrid.MouseDown += new MouseButtonEventHandler(ctlSizeGrid_MouseDown);
            ctlSizeGrid.MouseMove += new MouseEventHandler(ctlSizeGrid_MouseMove);

            // Set up system hot key
            WinDivSystemHotkey = new SystemHotkey();
            WinDivSystemHotkey.Shortcut = System.Windows.Forms.Shortcut.AltF1;
            WinDivSystemHotkey.Pressed += new EventHandler(WinDivSystemHotkey_Pressed);
        }

        void menuItem_Click(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        void WinDivSystemHotkey_Pressed(object sender, EventArgs e)
        {
            // Get the top window
            ForegroundWindow = GetForegroundWindow();
            // Show this window
            WindowState = System.Windows.WindowState.Normal;
            SelectRectangle(-1, -1, -1, -1);
            Show();
            Activate();
        }

        void ctlSizeGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (MouseIsDown)
            {
                var childrenAtPoint = GetChildrenAtPoint<Rectangle>((Visual)sender, e.GetPosition((UIElement)sender));
                if (FirstRectangleSelected == null)
                {
                    if (childrenAtPoint.Count > 0)
                    {
                        FirstRectangleSelected = childrenAtPoint[0];
                        SelectRectangle(Grid.GetColumn(FirstRectangleSelected), Grid.GetColumn(FirstRectangleSelected), Grid.GetRow(FirstRectangleSelected), Grid.GetRow(FirstRectangleSelected));
                    }
                }
                else
                {
                    if (childrenAtPoint.Count > 0 && LastRectangle != childrenAtPoint[0])
                    {
                        LastRectangle = childrenAtPoint[0];
                        int startX = Math.Min(Grid.GetColumn(LastRectangle), Grid.GetColumn(FirstRectangleSelected));
                        int endX = Math.Max(Grid.GetColumn(LastRectangle), Grid.GetColumn(FirstRectangleSelected));

                        int startY = Math.Min(Grid.GetRow(LastRectangle), Grid.GetRow(FirstRectangleSelected));
                        int endY = Math.Max(Grid.GetRow(LastRectangle), Grid.GetRow(FirstRectangleSelected));

                        SelectRectangle(startX, endX, startY, endY);
                    }
                }
            }
        }

        void SelectRectangle(int startX, int endX, int startY, int endY)
        {
            foreach (Rectangle rect in ctlSizeGrid.Children
                        .Cast<UIElement>()
                        .Where(rect =>
                            Grid.GetColumn(rect) >= startX &&
                            Grid.GetColumn(rect) <= endX &&
                            Grid.GetRow(rect) >= startY &&
                            Grid.GetRow(rect) <= endY))
            {
                rect.Fill = SelectedFillBrush;
            }
            foreach (Rectangle rect in ctlSizeGrid.Children
                .Cast<UIElement>()
                .Where(rect =>
                    !(Grid.GetColumn(rect) >= startX &&
                            Grid.GetColumn(rect) <= endX &&
                            Grid.GetRow(rect) >= startY &&
                            Grid.GetRow(rect) <= endY)))
            {
                rect.Fill = NormalFillBrush;
            }
        }

        void ctlSizeGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Retrieve the coordinate of the mouse position.
            var childrenAtPoint = GetChildrenAtPoint<Rectangle>((Visual)sender, e.GetPosition((UIElement)sender));
            MouseIsDown = true;
            // Perform actions on the hit test results list.
            if (childrenAtPoint.Count > 0)
            {
                FirstRectangleSelected = childrenAtPoint[0];
                SelectRectangle(Grid.GetColumn(FirstRectangleSelected), Grid.GetColumn(FirstRectangleSelected), Grid.GetRow(FirstRectangleSelected), Grid.GetRow(FirstRectangleSelected));
            }
            ctlSizeGrid.CaptureMouse();
        }

        void ctlSizeGrid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Forms.Screen windowScreen = System.Windows.Forms.Screen.FromHandle(ForegroundWindow);
            int windowWidth = (windowScreen.WorkingArea.Width / ctlSizeGrid.ColumnDefinitions.Count) * (Math.Abs(Grid.GetColumn(FirstRectangleSelected) - Grid.GetColumn(LastRectangle))+1);
            int windowHeight = (windowScreen.WorkingArea.Height / ctlSizeGrid.ColumnDefinitions.Count) * (Math.Abs(Grid.GetRow(FirstRectangleSelected) - Grid.GetRow(LastRectangle))+1);
            int startX = windowScreen.Bounds.Left + (Math.Min(Grid.GetColumn(FirstRectangleSelected), Grid.GetColumn(LastRectangle)) * (windowScreen.WorkingArea.Width / ctlSizeGrid.ColumnDefinitions.Count));
            int startY = windowScreen.Bounds.Top + (Math.Min(Grid.GetRow(FirstRectangleSelected), Grid.GetRow(LastRectangle)) * (windowScreen.WorkingArea.Height / ctlSizeGrid.RowDefinitions.Count));
            MoveWindow(ForegroundWindow, startX, startY, windowWidth, windowHeight, true);
            MouseIsDown = false;
            FirstRectangleSelected = null;
            ctlSizeGrid.ReleaseMouseCapture();
            Hide();
        }

        List<T> GetChildrenAtPoint<T>(Visual v, Point p) where T : class
        {
            List<T> hitResults = new List<T>();
            // Set up a callback to receive the hit test result enumeration.
            VisualTreeHelper.HitTest(v, null, delegate(HitTestResult result) { if (result.VisualHit is T) { hitResults.Add(result.VisualHit as T); }; return HitTestResultBehavior.Continue; },
                new PointHitTestParameters(p));
            return hitResults;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }

        void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }
    }
}
