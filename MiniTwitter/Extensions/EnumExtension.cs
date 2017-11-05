﻿using System.Windows.Input;

using MiniTwitter.Models;
using MiniTwitter.Models.Input;

namespace MiniTwitter.Extensions
{
    static class EnumExtension
    {
        public static string ToPropertyPath(this ListSortCategory category)
        {
            switch (category)
            {
                case ListSortCategory.Id:
                    return "ID";
                case ListSortCategory.CreatedAt:
                    return "CreatedAt";
                case ListSortCategory.ScreenName:
                    return "Sender.ScreenName";
                default:
                    return string.Empty;
            }
        }

        public static ICommand ToCommand(this KeyAction action)
        {
            switch (action)
            {
                case KeyAction.Update:
                    return Commands.Update;
                case KeyAction.Refresh:
                    return Commands.Refresh;
                case KeyAction.Search:
                    return Commands.Search;
                case KeyAction.Reply:
                    return Commands.Reply;
                case KeyAction.ReplyMessage:
                    return Commands.ReplyMessage;
                case KeyAction.Delete:
                    return Commands.Delete;
                case KeyAction.Favorite:
                    return Commands.Favorite;
                case KeyAction.ReadAll:
                    return Commands.ReadAll;
                case KeyAction.InReplyTo:
                    return Commands.InReplyTo;
                //case KeyAction.ScrollUp:
                //    return Commands.ScrollUp;
                //case KeyAction.ScrollDown:
                //    return Commands.ScrollDown;
                default:
                    return null;
            }
        }
    }
}
