using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinTweaker.Components
{
    public partial class UnlockerCard : UserControl
    {
        public UnlockerCard()
        {
            InitializeComponent();
        }

        public string Title
        {
            get => TitleText.Text;
            set => TitleText.Text = value;
        }

        public string Description
        {
            get => DescriptionText.Text;
            set => DescriptionText.Text = value;
        }

        public string Status
        {
            get => StatusText.Text;
            set => StatusText.Text = value;
        }

        public Brush StatusColor
        {
            get => StatusText.Foreground;
            set => StatusText.Foreground = value;
        }

        public string ButtonText
        {
            get => ActionButton.Content?.ToString() ?? "";
            set => ActionButton.Content = value;
        }

        public event RoutedEventHandler? ActionClicked;

        private void ActionButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ActionClicked?.Invoke(this, e);
        }
    }
}