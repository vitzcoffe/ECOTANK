using ECOTANK.Data;

namespace ECOTANK
{
    public partial class MainPage : ContentPage
    {
        private readonly DatabaseService _database = new();

        public MainPage()
        {
            InitializeComponent();

            RolePicker.ItemsSource = new[]
            {
                "Turista",
                "Anfitrion",
                "Administrador",
                "Negocio Local"
            };
            RolePicker.SelectedIndex = 0;
        }

        private async void OnPrepareDatabaseClicked(object sender, EventArgs e)
        {
            await RunWithStatusAsync(async () =>
            {
                var connection = await _database.TestConnectionAsync();
                if (!connection.Ok)
                    return connection.Message;

                var schema = await _database.InitializeDatabaseAsync();
                if (!schema.Ok)
                    return schema.Message;

                var roles = await _database.EnsureDefaultRolesAsync();
                return roles.Ok
                    ? $"{connection.Message}\n{schema.Message}\n{roles.Message}"
                    : roles.Message;
            });
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            await RunWithStatusAsync(async () =>
            {
                var roleName = RolePicker.SelectedItem?.ToString() ?? "Turista";
                var result = await _database.RegisterUserAsync(
                    FullNameEntry.Text ?? "",
                    RegisterEmailEntry.Text ?? "",
                    RegisterPasswordEntry.Text ?? "",
                    roleName);

                if (!result.Ok)
                    return result.Message;

                LoginEmailEntry.Text = RegisterEmailEntry.Text;
                LoginPasswordEntry.Text = RegisterPasswordEntry.Text;
                return result.Message;
            });
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            await RunWithStatusAsync(async () =>
            {
                var result = await _database.ValidateLoginAsync(
                    LoginEmailEntry.Text ?? "",
                    LoginPasswordEntry.Text ?? "");

                return result.Message;
            });
        }

        private async Task RunWithStatusAsync(Func<Task<string>> operation)
        {
            try
            {
                SetFormEnabled(false);
                StatusLabel.Text = "Procesando...";

                var message = await operation();
                StatusLabel.Text = message;
                SemanticScreenReader.Announce(message);
            }
            finally
            {
                SetFormEnabled(true);
            }
        }

        private void SetFormEnabled(bool isEnabled)
        {
            foreach (var view in ((VerticalStackLayout)((ScrollView)Content).Content).Children)
            {
                if (view is VisualElement element)
                    element.IsEnabled = isEnabled;
            }
        }
    }
}
