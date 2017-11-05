using System;

namespace MiniTwitter.Models.Net.Twitter
{
    class UpdateEventArgs : EventArgs
    {
        public UpdateEventArgs(ITwitterItem item)
        {
            Item = item;
        }

        public ITwitterItem Item { get; private set; }
    }
}
