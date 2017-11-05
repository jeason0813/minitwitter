using System;
using System.ComponentModel;

namespace MiniTwitter.Models.Net.Twitter
{
    public interface ITwitterItem : INotifyPropertyChanged, IEquatable<ITwitterItem>
    {
        DateTime CreatedAt { get; set; }
        string RelativeTime { get; set; }
        ulong Id { get; set; }
        string Text { get; set; }
        bool IsAuthor { get; set; }
        bool IsNewest { get; set; }
        bool IsMessage { get; }
        bool IsReTweeted { get; }
        User Sender { get; set; }
        User Recipient { get; set; }
    }
}
