namespace WinTweaker.Models
{
    public class StartupItem
    {
        public string Name { get; set; } = "";

        public string Command { get; set; } = "";

        public string Source { get; set; } = "";

        public string Location { get; set; } = "";

        public bool IsEnabled { get; set; }

        public string ItemType { get; set; } = "";

        public string RegistryRoot { get; set; } = "";

        public string RegistrySubKey { get; set; } = "";

        public string RegistryValueName { get; set; } = "";

        // ВАЖНО:
        // StartupService хранит здесь числовое значение
        // типа реестра.
        public int RegistryValueKind { get; set; }

        public string StartupApprovedSubKey { get; set; } = "";

        public string OriginalPath { get; set; } = "";

        public bool CanToggle { get; set; }

        // Для системных записей:
        // "🔒 Системный"
        // "🟣 При входе"
        // "🟠 Отключено"
        public string CustomStatusText { get; set; } = "";

        public string StatusText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(
                        CustomStatusText))
                {
                    return CustomStatusText;
                }

                return IsEnabled
                    ? "🟢 Включено"
                    : "🟠 Отключено";
            }
        }
    }
}