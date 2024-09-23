using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace GraphicsModule.Pages
{
    /// <summary>
    /// Логика взаимодействия для MainMenuAdminPage.xaml
    /// </summary>
    public partial class MainMenuAdminPage : Page
    {
        public MainMenuAdminPage()
        {
            InitializeComponent();
        }

        private void Create_Plan_Butn(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new CreateAdminProjPage());
        }

        private void Set_peoples_Butn(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AdminControlPage());
        }

        private void Go_Back(object sender, RoutedEventArgs e)
        {
            // Спрашиваем пользователя перед возвратом
            MessageBoxResult result = MessageBox.Show(
                "Вы действительно хотите перейти на страницу авторизации?",
                "Остаться в панели",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            // Если пользователь нажал "Да", выполняем возврат
            if (result == MessageBoxResult.Yes)
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
        }

        private void Show_User_Load(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new UserWorkPage());
        }

        private void Show_User_Project(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new GeneralAdmCheckPeoplesProjsPage());
        }
    }
}
