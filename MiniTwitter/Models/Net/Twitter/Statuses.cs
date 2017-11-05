using System;
using System.Xml.Serialization;

namespace MiniTwitter.Models.Net.Twitter
{
    [Serializable]
    [XmlRoot("statuses")]
    public class Statuses
    {
        [XmlElement("status")]
        public Status[] Status { get; set; }

        public static readonly Status[] Empty = new Status[0];
    }
}
