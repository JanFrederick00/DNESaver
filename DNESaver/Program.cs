using DNESaver;
using DNESaver.UI;
using System.IO;
using System.Security.Cryptography;
using System.Windows;

internal class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string oodle_dll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oo2core_5_win64.dll");
        if (!File.Exists(oodle_dll))
        {
            MessageBox.Show($"The Game uses Oodle to compress its savegame.\nAs it is a proprietary DLL I can't distribute the relevant library with this tool.\n\nPlease acquire a copy of the File 'oo2core_5_win64.dll' and put it in the following location:\n{oodle_dll}", "Missing DLL");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.google.com/search?q=oo2core_5_win64.dll&peek_pws=0") { UseShellExecute = true });
            return -1;
        }

        string savegameBaseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Bloom&Rage", "Saved", "SaveGames");
        string savegameDir = Directory.Exists(savegameBaseDir) ? Directory.EnumerateDirectories(savegameBaseDir).First() : "";


        if (!Directory.Exists(savegameBaseDir) || !Directory.Exists(savegameDir))
        {
            Console.WriteLine($"Error: The savegame directory {savegameBaseDir} does not exist.");
            MessageBox.Show($"Error: The savegame directory {savegameBaseDir} does not exist.");
            return -1;
        }

        string savegameBackupDir = Path.Combine(savegameDir, "_Backups");
        Console.WriteLine($"Using Savegame location: {savegameDir}");
        Directory.CreateDirectory(savegameBackupDir);

        string Slot0Filename = Path.Combine(savegameDir, "0GameSave.sav");


        Console.WriteLine($"Opening: {Slot0Filename}");
        DNESaveFile savefile = new(Slot0Filename);

        if (savefile.IsCompressed)
        {
            Console.WriteLine($"The Savegame was compressed, meaning it was last written by the game.");
            Console.WriteLine("Checking for Backups...");

            static string str(byte[] dat) => string.Concat(dat.Select(s => s.ToString("X2")));
            var mymd5 = str(MD5.HashData(File.ReadAllBytes(savefile.FileName)));
            Console.WriteLine($"Hash of opened Savegame: {mymd5}");

            bool backed_up = false;
            foreach (var file in Directory.EnumerateFiles(savegameBackupDir, "*.sav"))
            {
                var thismd5 = str(MD5.HashData(File.ReadAllBytes(file)));
                if (thismd5 == mymd5)
                {
                    backed_up = true;
                    Console.WriteLine($"Backup already present under {file}");
                    break;
                }
            }

            if (!backed_up)
            {
                string backupName = Path.Combine(savegameBackupDir, $"0Backup_{DateTime.Now:yyyyMMdd_HHmmss}.sav");
                File.Copy(savefile.FileName, backupName, false);
                Console.WriteLine($"A Backup copy of the Savegame in slot 0 was made under: {backupName}");
                MessageBox.Show($"A Backup copy of the Savegame in slot 0 was made under: {backupName}");
            }
        }
        else
        {
            Console.WriteLine("The Savegame was not compressed. This implies that the file was last modified by this tool. No Backup will be created.");
        }

        try
        {

            string workingFilePath = Path.Combine(savegameDir, "CurrentWorkingFile.sav");
            savefile.WriteSavegameTo(workingFilePath);
            Console.WriteLine($"An uncompressed version of the savegame in slot 0 has been created under: {workingFilePath}");
            Console.WriteLine("");

            var patcher = new SaveFilePatcher(workingFilePath);
            var mgr = new SavegameModifier(savefile, patcher);

            SaveEditor se = new(patcher, mgr, savegameDir);
            se.ShowDialog();
            if (se.WasSuccesful)
            {
                Console.WriteLine("Patching was successful; Copying working file to Slot 0...");
                File.Copy(workingFilePath, Slot0Filename, true);

                if (se.LaunchGame)
                {
                    string url = "steam://launch/1902960/dialog";
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });

                    }
                    catch (Exception) { }
                }
                else
                {
                    MessageBox.Show("Your save game was successfully patched.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
            MessageBox.Show($"An error occured: {ex.Message}");
            return -1;
        }

        return 0;
    }
}

