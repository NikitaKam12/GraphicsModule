using System;
using System.Collections.Generic;
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
using GraphicsModule.Pages;

namespace GraphicsModule.Pages
{
    /// <summary>
    /// Логика взаимодействия для MainPageUser.xaml
    /// </summary>
    public partial class MainPageUser : Page
    {
        public MainPageUser()
        {
            InitializeComponent();
        }

        private void Go_Check_Mates_And_Plan(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new MatesAndPlanUserPage());
        }

        private void Go_Check_Plan(object sender, RoutedEventArgs e)
        {

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
    }
}
