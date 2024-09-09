using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class UserWorkPage : Page
    {
        private readonly Connection _connection;

        public UserWorkPage()
        {
            InitializeComponent();
            _connection = new Connection();
            LoadUserData();
        }

        private void LoadUserData()
        {
            try
            {
                string query = @"
                    WITH UserProjects AS (
                        SELECT u.id_user, u.name_user, r.role_name,
                               p.id_project, c.date_start, c.date_end, cl.org_name
                        FROM users u
                        JOIN roles r ON u.id_role = r.id_role
                        JOIN projectusers pu ON u.id_user = pu.id_user
                        JOIN project p ON pu.id_project = p.id_project
                        JOIN contracts c ON p.id_contract = c.id_contract
                        JOIN organizations o ON c.id_org = o.id_org
                        JOIN clients cl ON o.id_client = cl.id_client
                        WHERE r.role_name != 'Администратор'
                    ),
                    UserProjectOverlap AS (
                        SELECT up1.id_user, COUNT(*) AS overlapping_projects
                        FROM UserProjects up1
                        JOIN UserProjects up2 ON up1.id_user = up2.id_user
                        WHERE up1.id_project <> up2.id_project
                          AND up1.date_start < up2.date_end
                          AND up2.date_start < up1.date_end
                        GROUP BY up1.id_user
                    )
                    SELECT u.name_user, r.role_name, 
                           COUNT(pu.id_project) AS project_count,
                           COALESCE(STRING_AGG(cl.org_name, ', '), 'Нет проектов') AS project_list,
                           COALESCE(upo.overlapping_projects, 0) AS overlapping_projects
                    FROM users u
                    JOIN roles r ON u.id_role = r.id_role
                    LEFT JOIN projectusers pu ON u.id_user = pu.id_user
                    LEFT JOIN project p ON pu.id_project = p.id_project
                    LEFT JOIN contracts c ON p.id_contract = c.id_contract
                    LEFT JOIN organizations o ON c.id_org = o.id_org
                    LEFT JOIN clients cl ON o.id_client = cl.id_client
                    LEFT JOIN UserProjectOverlap upo ON u.id_user = upo.id_user
                    WHERE r.role_name != 'Администратор'
                    GROUP BY u.name_user, r.role_name, upo.overlapping_projects
                    ORDER BY u.name_user;
                ";

                using (var conn = _connection.GetConnection())
                {
                    if (conn.State != ConnectionState.Open)
                    {
                        conn.Open();
                    }

                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            var usersList = new List<UserData>();

                            while (reader.Read())
                            {
                                var userData = new UserData
                                {
                                    Name_User = reader["name_user"].ToString(),
                                    Role_Name = reader["role_name"].ToString(),
                                    ProjectCount = Convert.ToInt32(reader["project_count"]),
                                    ProjectList = reader["project_list"].ToString(),
                                    OverlappingProjects = Convert.ToInt32(reader["overlapping_projects"])
                                };

                                usersList.Add(userData);
                            }

                            UsersDataGrid.ItemsSource = usersList;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void Go_Back(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }

    public class UserData
    {
        public string Name_User { get; set; }
        public string Role_Name { get; set; }
        public int ProjectCount { get; set; }
        public string ProjectList { get; set; }
        public int OverlappingProjects { get; set; }
    }
}
