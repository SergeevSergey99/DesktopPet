using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DesktopPet
{
    // Окно настроек для отображения статистики умерших питомцев
    public class SettingsForm : Form
    {
        private Label infoLabel;

        public SettingsForm()
        {
            this.Text = "Настройки";
            this.Size = new Size(300, 200);
            infoLabel = new Label() { Location = new Point(10, 10), Size = new Size(280, 150), AutoSize = false };
            this.Controls.Add(infoLabel);
            UpdateInfo();
        }

        public void UpdateInfo()
        {
            string info = $"Количество умерших питомцев: {PetForm.deadPetLifespans.Count}\n";
            for (int i = 0; i < PetForm.deadPetLifespans.Count; i++)
            {
                info += $"Питомец {i + 1}: {PetForm.deadPetLifespans[i].TotalSeconds:F0} секунд\n";
            }
            infoLabel.Text = info;
        }
    }

    // Главная программа с NotifyIcon
    static class Program
    {
        private static NotifyIcon trayIcon;
        private static SettingsForm settingsForm;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Создание значка в системном трее
            trayIcon = new NotifyIcon();
            trayIcon.Icon = SystemIcons.Application;
            trayIcon.Visible = true;
            trayIcon.Text = "Desktop Pet";
            ContextMenuStrip trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Настройки", null, OnSettingsClicked);
            trayMenu.Items.Add("Выход", null, OnExitClicked);
            trayIcon.ContextMenuStrip = trayMenu;

            // Запуск окна питомца
            PetForm petForm = new PetForm();
            Application.Run(petForm);
        }

        private static void OnSettingsClicked(object sender, EventArgs e)
        {
            if (settingsForm == null || settingsForm.IsDisposed)
            {
                settingsForm = new SettingsForm();
            }
            else
            {
                settingsForm.UpdateInfo();
            }
            settingsForm.Show();
            settingsForm.BringToFront();
        }

        private static void OnExitClicked(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }

}
