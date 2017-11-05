using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;
using MiniTwitter.Extensions;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("user")]
    public class User : PropertyChangedBase, IEquatable<User>
    {
        static User()
        {
            _networkAvailable = NetworkInterface.GetIsNetworkAvailable();
            NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(NetworkChange_NetworkAvailabilityChanged);
        }

        static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            _networkAvailable = e.IsAvailable;

            if (_networkAvailable)
            {
                lock (_iconCache)
                {
                    var keys = _iconCache.Where(p => p.Value == null).Select(p => p.Key);
                    foreach (var key in keys)
                    {
                        _iconCache.Remove(key);
                    }
                }
            }
        }

        private int id;

        [XmlElement("id")]
        public int Id
        {
            get { return id; }
            set
            {
                if (id != value)
                {
                    id = value;
                    OnPropertyChanged("ID");
                }
            }
        }

        private string _name;

        [XmlElement("name")]
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        private string _screenName;

        [XmlElement("screen_name")]
        public string ScreenName
        {
            get { return _screenName; }
            set
            {
                if (_screenName != value)
                {
                    _screenName = value;
                    OnPropertyChanged("ScreenName");
                }
            }
        }

        private string _location;

        [XmlElement("location")]
        public string Location
        {
            get { return _location; }
            set
            {
                if (_location != value)
                {
                    _location = value;
                    OnPropertyChanged("Location");
                }
            }
        }

        private string _description;

        [XmlElement("description")]
        public string Description
        {
            get { return _description; }
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged("Description");
                }
            }
        }

        private string _imageUrl;

        [XmlElement("profile_image_url")]
        public string ImageUrl
        {
            get { return _imageUrl; }
            set
            {
                if (_imageUrl != value)
                {
                    _imageUrl = value;
                    OnPropertyChanged("ImageUrl");
                }
            }
        }

        [NonSerialized]
        private ImageSource _icon;

        private int _retry = 0;

        private static volatile bool _networkAvailable;

        private static readonly Dictionary<string, List<User>> _processUsers = new Dictionary<string, List<User>>();
        private static readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>();

        [XmlIgnore]
        public ImageSource Icon
        {
            get
            {
                if (_icon == null)
                {
                    lock (_iconCache)
                    {
                        if (_iconCache.ContainsKey(_imageUrl))
                        {
                            _icon = _iconCache[_imageUrl];

                            if (_icon == null)
                            {
                                lock (_processUsers)
                                {
                                    // アイコンダウンロードを予約
                                    if (!_processUsers.ContainsKey(_imageUrl))
                                    {
                                        _processUsers.Add(_imageUrl, new List<User>());
                                    }
                                    _processUsers[_imageUrl].Add(this);
                                }

                                _iconCache.Remove(_imageUrl);
                            }
                        }
                        else
                        {
                            if (_retry > 5)
                            {
                                return null;
                            }

                            _iconCache.Add(_imageUrl, null);

                            if (_networkAvailable)
                            {
                                ThreadPool.QueueUserWorkItem(state =>
                                    {
                                        try
                                        {
                                            using (var client = new WebClient())
                                            {
                                                var data = client.DownloadData(_imageUrl);
                                                var stream = new MemoryStream(data);
                                                var bitmap = new BitmapImage();
                                                bitmap.BeginInit();
                                                bitmap.StreamSource = stream;
                                                bitmap.DecodePixelHeight = 48;
                                                bitmap.DecodePixelWidth = 48;
                                                bitmap.EndInit();

                                                bitmap.Freeze();

                                                lock (_iconCache)
                                                {
                                                    _iconCache[_imageUrl] = bitmap;
                                                }

                                                App.Current.AsyncInvoke(p =>
                                                {
                                                    Icon = p;
                                                    lock (_processUsers)
                                                    {
                                                        List<User> users;
                                                        if (_processUsers.TryGetValue(_imageUrl, out users))
                                                        {
                                                            foreach (var item in users)
                                                            {
                                                                item.Icon = p;
                                                            }
                                                            _processUsers.Remove(_imageUrl);
                                                        }
                                                    }
                                                }, bitmap, System.Windows.Threading.DispatcherPriority.Background);
                                            }
                                        }
                                        catch
                                        {
                                            lock (_iconCache)
                                            {
                                                _iconCache.Remove(_imageUrl);
                                                _retry++;
                                            }
                                        }
                                    });
                            }
                        }
                    }
                }
                return _icon;
            }
            set
            {
                _icon = value;
                OnPropertyChanged("Icon");
            }
        }

        private string _url;

        [XmlElement("url")]
        public string Url
        {
            get { return _url; }
            set
            {
                if (_url != value)
                {
                    _url = value;
                    OnPropertyChanged("Url");
                }
            }
        }

        private bool _protected;

        [XmlElement("protected")]
        public bool Protected
        {
            get { return _protected; }
            set
            {
                if (_protected != value)
                {
                    _protected = value;
                    OnPropertyChanged("Protected");
                }
            }
        }

        private Status _status;

        [XmlElement("status")]
        public Status Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                }
            }
        }

        public bool Equals(User other)
        {
            if (other == null)
            {
                return false;
            }
            return (this.Id == other.Id);
        }

        public override int GetHashCode()
        {
            return this.Id;
        }
    }
}
