using System.Globalization;
using System.Windows.Controls;
using MiniTwitter.Resources.languages.jp;

namespace MiniTwitter.Controls
{
    public class TweetValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = (string)value;

            if (text.Length > 140)
            {
                return new ValidationResult(false, Resource_jp.exceeds_140_characters);
            }

            return ValidationResult.ValidResult;
        }
    }
}
