using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using System.Resources;
using System.Windows;
using System.Threading;
using System.Windows.Media;
using System.Windows.Controls;

namespace MemoryManage
{
    public class MainWindowViewModel : ObservableObject
    {
        public MainWindowViewModel()
        {
            SelectedIndex = 0;
            VirtualPageSize = 5;
            ButtonEnabled = true;
            StartRunText = "开始运行";
            ThreadSleepTime = 1000;

            InputSerial = "";
            NowInVirtualPage = "";
            NowOutVirtualPage = "";

            Pages = new();
            Pages.Add(new(0));
            Pages.Add(new(1));
            Pages.Add(new(2));

            PageStack = new();
            VirtualPageSerial = new();
            Logs = new();
            PageSnapshot = new();
        }

        public int ThreadSleepTime { get; set; }

        public ObservableCollection<Page> Pages { get; set; }

        public ObservableCollection<Page> PageStack { get; set; }

        public ObservableCollection<Page> VirtualPageSerial { get; set; }

        public ObservableCollection<Log> Logs { get; set; }

        public ObservableCollection<Snapshot> PageSnapshot { get; set ; }

        private int selectedIndex;

        public int SelectedIndex
        {
            get => selectedIndex;
            set => SetProperty(ref selectedIndex, value);
        }

        private int virtualPageSize;

        public int VirtualPageSize
        {
            get => virtualPageSize;
            set => SetProperty(ref virtualPageSize, value);
        }

        private int pageFaultTimes;

        public int PageFaultTimes
        {
            get => pageFaultTimes;
            set => SetProperty(ref pageFaultTimes, value);
        }

        private float pageFaultRate;

        public float PageFaultRate
        {
            get => pageFaultRate;
            set => SetProperty(ref pageFaultRate, value);
        }

        private bool buttonEnabled;

        public bool ButtonEnabled
        {
            get => buttonEnabled;
            set => SetProperty(ref buttonEnabled, value);
        }

        private string startRunText;

        public string StartRunText
        {
            get => startRunText;
            set => SetProperty(ref startRunText, value);
        }

        private string nowInVirtualPage;

        public string NowInVirtualPage
        {
            get => nowInVirtualPage;
            set => SetProperty(ref nowInVirtualPage, value);
        }

        private string nowOutVirtualPage;

        public string NowOutVirtualPage
        {
            get => nowOutVirtualPage;
            set => SetProperty(ref nowOutVirtualPage, value);
        }

        private string inputSerial;

        public string InputSerial
        {
            get => inputSerial;
            set => SetProperty(ref inputSerial, value);
        }

        private RelayCommand addPage;
        public ICommand AddPage => addPage ??= new RelayCommand(PerformAddPage);

        private void PerformAddPage()
        {
            Pages.Add(new(Pages.Count));

        }

        private RelayCommand deletePage;
        public ICommand DeletePage => deletePage ??= new RelayCommand(PerformDeletePage);

        private void PerformDeletePage()
        {
            if (Pages.Count > 1)
            {
                Pages.RemoveAt(Pages.Count - 1);
            }
        }

        private RelayCommand randomInput;
        public ICommand RandomInput => randomInput ??= new RelayCommand(PerformRandomInput);

        private void PerformRandomInput()
        {
            Random random = new();
            InputSerial = "0";
            int size = random.Next(10, 50);
            for (int i = 0; i < size; i++)
            {
                InputSerial += " " + random.Next(0, VirtualPageSize).ToString();
            }
        }

        private RelayCommand startRun;
        public ICommand StartRun => startRun ??= new RelayCommand(PerformStartRun);

