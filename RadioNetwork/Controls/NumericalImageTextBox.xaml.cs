using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RadioNetwork.Controls
{
    public partial class NumericalImageTextBox : TextBox
    {
        public NumericalImageTextBox()
        {
            InitializeComponent();

            // make default elements transparent
            this.Background = new SolidColorBrush();
            this.Foreground = new SolidColorBrush();
            this.CaretBrush = new SolidColorBrush();
            this.SelectionBrush = new SolidColorBrush();

            this.Cursor = Cursors.Hand;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            // Allow only backspace & digits
            if (!((e.Key == Key.Back)
                || (e.Key >= Key.D0 && e.Key <= Key.D9)
                || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                ))
            {
                e.Handled = true;
                return;
            }
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);

            // re-render digits
            this.Render();
        }

        /// <summary>
        /// Render control background based on Text value.
        /// </summary>
        private void Render()
        {
            string digit_uri_template = "pack://application:,,,/img/digits/{0}.jpg";

            int n = this.Text.Length;

            char[] chars = this.Text.ToCharArray();

            // load pictures for required digits
            List<BitmapImage> images = new List<BitmapImage>();
            for (int i = 0; i < n; ++i)
            {
                string uri = String.Format(digit_uri_template, chars[i]);
                images.Add(new BitmapImage(new Uri(uri)));
            }

            // fill free slots with "_" signs
            string empty_slot_uri = String.Format(digit_uri_template, "_");
            for (int i = 0; i < this.MaxLength - n; ++i)
            {
                images.Add(new BitmapImage(new Uri(empty_slot_uri)));
            }
            n = images.Count;

            // Find single sprite's parameters.
            // Assume that all sprites have the same format.
            BitmapImage im = images.FirstOrDefault();
            int width = im.PixelWidth;
            int height = im.PixelHeight;
            int stride = width * im.Format.BitsPerPixel / 8;

            // place all images on a single canvas side by side
            WriteableBitmap bitmap = new WriteableBitmap(im.PixelWidth * n, height, im.DpiX, im.DpiY, im.Format, im.Palette);
            for (int i = 0; i < n; ++i)
            {
                byte[] pixels = new byte[stride * im.PixelHeight * n];
                images[i].CopyPixels(pixels, stride, 0);
                bitmap.WritePixels(new Int32Rect(im.PixelWidth * i, 0, im.PixelWidth, im.PixelHeight), pixels, stride, 0);
            }

            this.Background = new ImageBrush(bitmap);
        }
    }
}