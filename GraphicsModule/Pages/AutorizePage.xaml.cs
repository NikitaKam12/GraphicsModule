using Npgsql;
using System;
using System.Collections.Generic;
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
                                    if (IsRole(roleId, "Администратор"))
                                    {
                                        NavigationService.Navigate(new MainMenuAdminPage());

                                        // Закрываем текущее соединение перед добавлением записей в workschedule
                                        reader.Close();
                                        conn.Close();

                                        // Открываем новое соединение для выполнения операций с workschedule
                                        AddWorkScheduleForAllUsers();
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

        private void AddWorkScheduleForAllUsers()
        {
            try
            {
                var connection = new Connection();

                using (var conn = connection.GetConnection())
                {
                    DateTime currentDate = DateTime.Now.Date;

                    // Получаем всех пользователей и их проекты из таблицы projectusers
                    string query = @"
                SELECT pu.id_user, pu.id_project
                FROM projectusers pu
                JOIN project p ON pu.id_project = p.id_project
                JOIN status s ON p.id_status = s.id_status
                WHERE s.status_name != 'Архивирован'";

                    List<(Guid userId, Guid projectId)> usersProjects = new List<(Guid, Guid)>();

                    // Читаем данные и сохраняем их в список
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Guid userId = reader.GetGuid(0);
                                Guid projectId = reader.GetGuid(1);
                                usersProjects.Add((userId, projectId));
                            }
                        }
                    }

                    // Теперь, когда reader закрыт, обрабатываем данные
                    foreach (var (userId, projectId) in usersProjects)
                    {
                        // Проверяем, есть ли уже запись в workschedule на текущую дату
                        string checkQuery = @"
                    SELECT COUNT(*)
                    FROM workschedule
                    WHERE id_user = @userId
                    AND work_date = @currentDate";

                        using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@userId", userId);
                            checkCmd.Parameters.AddWithValue("@currentDate", currentDate); // Передаем как DateTime

                            int count = Convert.ToInt32(checkCmd.ExecuteScalar());
                            if (count == 0)
                            {
                                // Если записи нет, добавляем запись в workschedule
                                string insertQuery = @"
                            INSERT INTO workschedule (id_user, id_project, work_date, start_time, end_time)
                            VALUES (@userId, @projectId, @currentDate, '09:00', '17:00')";

                                using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
                                {
                                    insertCmd.Parameters.AddWithValue("@userId", userId);
                                    insertCmd.Parameters.AddWithValue("@projectId", projectId);
                                    insertCmd.Parameters.AddWithValue("@currentDate", currentDate); // Передаем как DateTime

                                    insertCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                MessageBox.Show($"Ошибка при добавлении в workschedule: {ex.Message}");
            }
        }
        private void LogError(Exception ex)
        {
            try
            {
                string directoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Errors");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string filePath = Path.Combine(directoryPath, $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(filePath, $"Date: {DateTime.Now}\nException: {ex}\nStackTrace: {ex.StackTrace}");
            }
            catch (Exception logEx)
            {
                MessageBox.Show($"Не удалось записать информацию об ошибке в файл: {logEx.Message}");
            }
        }
    }
}
