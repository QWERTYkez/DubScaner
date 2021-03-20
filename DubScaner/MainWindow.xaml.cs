using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DubScaner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Title = "DubScaner v0.8";
        }

        string Path;
        string SizeStr;
        int SizeType;
        long Size;
        private readonly ThreadStart ZD;
        private Thread Process;
        private void Start(object sender, RoutedEventArgs e)
        {
            Path = SourceTB.Text;
            SizeStr = SizeTB.Text;
            SizeType = SizeTypeCB.SelectedIndex;

            Process = new Thread(Scan);

            Process.Start();
        }

        List<ListBoxItem> Items;
        private void Scan()
        {
            var name = Thread.CurrentThread.Name;

            Dispatcher.Invoke(() =>
            {
                StatusLB.Content = "";
                InfoLB1.Content = "";
                InfoLB2.Content = "";
            });

            DirectoryInfo Dir;

            #region Проверки
            try
            {
                Dir = new DirectoryInfo(Path);
            }
            catch
            {
                ShowError();
                return;
            }
            if (!Dir.Exists)
            {
                ShowError();
                return;
            }
            try
            {
                var Sz = Convert.ToDouble(SizeStr.Replace('.', ','));
                for (int i = 0; i < SizeType; i++) Sz *= 1024;
                Size = Convert.ToInt64(Sz);
            }
            catch
            {
                ShowError();
                return;
            }
            #endregion

            Items = new List<ListBoxItem>();

            Dispatcher.Invoke(() => 
            {
                FilesList.ItemsSource = null;
                StatusLB.Content = "Сканирование каталогов";
            });

            var aaa = DateTime.Now;

            AllFiles = Dir.GetFiles().ToList();
            ScanDirectories(Dir);

            var gfl = AllFiles.AsParallel().GroupBy(f => f.Name).Where(g => g.Count() > 1).OrderBy(g => g.Key).ToList();

            Dispatcher.Invoke(() =>
            {
                StatusLB.Content = "Составление списка";
                InfoLB1.Content = "";
                InfoLB2.Content = "";
            });

            for (int i = 0; i < gfl.Count; i++)
            {
                Dispatcher.Invoke(() =>
                {
                    Items.Add(new ListBoxItem { Content = "" });
                    Items.Add(new ListBoxItem { Content = $"-- {gfl[i].Key} --" });
                });

                foreach (var f in gfl[i])
                {
                    double L = f.Length;
                    string T = "b";

                    if (L > 1099511627776) { L = Math.Round(L / 1099511627776, 2); T = "Tb"; }
                    if (L > 1073741824) { L = Math.Round(L / 1073741824, 2); T = "Gb"; }
                    if (L > 1048576) { L = Math.Round(L / 1048576, 2); T = "Mb"; }
                    if (L > 1024) { L = Math.Round(L / 1024, 2); T = "Kb"; }

                    Dispatcher.Invoke(() =>
                    {
                        var LBI = new ListBoxItem
                        {
                            Tag = f,
                            Content = $"{L} {T}  ->  {f.FullName.Replace(f.Name, "")}"
                        };
                        LBI.MouseDoubleClick += LBI_MouseDoubleClick;
                        LBI.KeyDown += LBI_KeyDown;
                        Items.Add(LBI);
                    });
                }
            }

            if (Items.Count > 1)
                Dispatcher.Invoke(() => Items.RemoveAt(0));


            var bbb = DateTime.Now;

            var ab = bbb - aaa;

            Dispatcher.Invoke(() => 
            {
                FilesList.ItemsSource = Items;
                StatusLB.Content = "Sucessful!!!";
                InfoLB1.Content = $"Found {gfl.Count()} groups of duplicates";
                InfoLB2.Content = ab.ToString();
            });
        }

        private void LBI_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("explorer");
            proc.StartInfo.Arguments = $"/select, \"{((sender as ListBoxItem).Tag as FileInfo).FullName}\"";
            proc.Start();
        }

        private void LBI_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                FilesList.SelectedIndex = -1;
                var fi = (sender as ListBoxItem).Tag as FileInfo;
                fi.IsReadOnly = false;
                fi.Delete();
                Items.Remove(sender as ListBoxItem);
            }
        }

        private List<FileInfo> AllFiles = new List<FileInfo>();
        class Para
        {
            public Para(DirectoryInfo Dir)
            {
                n = 0; dirs = Dir.GetDirectories();
            }

            public int n;
            public DirectoryInfo[] dirs;
        }
        private void ScanDirectories(DirectoryInfo BaseDir)
        {
            var Tree = new List<Para> { new Para(BaseDir) };

        Next:

            try
            {
                while (Tree[0].dirs[Tree[0].n].GetDirectories().Length > 0)
                {
                    Tree.Insert(0, new Para(Tree[0].dirs[Tree[0].n]));
                    Dispatcher.Invoke(() =>
                    {
                        InfoLB1.Content = $"Глубина - {Tree.Count}";
                        InfoLB2.Content = $"Ширина - {0} / {Tree[0].dirs.Length}";
                    });
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Tree[0].n++;
                Dispatcher.Invoke(() =>
                {
                    InfoLB2.Content = $"Ширина - {Tree[0].n} / {Tree[0].dirs.Length}";
                });
                while (Tree[0].dirs.Length == Tree[0].n)
                {
                    Tree.RemoveAt(0);
                    if (Tree.Count > 0) Tree[0].n++;
                    else return;
                }
                goto Next;
            }

            try
            {
                AllFiles.AddRange(Tree[0].dirs[Tree[0].n].EnumerateFiles().Where(f => f.Length > Size));
                Dispatcher.Invoke(() =>
                {
                    InfoLB3.Content = AllFiles.Count;
                });
            }
            catch (UnauthorizedAccessException e)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusLB.Content = "ОШИБКА!!!";
                    InfoLB1.Content = "";
                    InfoLB2.Content = "UnauthorizedAccessException";
                });
            }

            Tree[0].n++;
            Dispatcher.Invoke(() =>
            {
                InfoLB2.Content = $"Ширина - {Tree[0].n} / {Tree[0].dirs.Length}";
            });
            while (Tree[0].dirs.Length == Tree[0].n)
            {
                Tree.RemoveAt(0);
                if (Tree.Count > 0) Tree[0].n++;
                else return;
            }
            goto Next;
        }

        class DirInfo
        {
            public DirInfo(DirectoryInfo dir)
            {
                Files = dir.EnumerateFiles();
                Count = Files.Count();
            }

            public IEnumerable<FileInfo> Files;
            public int Count;
        }

        private Task ShowError()
        {
            return Task.Run(() => 
            {
                Dispatcher.Invoke(() =>
                {
                    StatusLB.Content = "ошибка параметров";
                    InfoLB1.Content = "";
                    InfoLB2.Content = "";
                });
            });
        }

        private void Stop(object sender, RoutedEventArgs e)
        {
            if (Process != null)
            {
                if (Process.IsAlive)
                {
                    Process.Abort();
                    Dispatcher.Invoke(() =>
                    {
                        StatusLB.Content = "Остановлено";
                        InfoLB1.Content = "";
                        InfoLB2.Content = "";
                    });
                }   
            }
        }
    }
}
