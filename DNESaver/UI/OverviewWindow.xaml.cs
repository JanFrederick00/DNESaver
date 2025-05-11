using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DNESaver.UI
{
    /// <summary>
    /// Interaction logic for OverviewWindow.xaml
    /// </summary>
    public partial class OverviewWindow : Window, INotifyPropertyChanged
    {
        private string messages = "";

        public OverviewWindow(string savegameLocation)
        {
            SavegameLocation = savegameLocation;
            InitializeComponent();
        }

        public string Messages
        {
            get => messages; set
            {
                messages = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Messages)));
            }
        }

        public string SavegameLocation { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OpenSaveLocation(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer", SavegameLocation);
            }
            catch (Exception ex)
            {

            }
        }

        public void AddLine(string s)
        {
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine(s);
                Messages += $"\r\n{s}";
            });
        }

        public DNESaveFile? SaveFile = null;
        public string? WorkingFilePath;
        public string? Slot0Filename;

        void OpenEditor()
        {
            //Console.WriteLine("Decompressing Savegame on disk.");
            //savefile.WriteSavegameTo(Slot0Filename);




        }

        private void OpenSgEditor(object sender, RoutedEventArgs e)
        {
            if (SaveFile == null || WorkingFilePath == null || Slot0Filename == null) return;

            var patcher = new SaveFilePatcher(WorkingFilePath);
            var mgr = new SavegameModifier(SaveFile, patcher);

            SaveEditor se = new(patcher, mgr);
            se.ShowDialog();
            if (se.WasSuccesful)
            {
                AddLine("Patching was successful; Copying working file to Slot 0...");
                File.Copy(WorkingFilePath, Slot0Filename, true);
                MessageBox.Show("Your save game was successfully patched.");
                Close();
            }
        }
    }
}
