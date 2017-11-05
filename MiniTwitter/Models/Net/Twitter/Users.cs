using System;
using System.Xml.Serialization;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("users")]
    public class Users
    {
        [XmlElement("user")]
        public User[] User { get; set; }

        public static readonly User[] Empty = new User[0];
    }
}
