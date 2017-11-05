using System;
using System.Xml.Serialization;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("direct-messages")]
    public class DirectMessages
    {
        [XmlElement("direct_message")]
        public DirectMessage[] DirectMessage { get; set; }

        public static readonly DirectMessage[] Empty = new DirectMessage[0];
    }
}
