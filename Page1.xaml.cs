using GDotnet.Reader.Api.DAL;
using GDotnet.Reader.Api.Protocol.Gx;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private DispatcherTimer _timer;

        private ObservableCollection<TagStatus> _removedTags = new();
        public ObservableCollection<TagStatus> RemovedTags
        {
            get => _removedTags;
            set
            {
                if (_removedTags != value)
                {
                    _removedTags = value;
                    OnPropertyChanged(nameof(RemovedTags));
                }
            }
        }

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
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(async () =>
            {
                await StartRfidReaderWithRetryAsync();
                _timer.Start();
            }));
        }

        /// <summary>
        /// Starts the RFID reader with retry logic
        /// </summary>
        private async Task StartRfidReaderWithRetryAsync(int maxRetries = 3)
        {
            int retries = maxRetries;
            while (retries-- > 0 && !_isConnected)
            {
                CleanupClient();
                await Task.Delay(200); // allow port to free up
                StartRfidReader();
            }

            if (!_isConnected)
            {
                MessageBox.Show("Unable to connect to RFID reader after multiple attempts.");
            }
        }

        /// <summary>
        /// Starts the RFID reader
        /// </summary>
        private void StartRfidReader()
        {
            try
            {
                _client = new GClient();
                _client.OnEncapedTagEpcLog += OnTagRead;
                _client.OnEncapedTagEpcOver += OnTagReadComplete;

                if (_client.OpenSerial("COM5:115200", 3000, out var status))
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
                    _isConnected = false;
                    Debug.WriteLine("Failed to open serial port for RFID reader.");
                }
            }
            catch (Exception ex)
            {
                _isConnected = false;
                MessageBox.Show("RFID initialization error: " + ex.Message);
            }
        }

        /// <summary>
        /// Cleans up the RFID client safely
        /// </summary>
        private void CleanupClient()
        {
            if (_client != null)
            {
                try
                {
                    _client.OnEncapedTagEpcLog -= OnTagRead;
                    _client.OnEncapedTagEpcOver -= OnTagReadComplete;

                    if (_isConnected)
                    {
                        _client.SendSynMsg(new MsgBaseStop());
                        _client.Close();
                        _isConnected = false;
                    }

                    _client = null;
                    Debug.WriteLine("RFID client cleaned up.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error closing RFID connection: " + ex.Message);
                }
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
                        MissCount = 0,
                        Name = GetName(epc)
                    };
                }

                var removed = RemovedTags.FirstOrDefault(t => t.EPC == epc);
                if (removed != null)
                    RemovedTags.Remove(removed);
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
            Debug.WriteLine("Page unloading, cleaning up RFID reader...");

            // Stop timer first
            _timer.Tick -= CheckForRemovedTags;
            _timer.Stop();

            // Serialize active tags for debug
            if (_activeTags.Count > 0)
            {
                string json = JsonSerializer.Serialize(_activeTags.Values, new JsonSerializerOptions { WriteIndented = true });
                Debug.WriteLine(json);

            }

            // Cleanup client
            CleanupClient();
        }

        private void GoToSecondPage_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
                this.NavigationService.Navigate(new Page2());
            else
                MessageBox.Show("NavigationService is null. Are you using a Frame?");
        }

        public string GetName(string tag)
        {
            string readText = File.ReadAllText(@"C:\Users\Aleksa\Desktop\articles.json");  // Read the contents of the file

            List<Article> articles = JsonSerializer.Deserialize<List<Article>>(readText);

            var article = articles.FirstOrDefault(a => a.tagCode == tag);
            if (article != null)
            {
                return article.product.name;
            }

            return $" {tag} Page1";
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TagStatus : INotifyPropertyChanged
    {
        private string _epc;
        public string EPC { get => _epc; set { _epc = value; OnPropertyChanged(nameof(EPC)); } }

        private string _tid;
        public string TID { get => _tid; set { _tid = value; OnPropertyChanged(nameof(TID)); } }

        private string _antenna;
        public string Antenna { get => _antenna; set { _antenna = value; OnPropertyChanged(nameof(Antenna)); } }

        private DateTime _firstSeen;
        public DateTime FirstSeen { get => _firstSeen; set { _firstSeen = value; OnPropertyChanged(nameof(FirstSeen)); } }

        private DateTime _lastSeen;
        public DateTime LastSeen { get => _lastSeen; set { _lastSeen = value; OnPropertyChanged(nameof(LastSeen)); } }

        private int _missCount;
        public int MissCount { get => _missCount; set { _missCount = value; OnPropertyChanged(nameof(MissCount)); } }

        private string _name;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(nameof(Name)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
