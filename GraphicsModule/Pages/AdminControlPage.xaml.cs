using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class AdminControlPage : Page
    {
        private List<ProjectData> projects;
        private List<UserData> users;
        private Connection connection; // экземпляр класса для работы с подключением

        public AdminControlPage()
        {
            InitializeComponent();
            connection = new Connection();
            LoadData(); // Загрузка данных при инициализации страницы
        }

        // Метод для загрузки данных
        private void LoadData()
        {
            LoadProjects(); // Загрузка проектов
            LoadUsers();    // Загрузка пользователей
        }

        // Метод для загрузки всех проектов
        private void LoadProjects()
        {
            projects = new List<ProjectData>();

            try
            {
                string query = @"
        SELECT p.id_project, c.id_contract, cl.org_name
        FROM Project p
        INNER JOIN Contracts c ON p.id_contract = c.id_contract
        INNER JOIN Organizations o ON c.id_org = o.id_org
        INNER JOIN Clients cl ON o.id_client = cl.id_client;";

                using (var conn = connection.GetConnection())
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                projects.Add(new ProjectData
                                {
                                    ProjectId = reader.GetGuid(0),
                                    ContractId = reader.GetGuid(1),
                                    OrgName = reader.GetString(2)
                                });
                            }
                        }
                    }
                }

                ProjectsListView.ItemsSource = projects;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки проектов: {ex.Message}");
            }
        }

        // Метод для загрузки всех пользователей
        private void LoadUsers()
        {
            users = new List<UserData>();

            try
            {
                string query = @"
            SELECT u.id_user, u.name_user, 
                   CASE 
                       WHEN pu.id_user IS NOT NULL THEN 'Да' 
                       ELSE 'Нет' 
                   END as is_assigned,
                   STRING_AGG(cl.org_name, ', ') as org_names  -- Объединение названий компаний через запятую
            FROM Users u
            LEFT JOIN ProjectUsers pu ON u.id_user = pu.id_user
            LEFT JOIN Project p ON pu.id_project = p.id_project
            LEFT JOIN Contracts c ON p.id_contract = c.id_contract
            LEFT JOIN Organizations o ON c.id_org = o.id_org
            LEFT JOIN Clients cl ON o.id_client = cl.id_client
            LEFT JOIN Roles r ON u.id_role = r.id_role  -- Присоединение таблицы с ролями
            WHERE r.role_name != 'Администратор'  -- Исключаем администраторов
            GROUP BY u.id_user, u.name_user, pu.id_user";

                using (var conn = connection.GetConnection())
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                users.Add(new UserData
                                {
                                    UserId = reader.GetGuid(0),
                                    UserName = reader.GetString(1),
                                    IsAssigned = reader.GetString(2),
                                    OrgNames = reader.IsDBNull(3) ? "Нет компании" : reader.GetString(3)  // Если не назначен, выводим "Нет компании"
                                });
                            }
                        }
                    }
                }

                UsersListView.ItemsSource = users;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}");
            }
        }

        // Метод для назначения пользователя на проект
        private void AssignUserToProject_Click(object sender, RoutedEventArgs e)
        {
            var selectedProject = ProjectsListView.SelectedItem as ProjectData;
            var selectedUser = UsersListView.SelectedItem as UserData;

            if (selectedProject == null || selectedUser == null)
            {
                MessageBox.Show("Пожалуйста, выберите проект и пользователя.");
                return;
            }

            try
            {
                using (var conn = connection.GetConnection())
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    if (IsUserAlreadyAssigned(selectedUser.UserId, selectedProject.ProjectId, conn))
                    {
                        MessageBox.Show("Этот пользователь уже назначен на выбранный проект.");
                        return;
                    }

                    AssignUserToProject(selectedUser.UserId, selectedProject.ProjectId, conn);

                    MessageBox.Show("Пользователь успешно назначен на проект.");
                    LoadUsers(); // Обновляем список пользователей
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка назначения пользователя на проект: {ex.Message}");
            }
        }

        // Проверка, назначен ли пользователь на проект
        private bool IsUserAlreadyAssigned(Guid userId, Guid projectId, NpgsqlConnection conn)
        {
            string query = "SELECT COUNT(*) FROM ProjectUsers WHERE id_project = @projectId AND id_user = @userId";

            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@projectId", projectId);
                cmd.Parameters.AddWithValue("@userId", userId);

                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        // Назначение пользователя на проект
        private void AssignUserToProject(Guid userId, Guid projectId, NpgsqlConnection conn)
        {
            string query = "INSERT INTO ProjectUsers (id_project, id_user) VALUES (@projectId, @userId)";

            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@projectId", projectId);
                cmd.Parameters.AddWithValue("@userId", userId);

                cmd.ExecuteNonQuery();
            }
        }

        // Класс для хранения данных проекта
        public class ProjectData
        {
            public Guid ProjectId { get; set; }
            public Guid ContractId { get; set; }
            public string OrgName { get; set; }
        }

        // Класс для хранения данных пользователя
        public class UserData
        {
            public Guid UserId { get; set; }
            public string UserName { get; set; }
            public string IsAssigned { get; set; } // Статус назначения пользователя на проект
            public string OrgNames { get; set; } // Названия компаний через запятую
        }

        private void GoBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
