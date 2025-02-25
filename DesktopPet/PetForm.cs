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
        private List<Image> petFrames = new List<Image>();
        private int currentFrame = 0;
        private Timer moveTimer;
        private Timer stateTimer;
        private Timer animationTimer;

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
        // В PetForm.cs добавьте следующие P/Invoke объявления и константы
        private const int WH_CALLWNDPROC = 4;
        private const int WM_WINDOWPOSCHANGED = 0x0047;
        private static IntPtr hHook = IntPtr.Zero;
        private static PetForm _instance;
        private static NativeCallbackDelegate callbackDelegate;

// Делегат для обратного вызова
        private delegate IntPtr NativeCallbackDelegate(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct CWPSTRUCT
        {
            public IntPtr lParam;
            public IntPtr wParam;
            public uint message;
            public IntPtr hwnd;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, NativeCallbackDelegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
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
            for (int i = 0; i < 2; i++) // предполагаем, что есть 4 кадра
            {
                try
                {
                    petFrames.Add(Image.FromFile($"Images/Capy/frame_{i}.png"));
                }
                catch
                {
                    // Обработка ошибки
             
                    var image = new Bitmap(100, 100);
                    using (Graphics g = Graphics.FromImage(image))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.FillEllipse(Brushes.Blue, 0, 0, 100, 100);
                    }   
                    petFrames.Add(image);
                }
            }
            petImage = petFrames[0];
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

            // Создание таймера анимации
            animationTimer = new Timer();
            animationTimer.Interval = 200; // 5 кадров в секунду
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();
            
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
            
            _instance = this;
    
            // Установка Windows Hook для отслеживания изменений положения окон
            callbackDelegate = new NativeCallbackDelegate(WindowProcCallback);
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (taskbarHwnd != IntPtr.Zero)
            {
                uint processId;
                uint threadId = GetWindowThreadProcessId(taskbarHwnd, out processId);
                if (threadId != 0)
                {
                    hHook = SetWindowsHookEx(WH_CALLWNDPROC, callbackDelegate, IntPtr.Zero, threadId);
                }
            }
        }
        
        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (petFrames.Count > 1) // Только если есть несколько кадров
            {
                currentFrame = (currentFrame + 1) % petFrames.Count;
                petImage = petFrames[currentFrame];
                Invalidate(); // Перерисовка формы
            }
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
    
            // Найдем фактическое текущее положение панели задач
            IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            RECT taskbarRect;
            int targetY;
    
            if (taskbarHwnd != IntPtr.Zero && GetWindowRect(taskbarHwnd, out taskbarRect))
            {
                // Проверяем, в какой части экрана находится панель задач
                if (taskbarRect.top > screenBounds.Height / 2) // Панель задач снизу
                {
                    targetY = taskbarRect.top - this.Height;
                }
                else if (taskbarRect.left > screenBounds.Width / 2) // Панель задач справа
                {
                    targetY = this.Location.Y; // Не меняем Y
                }
                else if (taskbarRect.right < screenBounds.Width / 2) // Панель задач слева
                {
                    targetY = this.Location.Y; // Не меняем Y
                }
                else // Панель задач сверху
                {
                    targetY = taskbarRect.bottom;
                }
            }
            else
            {
                // Если не можем получить положение панели задач, используем WorkingArea
                if (Math.Abs(screenBounds.Bottom - workingArea.Bottom) <= 5)
                {
                    targetY = screenBounds.Bottom - this.Height;
                }
                else
                {
                    targetY = workingArea.Bottom - this.Height;
                }
            }
    
            // Плавно перемещаем питомца к новой позиции
            if (Math.Abs(this.Location.Y - targetY) > 2)
            {
                this.Location = new Point(this.Location.X, targetY);
            }
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
        private static IntPtr WindowProcCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                CWPSTRUCT cwp = (CWPSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPSTRUCT));
        
                // Проверяем, не сообщение ли это об изменении положения панели задач
                if (cwp.message == WM_WINDOWPOSCHANGED)
                {
                    IntPtr taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                    if (cwp.hwnd == taskbarHwnd)
                    {
                        // Вызываем метод UpdateVerticalPosition через Invoke, 
                        // так как мы находимся в другом потоке
                        if (_instance != null && !_instance.IsDisposed)
                        {
                            _instance.BeginInvoke(new Action(_instance.UpdateVerticalPosition));
                        }
                    }
                }
            }
    
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        private void PetDies()
        {
            isDead = true;
            moveTimer.Stop();
            stateTimer.Stop();
            animationTimer.Stop(); // Останавливаем анимацию
            TimeSpan lifespan = DateTime.Now - petCreationTime;
            deadPetLifespans.Add(lifespan);
            Invalidate();
            MessageBox.Show("Питомец умер...");
        }
        // Добавьте в PetForm.cs для диагностики:
        private void LogDiagnosticInfo()
        {
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), 
                "petlog.txt");
    
            using (StreamWriter sw = File.AppendText(path))
            {
                sw.WriteLine($"[{DateTime.Now}] Visibility check:");
                sw.WriteLine($"Location: {this.Location}, Size: {this.Size}");
                sw.WriteLine($"TransparencyKey: {this.TransparencyKey}");
                sw.WriteLine($"WorkingArea: {Screen.PrimaryScreen.WorkingArea}");
            }
        }
        private void PetForm_MouseEnter(object sender, EventArgs e)
        {
            isHovering = true;

            // Центрируем с проверкой границ экрана
            Point popupLocation = new Point(
                Math.Max(0, Math.Min(Screen.PrimaryScreen.Bounds.Width - popupForm.Width, 
                    this.Location.X + (this.Width - popupForm.Width) / 2)),
                Math.Max(0, this.Location.Y - popupForm.Height - 5)
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
                    // для немедленного обновления прогресс-бара
                    popupForm.UpdateState(hunger, loneliness, MAX_HUNGER_STATE, MAX_LONELINESS_STATE);
                    Invalidate();
                }
                else if (petButtonRect.Contains(e.Location))
                {
                    loneliness = 0;
                    // для немедленного обновления прогресс-бара
                    popupForm.UpdateState(hunger, loneliness, MAX_HUNGER_STATE, MAX_LONELINESS_STATE);
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
            animationTimer.Start(); // Возобновляем анимацию
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
            animationTimer.Stop();
            animationTimer.Dispose();
    
            foreach (var frame in petFrames)
            {
                frame.Dispose();
            }
            if (hHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hHook);
                hHook = IntPtr.Zero;
            }
    
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnFormClosing(e);
        }
    }
}
