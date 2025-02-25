using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopPet
{
    // Всплывающее окно для отображения индикаторов состояний

    public class GraphicalPopupForm : Form
    {
        private int hunger;
        private int loneliness;
        private int maxHunger;
        private int maxLoneliness;

        public GraphicalPopupForm()
        {
            // Окно без рамки, чтобы можно было полностью контролировать отрисовку
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(220, 110); // Размер увеличен для комфортного размещения элементов
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.DoubleBuffered = true;

            // Прозрачный фон с закруглёнными краями// Используйте более стабильное решение:
            this.AllowTransparency = true;
            this.BackColor = Color.Gray;
            this.TransparencyKey = Color.Gray;
        }

        public void UpdateState(int hunger, int loneliness, int maxhunger, int maxloneliness)
        {
            this.hunger = hunger;
            this.loneliness = loneliness;
            this.maxHunger = maxhunger;
            this.maxLoneliness = maxloneliness;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;

            // Рисуем закруглённый прямоугольник как фон
            int radius = 15;
            using (GraphicsPath path = RoundedRect(rect, radius))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(220, Color.LightBlue)))
            {
                g.FillPath(bgBrush, path);
                using (Pen pen = new Pen(Color.DarkBlue, 2))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Отступы
            int margin = 10;
            int barWidth = rect.Width - 2 * margin;
            int barHeight = 15;

            // Рисуем метку и индикатор для "Голода"
            using (Font font = new Font("Segoe UI", 9))
            {
                g.DrawString("Голод", font, Brushes.Black, margin, margin);
            }
            Rectangle hungerBarRect = new Rectangle(margin, margin + 20, barWidth, barHeight);
            DrawProgressBar(g, hungerBarRect, hunger, maxHunger, Color.Red);

            // Рисуем метку и индикатор для "Одиночества"
            using (Font font = new Font("Segoe UI", 9))
            {
                g.DrawString("Одиночество", font, Brushes.Black, margin, margin + 45);
            }
            Rectangle lonelinessBarRect = new Rectangle(margin, margin + 65, barWidth, barHeight);
            DrawProgressBar(g, lonelinessBarRect, loneliness, maxLoneliness, Color.Green);
        }

        private void DrawProgressBar(Graphics g, Rectangle rect, int value, int max, Color fillColor)
        {
            // Рисуем границу индикатора
            g.DrawRectangle(Pens.Black, rect);
            // Вычисляем ширину заполненной части
            int fillWidth = (int)((rect.Width - 2) * ((float)value / max));
            if (fillWidth < 0)
                fillWidth = 0;
            Rectangle fillRect = new Rectangle(rect.X + 1, rect.Y + 1, fillWidth, rect.Height - 2);
            using (Brush brush = new SolidBrush(fillColor))
            {
                g.FillRectangle(brush, fillRect);
            }
        }

        // Метод для создания закруглённого прямоугольника
        private GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            // Левый верхний угол
            path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
            // Верхняя сторона
            path.AddLine(bounds.X + radius, bounds.Y, bounds.Right - radius, bounds.Y);
            // Правый верхний угол
            path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
            // Правая сторона
            path.AddLine(bounds.Right, bounds.Y + radius, bounds.Right, bounds.Bottom - radius);
            // Правый нижний угол
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            // Нижняя сторона
            path.AddLine(bounds.Right - radius, bounds.Bottom, bounds.X + radius, bounds.Bottom);
            // Левый нижний угол
            path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            // Левая сторона
            path.AddLine(bounds.X, bounds.Bottom - radius, bounds.X, bounds.Y + radius);
            path.CloseFigure();
            return path;
        }
    }
}
