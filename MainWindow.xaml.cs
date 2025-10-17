using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private GClient _client;
        private bool _isConnected = false;

        private Dictionary<string, TagStatus> _activeTags = new();
        private TimeSpan _missThreshold = TimeSpan.FromSeconds(2);
        private int _maxMissCount = 2;

        public ObservableCollection<TagStatus> RemovedTags { get; set; } = new();

        private DispatcherTimer _timer;
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            StartRfidReader();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += CheckForRemovedTags;
            _timer.Start();
        }


        private void StartRfidReader()
        {
            _client = new GClient();
            _client.OnEncapedTagEpcLog += OnTagRead;
            _client.OnEncapedTagEpcOver += OnTagReadComplete;

            if (_client.OpenSerial("COM3:115200", 3000, out var status))
            {
                _isConnected = true;
                _client.SendSynMsg(new MsgBaseStop());

                var msgInventory = new MsgBaseInventoryEpc
                {
                    AntennaEnable = (uint)(eAntennaNo._1 | eAntennaNo._2 | eAntennaNo._3 | eAntennaNo._4),
                    //AntennaEnable = (uint)(eAntennaNo._1),
                    InventoryMode = (byte)eInventoryMode.Inventory,
                    ReadTid = new ParamEpcReadTid
                    {
                        Mode = (byte)eParamTidMode.Auto,
                        Len = 6
                    }
                };

                _client.SendSynMsg(msgInventory);
                if (msgInventory.RtCode != 0)
                    MessageBox.Show("Failed to start inventory.");
            }
            else
            {
                MessageBox.Show("Failed to connect to reader.");
            }
        }

        private void OnTagRead(EncapedLogBaseEpcInfo msg)
        {
            if (msg == null || msg.logBaseEpcInfo.Result != 0) return;

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

        private void OnTagReadComplete(EncapedLogBaseEpcOver msg)
        {
            // Ovde ce biti logika vezana za naplatu
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_isConnected)
            {
                try
                {
                    _client.SendSynMsg(new MsgBaseStop());
                    _client.Close();
                }
                catch { }
            }

            _timer?.Stop();
        }

        public event PropertyChangedEventHandler PropertyChanged;

    }
}