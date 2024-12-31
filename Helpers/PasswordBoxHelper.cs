using System.Windows.Controls;
using System.Windows;

namespace MES.Solution.Helpers
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty IsEmptyProperty =
            DependencyProperty.RegisterAttached("IsEmpty", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(true));

        public static bool GetIsEmpty(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEmptyProperty);
        }

        public static void SetIsEmpty(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEmptyProperty, value);
        }

        public static void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            if (passwordBox != null)
            {
                SetIsEmpty(passwordBox, string.IsNullOrEmpty(passwordBox.Password));
            }
        }
    }
}
