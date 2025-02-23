using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace DesktopPet
{
    public class PetForm : Form
    {
        private Image petImage;
        private Timer moveTimer;
        private Timer stateTimer;

        // Параметры движения
        private float currentX;
        private float targetX;
        private bool isPaused;
        private int pauseTicks;
        private const int MoveInterval = 50; // мс
        private const int Speed = 5; // пикселей за тик

        // Состояния питомца
        private int hunger;       // Голод
        private int loneliness;   // Одиночество
        private const int MAX_HUNGER_STATE = 100;
        private const int MAX_LONELINESS_STATE = 300;
        private DateTime petCreationTime;
        private bool isDead;

        // Графические кнопки (их области на форме)
        private bool isHovering = false;
        private Rectangle feedButtonRect;
        private Rectangle petButtonRect;
        private Rectangle buryButtonRect; // для мёртвого питомца

        // Графическое всплывающее окно для состояний
        private GraphicalPopupForm popupForm;

        public static List<TimeSpan> deadPetLifespans = new List<TimeSpan>();

        // --- P/Invoke для получения положения панели задач ---
        private const UInt32 ABM_GETTASKBARPOS = 5;

        [DllImport("shell32.dll", SetLastError = true)]
        static extern UInt32 SHAppBarMessage(UInt32 dwMessage, ref APPBARDATA pData);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left, top, right, bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public UInt32 cbSize;
            public IntPtr hWnd;
            public UInt32 uCallbackMessage;
            public UInt32 uEdge;
            public RECT rc;
            public Int32 lParam;
        }
        // -------------------------------------------------------

        public PetForm()
        {
            // Задаём фиксированные размеры окна питомца (например, 120×120)
            this.ClientSize = new Size(120, 120);
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            // Загрузка изображения питомца (оригинальный ресурс)
            try
            {
                petImage = Image.FromFile("Images/Capy/frame_0.png");
            }
            catch
            {
                petImage = new Bitmap(100, 100);
                using (Graphics g = Graphics.FromImage(petImage))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillEllipse(Brushes.Blue, 0, 0, 100, 100);
                }
            }
            // Не используем размер изображения для окна – используем фиксированные размеры

            // Инициализация позиции – случайное положение по горизонтали
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Random rand = new Random();
            currentX = rand.Next(screenBounds.Left, screenBounds.Right - this.Width);
            targetX = currentX;
            UpdateVerticalPosition(); // устанавливаем вертикальную позицию
            // После UpdateVerticalPosition() this.Location.Y выставится корректно

            // Инициализация состояний
            hunger = 0;
            loneliness = 0;
            isDead = false;
            petCreationTime = DateTime.Now;

            // Таймер для движения
            moveTimer = new Timer();
            moveTimer.Interval = MoveInterval;
            moveTimer.Tick += MoveTimer_Tick;
            moveTimer.Start();

            // Таймер для обновления состояний
            stateTimer = new Timer();
            stateTimer.Interval = 1000;
            stateTimer.Tick += StateTimer_Tick;
            stateTimer.Start();

            // Инициализация графического всплывающего окна для состояний
            popupForm = new GraphicalPopupForm();
            popupForm.StartPosition = FormStartPosition.Manual;

            // Обработчики событий мыши для показа кнопок и всплывающего окна
            this.MouseEnter += PetForm_MouseEnter;
            this.MouseLeave += PetForm_MouseLeave;
            this.MouseClick += PetForm_MouseClick;

            this.DoubleBuffered = true;

            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            UpdateVerticalPosition();
        }

        // Получает положение панели задач через SHAppBarMessage
        private Rectangle GetTaskbarRectangle()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            UInt32 result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            if (result != 0)
            {
                return new Rectangle(data.rc.left, data.rc.top, data.rc.right - data.rc.left, data.rc.bottom - data.rc.top);
            }
            return Screen.PrimaryScreen.WorkingArea;
        }

        // Обновляет вертикальную позицию питомца:
        // Если панель задач видна – питомец располагается сразу над ней,
        // если скрыта – у нижней границы экрана.
        private void UpdateVerticalPosition()
        {
            Rectangle screenBounds = Screen.PrimaryScreen.Bounds;
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            int tolerance = 5;

            int targetY;
            if (Math.Abs(screenBounds.Bottom - workingArea.Bottom) <= tolerance)
            {
                targetY = screenBounds.Bottom - this.Height;
            }
            else
            {
                Rectangle taskbarRect = GetTaskbarRectangle();
                targetY = taskbarRect.Top - this.Height;
            }
            this.Location = new Point(this.Location.X, targetY);
        }

        private void MoveTimer_Tick(object sender, EventArgs e)
        {
            UpdateVerticalPosition();

            if (isDead)
                return;

            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            if (isPaused)
            {
                pauseTicks--;
                if (pauseTicks <= 0)
                {
                    Random rand = new Random();
                    targetX = rand.Next(workingArea.Left, workingArea.Right - this.Width);
                    isPaused = false;
                }
            }
            else
            {
                if (isHovering) // Если мышь над питомцем, не двигаем его
                    return;
                if (Math.Abs(currentX - targetX) < Speed)
                {
                    currentX = targetX;
                    Random rand = new Random();
                    pauseTicks = rand.Next(20, 60);
                    isPaused = true;
                }
                else
                {
                    currentX += (currentX < targetX) ? Speed : -Speed;
                }
            }
            this.Location = new Point((int)currentX, this.Location.Y);
        }

        private void StateTimer_Tick(object sender, EventArgs e)
        {
            if (isDead)
                return;

            hunger++;
            loneliness++;

            if (popupForm.Visible)
                popupForm.UpdateState(hunger, loneliness, MAX_HUNGER_STATE, MAX_LONELINESS_STATE);

            if (hunger >= MAX_HUNGER_STATE || loneliness >= MAX_LONELINESS_STATE)
                PetDies();
        }

        private void PetDies()
        {
            isDead = true;
            moveTimer.Stop();
            stateTimer.Stop();
            TimeSpan lifespan = DateTime.Now - petCreationTime;
            deadPetLifespans.Add(lifespan);
            Invalidate();
            MessageBox.Show("Питомец умер...");
        }
        private void PetForm_MouseEnter(object sender, EventArgs e)
        {
            isHovering = true;

            // Центрируем всплывающее окно относительно питомца
            Point popupLocation = new Point(
                this.Location.X + (this.Width - popupForm.Width) / 2,
                this.Location.Y - popupForm.Height - 5
            );
            popupForm.Location = popupLocation;
            popupForm.UpdateState(hunger, loneliness, MAX_HUNGER_STATE, MAX_LONELINESS_STATE);
            popupForm.Show();

            // Рассчитываем кнопки с учётом ограничений по ширине окна
            int gap = 5;
            int buttonHeight = 25;
            int availableWidth = this.ClientSize.Width - 3 * gap;

            if (!isDead)
            {
                int buttonWidth = availableWidth / 2;

                feedButtonRect = new Rectangle(
                    gap,
                    this.ClientSize.Height - buttonHeight - gap,
                    buttonWidth,
                    buttonHeight
                );

                petButtonRect = new Rectangle(
                    2 * gap + buttonWidth,
                    this.ClientSize.Height - buttonHeight - gap,
                    buttonWidth,
                    buttonHeight
                );
            }
            else
            {
                buryButtonRect = new Rectangle(
                    gap,
                    this.ClientSize.Height - buttonHeight - gap,
                    availableWidth + gap,
                    buttonHeight
                );
            }
            Invalidate();
        }


        private void PetForm_MouseLeave(object sender, EventArgs e)
        {
            isHovering = false;
            popupForm.Hide();
            Invalidate();
        }

        private void PetForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isHovering)
                return;

            if (!isDead)
            {
                if (feedButtonRect.Contains(e.Location))
                {
                    hunger = 0;
                    Invalidate();
                }
                else if (petButtonRect.Contains(e.Location))
                {
                    loneliness = 0;
                    Invalidate();
                }
            }
            else
            {
                if (buryButtonRect.Contains(e.Location))
                {
                    ResetPet();
                }
            }
        }

        private void ResetPet()
        {
            hunger = 0;
            loneliness = 0;
            isDead = false;
            petCreationTime = DateTime.Now;
            moveTimer.Start();
            stateTimer.Start();
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // Оптимальные настройки для Pixel-Perfect рендеринга
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            Rectangle destRect = new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height);

            if (isDead)
            {
                ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {0,0,0,0,0},
                    new float[] {0,0,0,0,0},
                    new float[] {0,0,0,0,0},
                    new float[] {0,0,0,1,0},
                    new float[] {0,0,0,0,1}
                });

                using (ImageAttributes attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(colorMatrix);
                    g.DrawImage(petImage, destRect, 0, 0, petImage.Width, petImage.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            else
            {
                g.DrawImage(petImage, destRect);
            }

            if (isHovering)
            {
                if (!isDead)
                {
                    DrawButton(g, feedButtonRect, "Кормить");
                    DrawButton(g, petButtonRect, "Гладить");
                }
                else
                {
                    DrawButton(g, buryButtonRect, "Похоронить");
                }
            }
        }


        private void DrawButton(Graphics g, Rectangle rect, string text)
        {
            using (GraphicsPath path = RoundedRect(rect, 5))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.LightGray, Color.Gray, LinearGradientMode.Vertical))
                {
                    g.FillPath(brush, path);
                }
                // Рисуем обводку кнопки – если ранее появлялась красная, теперь используем прозрачный цвет
                using (Pen pen = new Pen(Color.Transparent))
                {
                    g.DrawPath(pen, path);
                }
            }
            using (Font font = new Font("Segoe UI", 8))
            {
                StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, Brushes.Black, rect, sf);
            }
        }

        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnFormClosing(e);
        }
    }
}
