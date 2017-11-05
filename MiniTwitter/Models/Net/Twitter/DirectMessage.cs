using System;
using System.Xml.Serialization;
using MiniTwitter.Extensions;
using MiniTwitter.Models;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("direct_message")]
    public class DirectMessage : PropertyChangedBase, ITwitterItem
    {
        private DateTime _createdAt;

        [XmlIgnore]
        public DateTime CreatedAt
        {
            get { return _createdAt; }
            set
            {
                if (_createdAt != value)
                {
                    _createdAt = value;
                    OnPropertyChanged("CreatedAt");
                }
            }
        }

        private string _createdAtInternal;

        [XmlElement("created_at")]
        public string CreatedAtInternal
        {
            get { return _createdAtInternal; }
            set
            {
                if (_createdAtInternal != value)
                {
                    _createdAtInternal = value;
                    if (!string.IsNullOrEmpty(_createdAtInternal))
                    {
                        CreatedAt = TwitterExtension.ParseDateTime(_createdAtInternal);
                    }
                }
            }
        }

        private string _relativeTime;

        [XmlIgnore]
        public string RelativeTime
        {
            get
            {
                this.UpdateRelativeTime();
                return _relativeTime;
            }
            set
            {
                if (_relativeTime != value)
                {
                    _relativeTime = value;
                }
            }
        }

        private ulong id;

        [XmlElement("id")]
        public ulong Id
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

        private string _text;

        [XmlElement("text")]
        public string Text
        {
            get { return _text; }
            set
            {
                if (_text != value)
                {
                    _text = TwitterClient.Unescape(value);
                    OnPropertyChanged("Text");
                }
            }
        }

        private User _sender;

        [XmlElement("sender")]
        public User Sender
        {
            get { return _sender; }
            set
            {
                if (_sender != value)
                {
                    _sender = value;
                    OnPropertyChanged("Sender");
                }
            }
        }

        private User _recipient;

        [XmlElement("recipient")]
        public User Recipient
        {
            get { return _recipient; }
            set
            {
                if (_recipient != value)
                {
                    _recipient = value;
                    OnPropertyChanged("Recipient");
                }
            }
        }

        [XmlIgnore]
        public bool IsAuthor { get; set; }

        private bool _isNewest;

        [XmlIgnore]
        public bool IsNewest
        {
            get { return _isNewest; }
            set
            {
                if (_isNewest != value)
                {
                    _isNewest = value;
                    OnPropertyChanged("IsNewest");
                }
            }
        }

        [XmlIgnore]
        public bool IsMessage
        {
            get { return true; }
        }

        [XmlIgnore]
        public bool IsReTweeted
        {
            get { return false; }
        }

        public bool Equals(ITwitterItem other)
        {
            if (other == null)
            {
                return false;
            }
            else if (!(other is DirectMessage))
            {
                return false;
            }
            return (this.Id == other.Id);
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
