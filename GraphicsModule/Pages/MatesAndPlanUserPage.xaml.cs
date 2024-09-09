using Npgsql;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class MatesAndPlanUserPage : Page
    {
        public PlotModel PlotModel { get; set; }

        private List<ProjectData> projects;

        public MatesAndPlanUserPage()
        {
            InitializeComponent();
            LoadProjectsData();
            InitializePlotModel();
        }

        private void LoadProjectsData()
        {
            var connection = new Connection();
            var userId = Session.UserID;

            string query = @"
        SELECT 
            p.id_project,
            c.date_start,
            c.date_end,
            s.status_name,
            cl.org_name,
            u.id_user,
            u.name_user
        FROM 
            ProjectUsers pu
        JOIN 
            Project p ON pu.id_project = p.id_project
        JOIN 
            Contracts c ON p.id_contract = c.id_contract
        JOIN 
            Status s ON p.id_status = s.id_status
        JOIN 
            Organizations o ON c.id_org = o.id_org
        JOIN 
            Clients cl ON o.id_client = cl.id_client
        JOIN 
            ProjectUsers pu_all ON p.id_project = pu_all.id_project
        JOIN 
            Users u ON pu_all.id_user = u.id_user
        WHERE 
            pu.id_user = @userId";

            using (var conn = connection.GetConnection())
            {
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@userId", userId);

                    using (var reader = cmd.ExecuteReader())
                    {
                        projects = new List<ProjectData>();
                        while (reader.Read())
                        {
                            var projectId = reader.GetGuid(0);
                            var projectData = projects.FirstOrDefault(p => p.ProjectId == projectId);

                            // Если проект еще не добавлен в список
                            if (projectData == null)
                            {
                                projectData = new ProjectData
                                {
                                    ProjectId = projectId,
                                    StartDate = reader.GetDateTime(1),
                                    EndDate = reader.GetDateTime(2),
                                    Status = reader.GetString(3),
                                    OrganizationName = reader.GetString(4),
                                    TeamMembers = new List<UserData>()
                                };
                                projects.Add(projectData);
                            }

                            // Добавление членов команды проекта
                            var teamMember = new UserData
                            {
                                UserId = reader.GetGuid(5),
                                UserName = reader.GetString(6)
                            };

                            if (!projectData.TeamMembers.Any(u => u.UserId == teamMember.UserId))
                            {
                                projectData.TeamMembers.Add(teamMember);
                            }
                        }

                        // После загрузки данных из базы вызов метода для обновления графика
                        InitializePlotModel();
                    }
                }
            }
        }


        private void InitializePlotModel()
        {
            if (projects == null || !projects.Any())
            {
                throw new Exception("Список проектов пуст или не инициализирован.");
            }

            PlotModel = new PlotModel { Title = "Диаграмма Ганта" };

            // Ось X - время (месяцы)
            var startDate = projects.Min(p => p.StartDate);
            var endDate = projects.Max(p => p.EndDate);

            var timeAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = DateTimeAxis.ToDouble(startDate),
                Maximum = DateTimeAxis.ToDouble(endDate),
                Title = "Время",
                StringFormat = "MMM yyyy",
                IntervalType = DateTimeIntervalType.Months,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            };
            PlotModel.Axes.Add(timeAxis);

            // Ось Y - Проекты (категории) с возможностью прокрутки
            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Проекты",
                GapWidth = 0.5,  // Делает график более читаемым
                IsPanEnabled = true,  // Включение прокрутки
                IsZoomEnabled = true  // Включение увеличения
            };

            foreach (var project in projects)
            {
                categoryAxis.Labels.Add(project.OrganizationName);
            }
            PlotModel.Axes.Add(categoryAxis);

            // Добавление каждого проекта как прямоугольника
            foreach (var project in projects)
            {
                var barSeries = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName
                };

                var startDateDouble = DateTimeAxis.ToDouble(project.StartDate);
                var endDateDouble = DateTimeAxis.ToDouble(project.EndDate);
                int projectIndex = projects.IndexOf(project);

                barSeries.Items.Add(new RectangleBarItem(startDateDouble, projectIndex - 0.4, endDateDouble, projectIndex + 0.4));

                // Подключение события для обновления команды и компании
                barSeries.MouseDown += (s, e) =>
                {
                    UpdateCompanyName(project.OrganizationName);
                    UpdateTeamList(project.TeamMembers);
                };

                PlotModel.Series.Add(barSeries);
            }

            ProjectTimeline.Model = PlotModel;
        }


        // Метод для обновления списка команды проекта
        private void UpdateTeamList(List<UserData> teamMembers)
        {
            TeamList.ItemsSource = teamMembers;
        }

        // Метод для обновления названия компании
        private void UpdateCompanyName(string companyName)
        {
            CompanyName.Text = companyName;
        }

        public class ProjectData
        {
            public Guid ProjectId { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string OrganizationName { get; set; }
            public string Status { get; set; }
            public List<UserData> TeamMembers { get; set; } = new List<UserData>();
        }

        public class UserData
        {
            public Guid UserId { get; set; }
            public string UserName { get; set; }
        }

        private void Go_Back(object sender, System.Windows.RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
