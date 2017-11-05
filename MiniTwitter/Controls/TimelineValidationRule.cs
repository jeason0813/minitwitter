using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

using MiniTwitter.Extensions;
using MiniTwitter.Resources.languages.jp;

namespace MiniTwitter.Controls
{
    public class TimelineValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var bindingGroup = (BindingGroup)value;
            var timeline = (Timeline)bindingGroup.Items[0];
            var name = (string)bindingGroup.GetValue(timeline, "Name");
            if (name.IsNullOrEmpty())
            {
                return new ValidationResult(false, Resource_jp.please_enter_name);
            }
            return ValidationResult.ValidResult;
        }
    }
}
