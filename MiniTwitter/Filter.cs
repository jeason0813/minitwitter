using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using MiniTwitter.Models.Net.Twitter;

namespace MiniTwitter
{
    [Serializable]
    public class Filter : PropertyChangedBase, IEditableObject, IEquatable<Filter>
    {
        private FilterType _type;

        [XmlAttribute]
        public FilterType Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged("Type");
                }
            }
        }

        private string _pattern;

        [XmlAttribute]
        public string Pattern
        {
            get { return _pattern; }
            set
            {
                if (_pattern != value)
                {
                    _pattern = value;
                    OnPropertyChanged("Pattern");
                }
            }
        }

        public bool Process(ITwitterItem item)
        {
            switch (Type)
            {
                case FilterType.Text:
                    return item.Text.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) != -1;
                case FilterType.RegexText:
                    return Regex.IsMatch(item.Text, Pattern, RegexOptions.IgnoreCase);
                case FilterType.Name:
                    return string.Compare(item.Sender.ScreenName, Pattern, true) == 0;
                case FilterType.RegexName:
                    return Regex.IsMatch(item.Sender.ScreenName, Pattern, RegexOptions.IgnoreCase);
                case FilterType.Source:
                    {
                        var status = item as Status;

                        if (status != null)
                        {
                            return string.Compare(status.Source, Pattern, true) == 0;
                        }
                    }
                    break;
                case FilterType.RegexSource:
                    {
                        var status = item as Status;

                        if (status != null)
                        {
                            return Regex.IsMatch(status.Source, Pattern, RegexOptions.IgnoreCase);
                        }
                    }
                    break;
            }
            return false;
        }

        private Filter _copy;

        public void BeginEdit()
        {
            if (_copy == null)
            {
                _copy = new Filter();
            }
            _copy.Pattern = this.Pattern;
            _copy.Type = this.Type;
        }

        public void CancelEdit()
        {
            this.Pattern = _copy.Pattern;
            this.Type = _copy.Type;
        }

        public void EndEdit()
        {
            _copy.Pattern = null;
            _copy.Type = FilterType.None;
        }

        public override int GetHashCode()
        {
            return _type.GetHashCode() ^ _pattern.GetHashCode();
        }

        #region IEquatable<Filter> メンバ

        public bool Equals(Filter other)
        {
            return _pattern == other._pattern && _type == other._type;
        }

        #endregion
    }
}
