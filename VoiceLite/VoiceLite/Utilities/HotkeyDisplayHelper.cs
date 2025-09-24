using System.Text;
using System.Windows.Input;

namespace VoiceLite.Utilities
{
    internal static class HotkeyDisplayHelper
    {
        public static string Format(Key key, ModifierKeys modifiers)
        {
            var builder = new StringBuilder();

            if (modifiers != ModifierKeys.None)
            {
                AppendModifier(builder, modifiers, ModifierKeys.Control, "Ctrl");
                AppendModifier(builder, modifiers, ModifierKeys.Alt, "Alt");
                AppendModifier(builder, modifiers, ModifierKeys.Shift, "Shift");
                AppendModifier(builder, modifiers, ModifierKeys.Windows, "Win");
            }

            builder.Append(key);
            return builder.ToString();
        }

        private static void AppendModifier(StringBuilder builder, ModifierKeys modifiers, ModifierKeys flag, string label)
        {
            if ((modifiers & flag) == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append(" + ");
            }

            builder.Append(label);
        }
    }
}
