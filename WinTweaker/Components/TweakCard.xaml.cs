using System.Windows;
using System.Windows.Controls;

namespace WinTweaker.Components
{
    public partial class TweakCard : UserControl
    {
        public TweakCard()
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

        public string ButtonText
        {
            get => ActionButton.Content?.ToString() ?? "";
            set => ActionButton.Content = value;
        }

        public string Status
        {
            get => StatusText.Text;
            set => StatusText.Text = value;
        }

        public event RoutedEventHandler? ActionClicked;

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            ActionClicked?.Invoke(this, e);
        }
    }
}