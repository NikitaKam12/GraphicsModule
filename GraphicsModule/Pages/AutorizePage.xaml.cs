using Npgsql;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class AutorizePage : Page
    {
        public AutorizePage()
        {
            InitializeComponent();
        }

        private void Autorize_Click(object sender, RoutedEventArgs e)
        {
            var connection = new Connection();

            try
            {
                string login = Login_Box.Text;
                string password = Psswrd_box.Password;

                string query = $"SELECT id_user, id_role FROM Users WHERE login = @login AND password = @password";

                using (var conn = connection.GetConnection())
                {
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@login", login);
                        cmd.Parameters.AddWithValue("@password", password);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                var userId = reader.GetGuid(0);
                                var roleId = reader.GetGuid(1);

                                // Сохраняем id_user в сессии
                                Session.UserID = userId;

                                // Проверяем роль пользователя
                                if (roleId != Guid.Empty)
                                {
                                    // В зависимости от роли перенаправляем на нужную страницу
                                    if (IsRole(roleId, "Администратор"))
                                    {
                                        NavigationService.Navigate(new MainMenuAdminPage());
                                    }
                                    else if (IsRole(roleId, "Работник"))
                                    {
                                        NavigationService.Navigate(new MainPageUser());
                                    }
                                    else
                                    {
                                        MessageBox.Show("Роль не определена.");
                                    }
                                }
                                else
                                {
                                    MessageBox.Show("У данного пользователя нет роли.");
                                }
                            }
                            else
                            {
                                MessageBox.Show("Неверный логин или пароль.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Записываем ошибку в файл
                LogError(ex);
                MessageBox.Show($"Произошла ошибка: {ex.Message}");
            }
        }

        private bool IsRole(Guid roleId, string roleName)
        {
            var connection = new Connection();
            string query = $"SELECT COUNT(*) FROM Roles WHERE id_role = @roleId AND role_name = @roleName";

            using (var conn = connection.GetConnection())
            {
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@roleId", roleId);
                    cmd.Parameters.AddWithValue("@roleName", roleName);

                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        private void LogError(Exception ex)
        {
            try
            {
                // Определяем путь к папке Errors
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Errors");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Определяем путь к файлу логов
                string filePath = Path.Combine(directoryPath, $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                // Записываем информацию об ошибке в файл
                File.WriteAllText(filePath, $"Date: {DateTime.Now}\nException: {ex}\nStackTrace: {ex.StackTrace}");
            }
            catch (Exception logEx)
            {
                // Если запись в файл не удалась, можно добавить другую логику или просто игнорировать
                MessageBox.Show($"Не удалось записать информацию об ошибке в файл: {logEx.Message}");
            }
        }
    }
}
