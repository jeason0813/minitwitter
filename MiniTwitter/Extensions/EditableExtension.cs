using System.Collections.Generic;
using System.ComponentModel;

namespace MiniTwitter.Extensions
{
    static class EditableExtension
    {
        public static void BeginEdit<TSource>(this IEnumerable<TSource> source) where TSource : IEditableObject
        {
            foreach (var item in source)
            {
                item.BeginEdit();
            }
        }

        public static void EndEdit<TSource>(this IEnumerable<TSource> source) where TSource : IEditableObject
        {
            foreach (var item in source)
            {
                item.EndEdit();
            }
        }

        public static void CancelEdit<TSource>(this IEnumerable<TSource> source) where TSource : IEditableObject
        {
            foreach (var item in source)
            {
                item.CancelEdit();
            }
        }
    }
}