        private void PerformStartRun()
        {
            Thread thread;
            try
            {
                // 尚未运行
                if (StartRunText == "开始运行")
                {
                    // 清空虚页序列
                    VirtualPageSerial.Clear();
                    // 清空实页内容
                    foreach (var page in Pages)
                    {
                        page.Vid = "";
                    }
                    // 清空日志
                    Logs.Clear();
                    // 清空快照
                    PageSnapshot.Clear();

                    // 重置缺页次数和缺页率
                    PageFaultTimes = 0;
                    PageFaultRate = 0;

                    // 虚页序列转换
                    foreach (var virtualPage in InputSerial.Split(' '))
                    {
                        int vid = int.Parse(virtualPage);
                        if (vid >= VirtualPageSize)
                        {
                            throw new("输入的虚页号超出虚页数");
                        }
                        VirtualPageSerial.Add(new(vid, 0));
                    }

                    // 输入可用性修改
                    ButtonEnabled = false;
                    StartRunText = "跳过动画";

                    // 初始化页面栈
                    PageStack.Clear();

                    if (SelectedIndex == 0)
                    {
                        thread = new(LRU);
                        thread.Start();
                    }
                    else
                    {
                        thread = new(FIFO);
                        thread.Start();
                    }
                }
                // 运行中
                else
                {
                    // 输入可用性修改
                    ButtonEnabled = true;
                    StartRunText = "开始运行";

                    // 设置线程速度
                    ThreadSleepTime = 0;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        private void LRU()
        {
            for (int i = 0; i < VirtualPageSerial.Count; i++)
            {
                Thread.Sleep(ThreadSleepTime);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 创建快照
                    PageSnapshot.Add(new());

                    // 当前调入虚页号
                    NowInVirtualPage = VirtualPageSerial[i].Vid;
                    // 当前调出虚页号
                    NowOutVirtualPage = "";

                    bool inPage = false;
                    // 命中
                    for (int j = 0; j < PageStack.Count; j++)
                    {
                        if (PageStack[j].Vid == VirtualPageSerial[i].Vid)
                        {
                            // 缺页次数和缺页率
                            PageFaultRate = (float)PageFaultTimes / (i + 1);

                            // 实页表
                            Pages[PageStack[j].IntPid].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack[j].IntPid);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, PageStack[j].Pid, true));

                            // 页面栈
                            var temp = PageStack[j];
                            PageStack.RemoveAt(j);
                            PageStack.Insert(0, temp);

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count<Pages.Count)
{
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }

                            inPage = true;
                            break;
                        }
                    }
                    // 未命中
                    if (!inPage)
                    {
                        // 缺页次数和缺页率
                        PageFaultTimes++;
                        PageFaultRate = (float)PageFaultTimes / (i + 1);

                        // 还有物理页
                        if (PageStack.Count < Pages.Count)
                        {
                            // 实页表
                            Pages[PageStack.Count].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack.Count);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, Pages[PageStack.Count].Pid, false));

