using System;
using MiniTwitter.Models.Net.Twitter;

namespace MiniTwitter.Models.Net
{
    public class StatusEventArgs : EventArgs
    {
        public StatusEventArgs(Status status)
        {
            Status = status;
        }

        public Status Status { get; private set; }

        public StatusAction Action { get; set; }
    }

    public enum StatusAction
    {
        Update,
        Deleted,
        Favorited,
        Unfavorited,
    }
}
