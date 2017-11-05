using System;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using MiniTwitter.Extensions;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("status")]
    public class Status : PropertyChangedBase, ITwitterItem
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
                    if (!value.IsNullOrEmpty())
                    {
                        _createdAtInternal = value;
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

        private ulong _id;

        [XmlElement("id")]
        public ulong Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged("ID");
                }
            }
        }

        private string text;

        [XmlElement("text")]
        public string Text
        {
            get { return text; }
            set
            {
                if (text != value)
                {
                    text = TwitterClient.Unescape(value);
                    OnPropertyChanged("Text");
                }
            }
        }

        private string _source;

        [XmlElement("source")]
        public string Source
        {
            get { return _source; }
            set
            {
                if (_source != value)
                {
                    if (!value.IsNullOrEmpty())
                    {
                        _source = Regex.Replace(value, @"<(.|\n)*?>", string.Empty);
                        OnPropertyChanged("Source");
                    }
                }
            }
        }

        private bool _favorited;

        [XmlElement("favorited")]
        public bool Favorited
        {
            get { return _favorited; }
            set
            {
                if (_favorited != value)
                {
                    _favorited = value;
                    OnPropertyChanged("Favorited");
                }
            }
        }

        private ulong _inReplyToStatusId;

        [XmlIgnore]
        public ulong InReplyToStatusId
        {
            get { return _inReplyToStatusId; }
            set
            {
                if (_inReplyToStatusId != value)
                {
                    _inReplyToStatusId = value;
                    OnPropertyChanged("InReplyToStatusID");
                }
            }
        }

        private string _inReplyToStatusIdInternal;

        [XmlElement("in_reply_to_status_id", IsNullable = true)]
        public string InReplyToStatusIdInternal
        {
            get { return _inReplyToStatusIdInternal; }
            set
            {
                if (_inReplyToStatusIdInternal != value)
                {
                    _inReplyToStatusIdInternal = value;
                    if (!_inReplyToStatusIdInternal.IsNullOrEmpty())
                    {
                        InReplyToStatusId = ulong.Parse(_inReplyToStatusIdInternal);
                    }
                }
            }
        }

        private long _inReplyToUserId;

        [XmlIgnore]
        public long InReplyToUserId
        {
            get { return _inReplyToUserId; }
            set
            {
                if (_inReplyToUserId != value)
                {
                    _inReplyToUserId = value;
                    OnPropertyChanged("InReplyToUserID");
                }
            }
        }

        private string _inReplyToUserIdInternal;

        [XmlElement("in_reply_to_user_id", IsNullable = true)]
        public string InReplyToUserIdInternal
        {
            get { return _inReplyToUserIdInternal; }
            set
            {
                if (_inReplyToUserIdInternal != value)
                {
                    _inReplyToUserIdInternal = value;
                    if (!_inReplyToUserIdInternal.IsNullOrEmpty())
                    {
                        InReplyToUserId = long.Parse(_inReplyToUserIdInternal);
                    }
                }
            }
        }

        private string _inReplyToScreenName;

        [XmlElement("in_reply_to_screen_name", IsNullable = true)]
        public string InReplyToScreenName
        {
            get { return _inReplyToScreenName; }
            set
            {
                if (_inReplyToScreenName != value)
                {
                    _inReplyToScreenName = value;
                    OnPropertyChanged("InReplyToScreenName");
                }
            }
        }

        private Status _retweetedStatus;

        [XmlElement("retweeted_status")]
        public Status ReTweetedStatus
        {
            get { return _retweetedStatus; }
            set
            {
                if (_retweetedStatus != value)
                {
                    _retweetedStatus = value;
                    OnPropertyChanged("ReTweetedStatus");
                    OnPropertyChanged("IsReTweeted");
                }
            }
        }

        private User _sender;

        [XmlElement("user")]
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

        [XmlIgnore]
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
        public bool IsReply
        {
            get { return InReplyToStatusId != 0; }
        }

        public bool IsMention { get; set; }

        [XmlIgnore]
        public bool IsMessage
        {
            get { return false; }
        }

        [XmlIgnore]
        public bool IsReTweeted
        {
            get { return _retweetedStatus != null; }
        }

        public bool Equals(ITwitterItem other)
        {
            if (other == null)
            {
                return false;
            }
            else if (!(other is Status))
            {
                return false;
            }
            if (((Status)other).IsReTweeted && IsReTweeted)
            {
                return (ReTweetedStatus.Id == ((Status)other).ReTweetedStatus.Id);
            }
            else if (((Status)other).IsReTweeted)
            {
                return (((Status)other).ReTweetedStatus.Id == this.Id);
            }
            else if (IsReTweeted)
            {
                return (ReTweetedStatus.Id == other.Id);
            }
            return (this.Id == other.Id);
        }

        public override int GetHashCode()
        {
            return this._id.GetHashCode();
        }
    }
}