                            // 页面栈
                            PageStack.Insert(0, new(PageStack.Count));
                            PageStack[0].Vid = VirtualPageSerial[i].Vid;

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count < Pages.Count)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }
                            PageSnapshot[i].PageFault = "X";
                        }
                        // 物理页用完，替换
                        else
                        {
                            // 当前调出虚页号
                            NowOutVirtualPage = PageStack[^1].Vid;

                            // 实页表
                            Pages[PageStack[^1].IntPid].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack[^1].IntPid);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, NowOutVirtualPage, Pages[PageStack[^1].IntPid].Pid));

                            // 页面栈
                            PageStack[^1].Vid = VirtualPageSerial[i].Vid;
                            var temp = PageStack[^1];
                            PageStack.RemoveAt(PageStack.Count - 1);
                            PageStack.Insert(0, temp);

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count < Pages.Count)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }
                            PageSnapshot[i].PageFault = "X";
                        }
                    }
                });
            }

            // 运行完毕
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 输入可用性修改
                ButtonEnabled = true;
                StartRunText = "开始运行";

                // 设置线程速度
                ThreadSleepTime = 1000;
            });
        }

        private void HighLightPage(int intPid)
        {
            Pages[intPid].PageColor = new(Color.FromArgb(0xFF, 150, 180, 203));
            Thread thread = new(() =>
            {
                Thread.Sleep(ThreadSleepTime / 2);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Pages[intPid].PageColor = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                });
            });
            thread.Start();
        }

        private void FIFO()
        {
            for (int i = 0; i < VirtualPageSerial.Count; i++)
            {
                Thread.Sleep(ThreadSleepTime);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 创建快照
                    PageSnapshot.Add(new());

                    // 当前调入虚页号
                    NowInVirtualPage = VirtualPageSerial[i].Vid;
                    // 当前调出虚页号
                    NowOutVirtualPage = "";

                    bool inPage = false;
                    // 命中
                    for (int j = 0; j < PageStack.Count; j++)
                    {
                        if (PageStack[j].Vid == VirtualPageSerial[i].Vid)
                        {
                            // 缺页次数和缺页率
                            PageFaultRate = (float)PageFaultTimes / (i + 1);

                            // 实页表
                            Pages[PageStack[j].IntPid].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack[j].IntPid);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, PageStack[j].Pid, true));

                            // 页面队列
                            //// 页面栈
                            //var temp = PageStack[j];
                            //PageStack.RemoveAt(j);
                            //PageStack.Insert(0, temp);

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count < Pages.Count)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }

                            inPage = true;
                            break;
                        }
                    }
                    // 未命中
                    if (!inPage)
                    {
                        // 缺页次数和缺页率
                        PageFaultTimes++;
                        PageFaultRate = (float)PageFaultTimes / (i + 1);

                        // 还有物理页
                        if (PageStack.Count < Pages.Count)
                        {
                            // 实页表
                            Pages[PageStack.Count].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack.Count);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, Pages[PageStack.Count].Pid, false));

                            // 页面队列
                            //// 页面栈
                            PageStack.Insert(0, new(PageStack.Count));
                            PageStack[0].Vid = VirtualPageSerial[i].Vid;

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count < Pages.Count)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }
                            PageSnapshot[i].PageFault = "X";
                        }
                        // 物理页用完，替换
                        else
                        {
                            // 当前调出虚页号
                            NowOutVirtualPage = PageStack[^1].Vid;

                            // 实页表
                            Pages[PageStack[^1].IntPid].Vid = VirtualPageSerial[i].Vid;
                            HighLightPage(PageStack[^1].IntPid);

                            // 日志
                            Logs.Add(new(Logs.Count, NowInVirtualPage, NowOutVirtualPage, Pages[PageStack[^1].IntPid].Pid));

                            // 页面队列
                            PageStack[^1].Vid = VirtualPageSerial[i].Vid;
                            //// 页面栈
                            var temp = PageStack[^1];
                            PageStack.RemoveAt(PageStack.Count - 1);
                            PageStack.Insert(0, temp);

                            // 快照
                            foreach (var page in PageStack)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new(page.Vid));
                            }
                            while (PageSnapshot[i].SingleSnapshots.Count < Pages.Count)
                            {
                                PageSnapshot[i].SingleSnapshots.Add(new("   "));
                            }
                            PageSnapshot[i].PageFault = "X";
                        }
                    }
                });
            }

            // 运行完毕
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 输入可用性修改
                ButtonEnabled = true;
                StartRunText = "开始运行";

                // 设置线程速度
                ThreadSleepTime = 1000;
            });
        }

        private RelayCommand accelerateRun;
        public ICommand AccelerateRun => accelerateRun ??= new RelayCommand(PerformAccelerateRun);

        private void PerformAccelerateRun()
        {
            if (ThreadSleepTime > 400)
            {
                ThreadSleepTime -= 200;
            }
        }
    }

    public class Snapshot : ObservableObject
    {
        public Snapshot()
        {
            SingleSnapshots = new();
            PageFault = "";
        }

        public ObservableCollection<SingleSnapshot> SingleSnapshots { get; set; }

        private string pageFault;

        public string PageFault
        {
            get => pageFault;
            set => SetProperty(ref pageFault, value);
        }
    }

    public class SingleSnapshot: ObservableObject
    {
        public SingleSnapshot(string id)
        {
            Vid = id[2..];
        }

        private string vid;

        public string Vid
        {
            get => vid;
            set => SetProperty(ref vid, value);
        }
    }

    public class Log : ObservableObject
    {
        public Log(int i, string inVid, string outVid, string Pid)
        {
            Logs = i.ToString() + ".      当前调入    " + inVid + "    缺页    换出位于    "
                + Pid + "  的  " + outVid;
            if (i >= 10)
            {
                Logs = i.ToString() + ".    当前调入    " + inVid + "    缺页    换出位于    "
                + Pid + "  的  " + outVid;
            }
        }

        public Log(int i, string inVid, string Pid, bool hit)
        {
            if (hit)
            {
                Logs = i.ToString() + ".      当前调入    " + inVid + "    命中    位于    " + Pid;
            }
            else
            {
                Logs = i.ToString() + ".      当前调入    " + inVid + "    缺页    调入    " + Pid;
            }
            if (i >= 10)
            {
                if (hit)
                {
                    Logs = i.ToString() + ".    当前调入    " + inVid + "    命中    位于    " + Pid;
                }
                else
                {
                    Logs = i.ToString() + ".    当前调入    " + inVid + "    缺页    调入    " + Pid;
                }
            }
        }

        private string logs;

        public string Logs
        {
            get => logs;
            set => SetProperty(ref logs, value);
        }
    }

    public class Page : ObservableObject
    {
        public Page(int id)
        {
            IntPid = id;
            Pid = "实页" + id.ToString();
            Vid = "";
            PageColor = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        }

        public Page(int id, int i)
        {
            Vid = "虚页" + id.ToString();
            Pid = "";
            PageColor = new(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
        }

        private int intPid;

        public int IntPid
        {
            get => intPid;
            set => SetProperty(ref intPid, value);
        }

        private string pid;

        public string Pid
        {
            get => pid;
            set => SetProperty(ref pid, value);
        }

        private string vid;

        public string Vid
        {
            get => vid;
            set => SetProperty(ref vid, value);
        }

        private SolidColorBrush pageColor;

        public SolidColorBrush PageColor
        {
            get => pageColor;
            set => SetProperty(ref pageColor, value);
        }
    }
}
