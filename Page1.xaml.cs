using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace VendingKioskUI
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>



    public partial class Page1 : Page, INotifyPropertyChanged
    {
        private GClient _client;
        private bool _isConnected = false;

        private Dictionary<string, TagStatus> _activeTags = new();
        private TimeSpan _missThreshold = TimeSpan.FromSeconds(2);
        private int _maxMissCount = 3;

        public ObservableCollection<TagStatus> RemovedTags { get; set; } = new();

        private DispatcherTimer _timer;

        public Page1()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += Page1_Loaded;
            Unloaded += Page1_Unloaded;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += CheckForRemovedTags;
        }

        private void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            // Defer RFID reader start until after visual tree is fully loaded
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                StartRfidReader();
                _timer.Start();
            }));
        }

        private void StartRfidReader()
        {
            try
            {
                _client = new GClient();
                _client.OnEncapedTagEpcLog += OnTagRead;
                _client.OnEncapedTagEpcOver += OnTagReadComplete;

                if (_client.OpenSerial("COM3:115200", 3000, out var status))
                {
                    _isConnected = true;

                    try
                    {
                        _client.SendSynMsg(new MsgBaseStop());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error sending stop: " + ex.Message);
                    }

                    var msgInventory = new MsgBaseInventoryEpc
                    {
                        AntennaEnable = (uint)(eAntennaNo._1 | eAntennaNo._2 | eAntennaNo._3 | eAntennaNo._4),
                        InventoryMode = (byte)eInventoryMode.Inventory,
                        ReadTid = new ParamEpcReadTid
                        {
                            Mode = (byte)eParamTidMode.Auto,
                            Len = 6
                        }
                    };

                    try
                    {
                        _client.SendSynMsg(msgInventory);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error sending inventory message: " + ex.Message);
                    }

                    if (msgInventory.RtCode != 0)
                        MessageBox.Show("Failed to start inventory.");
                }
                else
                {
                    MessageBox.Show("Failed to connect to reader.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("RFID initialization error: " + ex.Message);
            }
        }

        private void OnTagRead(EncapedLogBaseEpcInfo msg)
        {
            if (msg == null || msg.logBaseEpcInfo == null || msg.logBaseEpcInfo.Result != 0)
                return;

            string epc = msg.logBaseEpcInfo.Epc;
            string tid = msg.logBaseEpcInfo.Tid;
            string antenna = msg.logBaseEpcInfo.AntId.ToString();
            DateTime now = DateTime.Now;

            Dispatcher.Invoke(() =>
            {
                if (_activeTags.TryGetValue(epc, out var existing))
                {
                    existing.LastSeen = now;
                    existing.MissCount = 0;
                }
                else
                {
                    _activeTags[epc] = new TagStatus
                    {
                        EPC = epc,
                        TID = tid,
                        Antenna = antenna,
                        FirstSeen = now,
                        LastSeen = now,
                        MissCount = 0
                    };
                }

                var removed = RemovedTags.FirstOrDefault(t => t.EPC == epc);
                if (removed != null)
                {
                    RemovedTags.Remove(removed);
                }
            });
        }

        private void OnTagReadComplete(EncapedLogBaseEpcOver msg)
        {
            // Handle completed tag read logic (e.g., payment)
        }

        private void CheckForRemovedTags(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;

            foreach (var tag in _activeTags.Values.ToList())
            {
                TimeSpan sinceLastSeen = now - tag.LastSeen;

                if (sinceLastSeen > _missThreshold)
                {
                    tag.MissCount++;

                    if (tag.MissCount >= _maxMissCount)
                    {
                        if (!RemovedTags.Any(t => t.EPC == tag.EPC))
                        {
                            RemovedTags.Add(tag);
                        }

                        _activeTags.Remove(tag.EPC);
                    }
                }
                else
                {
                    tag.MissCount = 0;
                }
            }
        }

        private void Page1_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Page is unloading, cleaning up RFID reader.");

            if (_isConnected && _client != null)
            {
                try
                {
                    _client.SendSynMsg(new MsgBaseStop());
                    _client.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error closing RFID connection: " + ex.Message);
                }
            }

            _timer?.Stop();
        }

        private void GoToSecondPage_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.Navigate(new Page2());
            }
            else
            {
                MessageBox.Show("NavigationService is null. Are you using a Frame?");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

}
