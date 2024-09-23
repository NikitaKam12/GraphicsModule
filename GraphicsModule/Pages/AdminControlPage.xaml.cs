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
    SELECT DISTINCT p.id_project, c.id_contract, cl.org_name, 
           c.date_start, c.date_end, c.date1_start, c.date2_end
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
                            var uniqueProjects = new HashSet<Guid>();  // Используем для устранения дублирования

                            while (reader.Read())
                            {
                                var projectId = reader.GetGuid(0);

                                if (!uniqueProjects.Contains(projectId))
                                {
                                    uniqueProjects.Add(projectId);  // Добавляем уникальные проекты

                                    projects.Add(new ProjectData
                                    {
                                        ProjectId = projectId,
                                        ContractId = reader.GetGuid(1),
                                        OrgName = reader.GetString(2),
                                        Phase1Start = reader.GetDateTime(3), // Дата начала первого этапа
                                        Phase1End = reader.GetDateTime(4),   // Дата конца первого этапа
                                        Phase2Start = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),  // Дата начала второго этапа
                                        Phase2End = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)     // Дата конца второго этапа
                                    });
                                }
                            }
                        }
                    }
                }

                ProjectsListView.ItemsSource = projects;  // Обновляем список проектов
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
            var selectedPhase = PhaseComboBox.SelectedItem as ComboBoxItem;

            if (selectedProject == null || selectedUser == null || selectedPhase == null)
            {
                MessageBox.Show("Пожалуйста, выберите проект, пользователя и этап.");
                return;
            }

            string phaseText = selectedPhase.Content.ToString();
            bool assignPhase1 = phaseText == "Этап 1" || phaseText == "Оба этапа";
            bool assignPhase2 = phaseText == "Этап 2" || phaseText == "Оба этапа";

            try
            {
                using (var conn = connection.GetConnection())
                {
                    if (conn.State != System.Data.ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    // Проверяем, назначен ли пользователь на этап 1 или этап 2
                    if (IsUserAssignedToPhase(selectedUser.UserId, selectedProject.ProjectId, assignPhase1, assignPhase2, conn))
                    {
                        MessageBox.Show("Пользователь уже назначен на выбранный этап проекта.");
                        return;
                    }

                    // Получаем выбранные даты из DatePicker'ов для каждого этапа
                    DateTime? phase1Start = Phase1StartDatePicker.SelectedDate;
                    DateTime? phase1End = Phase1EndDatePicker.SelectedDate;
                    DateTime? phase2Start = Phase2StartDatePicker.SelectedDate;
                    DateTime? phase2End = Phase2EndDatePicker.SelectedDate;

                    // Логика для этапа 1
                    if (assignPhase1)
                    {
                        if (phase1Start == null) phase1Start = selectedProject.Phase1Start;
                        if (phase1End == null) phase1End = selectedProject.Phase1End;

                        if (phase1Start < selectedProject.Phase1Start || phase1End > selectedProject.Phase1End)
                        {
                            MessageBox.Show("Даты этапа 1 выходят за рамки проекта.");
                            return;
                        }
                    }
                    else
                    {
                        phase1Start = null;
                        phase1End = null;
                    }

                    // Логика для этапа 2
                    if (assignPhase2)
                    {
                        if (phase2Start == null) phase2Start = selectedProject.Phase2Start;
                        if (phase2End == null) phase2End = selectedProject.Phase2End;

                        if (phase2Start < selectedProject.Phase2Start || phase2End > selectedProject.Phase2End)
                        {
                            MessageBox.Show("Даты этапа 2 выходят за рамки проекта.");
                            return;
                        }
                    }
                    else
                    {
                        phase2Start = null;
                        phase2End = null;
                    }

                    // Добавляем пользователя с датами или обновляем
                    AssignOrUpdateUserInProject(selectedUser.UserId, selectedProject.ProjectId, assignPhase1, assignPhase2, phase1Start, phase1End, phase2Start, phase2End, conn);

                    MessageBox.Show("Пользователь успешно назначен на проект.");
                    LoadUsers(); // Обновляем список пользователей
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка назначения пользователя на проект: {ex.Message}");
            }
        }

        // Проверка, назначен ли пользователь на этап
        private bool IsUserAssignedToPhase(Guid userId, Guid projectId, bool assignPhase1, bool assignPhase2, NpgsqlConnection conn)
        {
            string query = @"
    SELECT phase1, phase2 
    FROM ProjectUsers 
    WHERE id_project = @projectId AND id_user = @userId";

            using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@projectId", projectId);
                cmd.Parameters.AddWithValue("@userId", userId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        bool isAssignedToPhase1 = reader.GetBoolean(0);
                        bool isAssignedToPhase2 = reader.GetBoolean(1);

                        // Проверяем, если пользователь уже назначен на этап 1 или этап 2
                        if ((assignPhase1 && isAssignedToPhase1) || (assignPhase2 && isAssignedToPhase2))
                        {
                            return true; // Пользователь уже назначен на указанный этап
                        }
                    }
                }
            }

            return false; // Пользователь не назначен на указанный этап
        }

        // Назначение или обновление пользователя на проект
        private void AssignOrUpdateUserInProject(Guid userId, Guid projectId, bool assignPhase1, bool assignPhase2, DateTime? phase1Start, DateTime? phase1End, DateTime? phase2Start, DateTime? phase2End, NpgsqlConnection conn)
        {
            // Проверяем, существует ли уже запись для данного пользователя и проекта
            string checkQuery = @"
        SELECT id_project, id_user, phase1, phase2 
        FROM ProjectUsers 
        WHERE id_project = @projectId AND id_user = @userId";

            using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
            {
                checkCmd.Parameters.AddWithValue("@projectId", projectId);
                checkCmd.Parameters.AddWithValue("@userId", userId);

                using (var reader = checkCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        // Запись существует - выполняем обновление
                        bool isAssignedToPhase1 = reader.GetBoolean(2);
                        bool isAssignedToPhase2 = reader.GetBoolean(3);

                        reader.Close(); // Закрываем reader перед выполнением команды обновления

                        // Обновляем этапы для существующего пользователя
                        string updateQuery = @"
                            UPDATE ProjectUsers 
                            SET phase1 = @phase1, phase1_start = @phase1Start, phase1_end = @phase1End,
                                phase2 = @phase2, phase2_start = @phase2Start, phase2_end = @phase2End
                            WHERE id_project = @projectId AND id_user = @userId";

                        using (var updateCmd = new NpgsqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@projectId", projectId);
                            updateCmd.Parameters.AddWithValue("@userId", userId);

                            // Если уже назначен на фазу 1, не обновляем её заново, если только assignPhase1 не true
                            updateCmd.Parameters.AddWithValue("@phase1", assignPhase1 || isAssignedToPhase1);
                            updateCmd.Parameters.AddWithValue("@phase1Start", assignPhase1 ? (object)phase1Start : DBNull.Value);
                            updateCmd.Parameters.AddWithValue("@phase1End", assignPhase1 ? (object)phase1End : DBNull.Value);

                            // Если уже назначен на фазу 2, не обновляем её заново, если только assignPhase2 не true
                            updateCmd.Parameters.AddWithValue("@phase2", assignPhase2 || isAssignedToPhase2);
                            updateCmd.Parameters.AddWithValue("@phase2Start", assignPhase2 ? (object)phase2Start : DBNull.Value);
                            updateCmd.Parameters.AddWithValue("@phase2End", assignPhase2 ? (object)phase2End : DBNull.Value);

                            updateCmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        reader.Close(); // Закрываем reader перед выполнением команды вставки

                        // Запись не существует - выполняем вставку новой записи
                        string insertQuery = @"
                            INSERT INTO ProjectUsers (id_project, id_user, phase1, phase1_start, phase1_end, 
                                                      phase2, phase2_start, phase2_end) 
                            VALUES (@projectId, @userId, @phase1, @phase1Start, @phase1End, @phase2, @phase2Start, @phase2End)";

                        using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@projectId", projectId);
                            insertCmd.Parameters.AddWithValue("@userId", userId);
                            insertCmd.Parameters.AddWithValue("@phase1", assignPhase1);
                            insertCmd.Parameters.AddWithValue("@phase1Start", assignPhase1 ? (object)phase1Start : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@phase1End", assignPhase1 ? (object)phase1End : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@phase2", assignPhase2);
                            insertCmd.Parameters.AddWithValue("@phase2Start", assignPhase2 ? (object)phase2Start : DBNull.Value);
                            insertCmd.Parameters.AddWithValue("@phase2End", assignPhase2 ? (object)phase2End : DBNull.Value);

                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        // Класс для хранения данных проекта
        public class ProjectData
        {
            public Guid ProjectId { get; set; }
            public Guid ContractId { get; set; }
            public string OrgName { get; set; }
            public DateTime StartDate { get; set; }  // Дата начала проекта
            public DateTime EndDate { get; set; }    // Дата окончания проекта
            public DateTime? Phase1Start { get; set; }  // Дата начала этапа 1
            public DateTime? Phase1End { get; set; }    // Дата конца этапа 1
            public DateTime? Phase2Start { get; set; }  // Дата начала этапа 2
            public DateTime? Phase2End { get; set; }    // Дата конца этапа 2
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
