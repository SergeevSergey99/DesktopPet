using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer; // для SystemEvents

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
        private const int MAX_STATE = 100;
        private DateTime petCreationTime;
        private bool isDead;

        // Графические кнопки (их области на форме)
        private bool isHovering = false;
        private Rectangle feedButtonRect;
        private Rectangle petButtonRect;
        private Rectangle buryButtonRect; // для мёртвого питомца

        // Графическое всплывающее окно для состояний (описанный ранее класс)
        private GraphicalPopupForm popupForm; 
        
        public static List<TimeSpan> deadPetLifespans = new List<TimeSpan>();


        public PetForm()
        {
            // Настройка формы – небольшое окно под размер питомца
            this.FormBorderStyle = FormBorderStyle.None;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;

            // Загрузка изображения питомца
            try
            {
                petImage = Image.FromFile("pet.png");
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
            this.ClientSize = petImage.Size;

            // Инициализация позиции – случайное положение по горизонтали в пределах рабочей области
            Rectangle workingArea = SystemInformation.WorkingArea;
            Random rand = new Random();
            currentX = rand.Next(workingArea.Left, workingArea.Right - this.Width);
            targetX = currentX;
            int targetY = workingArea.Bottom - this.Height;
            this.Location = new Point((int)currentX, targetY);

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

            // Подписка на системное событие для отслеживания изменений рабочей области (например, появление панели задач)
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Обновляем вертикальную позицию при изменениях в системе
            UpdateVerticalPosition();
        }

        private void UpdateVerticalPosition()
        {
            Rectangle workingArea = SystemInformation.WorkingArea;
            int targetY = workingArea.Bottom - this.Height;
            this.Location = new Point(this.Location.X, targetY);
        }

        private void MoveTimer_Tick(object sender, EventArgs e)
        {
            // Обновляем вертикальную позицию, чтобы питомец всегда был над панелью задач
            UpdateVerticalPosition();

            if (isDead)
                return;

            Rectangle workingArea = SystemInformation.WorkingArea;
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
                popupForm.UpdateState(hunger, loneliness, MAX_STATE);

            if (hunger >= MAX_STATE && loneliness >= MAX_STATE)
                PetDies();
        }
        private void PetDies()
        {
            isDead = true;
            moveTimer.Stop();
            stateTimer.Stop();
            // Сохраняем продолжительность жизни питомца
            TimeSpan lifespan = DateTime.Now - petCreationTime;
            deadPetLifespans.Add(lifespan);
            Invalidate();
            MessageBox.Show("Питомец умер...");
        }

        private void PetForm_MouseEnter(object sender, EventArgs e)
        {
            isHovering = true;
            // Позиционируем всплывающее окно чуть выше питомца
            Point popupLocation = new Point(this.Location.X, this.Location.Y - popupForm.Height - 5);
            popupForm.Location = popupLocation;
            popupForm.UpdateState(hunger, loneliness, MAX_STATE);
            popupForm.Show();

            // Определяем области кнопок с увеличенными размерами
            int buttonWidth = 70;  // увеличено с 40 до 70
            int buttonHeight = 30; // увеличено с 20 до 30
            if (!isDead)
            {
                // Две кнопки: слева – "Кормить", справа – "Гладить"
                feedButtonRect = new Rectangle(5- buttonHeight/2, this.ClientSize.Height - buttonHeight - 5, buttonWidth, buttonHeight);
                petButtonRect = new Rectangle(this.ClientSize.Width - buttonWidth + buttonHeight/2- 5, this.ClientSize.Height - buttonHeight - 5, buttonWidth, buttonHeight);
            }
            else
            {
                // Одна кнопка "Похоронить" по центру
                int buryWidth = 120; // немного шире для размещения текста
                buryButtonRect = new Rectangle((this.ClientSize.Width - buryWidth) / 2, this.ClientSize.Height - buttonHeight - 5, buryWidth, buttonHeight);
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
                    //MessageBox.Show("Питомец покормлен!");
                    Invalidate();
                }
                else if (petButtonRect.Contains(e.Location))
                {
                    loneliness = 0;
                    //MessageBox.Show("Питомец поглажен!");
                    Invalidate();
                }
            }
            else
            {
                if (buryButtonRect.Contains(e.Location))
                {
                    // Здесь можно сохранить статистику жизни питомца
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
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Если питомец мёртв – рисуем его в виде чёрного эллипса,
            // иначе отрисовываем изображение
            if (isDead)
            {
                g.FillEllipse(Brushes.Black, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }
            else
            {
                g.DrawImage(petImage, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }

            // Если курсор над питомцем – отрисовываем графические кнопки
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
            // Отрисовка кнопки с закруглёнными углами и градиентом
            using (GraphicsPath path = RoundedRect(rect, 5))
            {
                using (LinearGradientBrush brush = new LinearGradientBrush(rect, Color.LightGray, Color.Gray, LinearGradientMode.Vertical))
                {
                    g.FillPath(brush, path);
                }
                g.DrawPath(Pens.Black, path);
            }
            // Центрирование текста
            using (Font font = new Font("Segoe UI", 8))
            {
                StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
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
