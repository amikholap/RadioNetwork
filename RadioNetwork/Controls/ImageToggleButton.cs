using System;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace RadioNetwork.Controls
{
    public class ImageToggleButton : ToggleButton
    {
        public static DependencyProperty CheckedBackgroundImageSourceProperty = DependencyProperty.Register("CheckedBackgroundImageSource", typeof(string), typeof(ImageToggleButton));
        public static DependencyProperty UncheckedBackgroundImageSourceProperty = DependencyProperty.Register("UncheckedBackgroundImageSource", typeof(string), typeof(ImageToggleButton));

        public string CheckedBackgroundImageSource
        {
            get
            {
                return (string)this.GetValue(CheckedBackgroundImageSourceProperty);
            }
            set
            {
                this.SetValue(CheckedBackgroundImageSourceProperty, value);
            }
        }

        public string UncheckedBackgroundImageSource
        {
            get
            {
                return (string)base.GetValue(UncheckedBackgroundImageSourceProperty);
            }
            set
            {
                base.SetValue(UncheckedBackgroundImageSourceProperty, value);
            }
        }

        static ImageToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageToggleButton), new FrameworkPropertyMetadata(typeof(ImageToggleButton)));
        }
    }
}
