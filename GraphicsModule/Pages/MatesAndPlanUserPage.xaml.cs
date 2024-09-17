using Npgsql;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class MatesAndPlanUserPage : Page
    {
        public PlotModel PlotModel { get; set; }

        private List<ProjectData> projects;
        public List<string> Companies { get; set; }
        public List<string> Months { get; set; }
        public List<int> Years { get; set; }

        public MatesAndPlanUserPage()
        {
            InitializeComponent();
            LoadProjectsData();
            LoadComboBoxData();
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
                    c.date1_start, 
                    c.date2_end,
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
                    pu.id_user = @userId
                    AND s.status_name != 'Архивирован'";

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

                            if (projectData == null)
                            {
                                projectData = new ProjectData
                                {
                                    ProjectId = projectId,
                                    StartDate = reader.GetDateTime(1),
                                    EndDate = reader.GetDateTime(2),
                                    ResumeDate = reader.GetDateTime(3), // Дата возобновления
                                    ResumeEndDate = reader.GetDateTime(4), // Дата окончания после возобновления
                                    Status = reader.GetString(5),
                                    OrganizationName = reader.GetString(6),
                                    TeamMembers = new List<TeamMemberData>()
                                };
                                projects.Add(projectData);
                            }

                            var teamMember = new TeamMemberData
                            {
                                UserId = reader.GetGuid(7),
                                UserName = reader.GetString(8)
                            };

                            if (!projectData.TeamMembers.Any(u => u.UserId == teamMember.UserId))
                            {
                                projectData.TeamMembers.Add(teamMember);
                            }
                        }

                        InitializePlotModel();
                    }
                }
            }
        }

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = "Диаграмма Ганта" };

            var startDate = projects.Min(p => p.StartDate);
            var endDate = projects.Max(p => p.ResumeEndDate);

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

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                Title = "Проекты",
                GapWidth = 0.5
            };

            foreach (var project in projects)
            {
                categoryAxis.Labels.Add(project.OrganizationName);
            }
            PlotModel.Axes.Add(categoryAxis);

            foreach (var project in projects)
            {
                var barSeries = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName
                };

                // Первая часть (до паузы)
                var startDateDouble = DateTimeAxis.ToDouble(project.StartDate);
                var endDateDouble = DateTimeAxis.ToDouble(project.EndDate);
                int projectIndex = projects.IndexOf(project);
                barSeries.Items.Add(new RectangleBarItem(startDateDouble, projectIndex - 0.2, endDateDouble, projectIndex + 0.2));

                // Вторая часть (после возобновления)
                var resumeDateDouble = DateTimeAxis.ToDouble(project.ResumeDate);
                var resumeEndDateDouble = DateTimeAxis.ToDouble(project.ResumeEndDate);
                barSeries.Items.Add(new RectangleBarItem(resumeDateDouble, projectIndex - 0.2, resumeEndDateDouble, projectIndex + 0.2));

                barSeries.MouseDown += (s, e) =>
                {
                    UpdateCompanyName(project.OrganizationName);
                    UpdateTeamList(project.TeamMembers);
                };

                PlotModel.Series.Add(barSeries);
            }

            ProjectTimeline.Model = PlotModel;
        }

        private void UpdateTeamList(List<TeamMemberData> teamMembers)
        {
            TeamList.ItemsSource = teamMembers;
        }

        private void UpdateCompanyName(string companyName)
        {
            CompanyName.Text = companyName;
        }

        private void LoadComboBoxData()
        {
            // Заполнение ComboBox списками компаний, месяцев и лет
            Companies = projects.Select(p => p.OrganizationName).Distinct().ToList();
            Months = projects.Select(p => p.StartDate.ToString("MMMM")).Distinct().ToList();
            Years = projects.Select(p => p.StartDate.Year).Distinct().ToList();

            SortByCompany.ItemsSource = Companies;
            SortByMonth.ItemsSource = Months;
            SortByYear.ItemsSource = Years;
        }

        private void SortByCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработка сортировки по компании
            var selectedCompany = SortByCompany.SelectedItem as string;
            var filteredProjects = projects.Where(p => p.OrganizationName == selectedCompany).ToList();
            UpdatePlot(filteredProjects);
        }

        private void SortByMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработка сортировки по месяцу
            var selectedMonth = SortByMonth.SelectedItem as string;
            var filteredProjects = projects.Where(p => p.StartDate.ToString("MMMM") == selectedMonth).ToList();
            UpdatePlot(filteredProjects);
        }

        private void SortByYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Обработка сортировки по году
            var selectedYear = SortByYear.SelectedItem as int?;
            var filteredProjects = projects.Where(p => p.StartDate.Year == selectedYear).ToList();
            UpdatePlot(filteredProjects);
        }
        private void AddMouseDownHandlersToSeries()
        {
            foreach (var series in PlotModel.Series.OfType<RectangleBarSeries>())
            {
                var project = projects.FirstOrDefault(p => p.OrganizationName == series.Title);
                if (project != null)
                {
                    series.MouseDown += (s, e) =>
                    {
                        UpdateCompanyName(project.OrganizationName);
                        UpdateTeamList(project.TeamMembers);
                    };
                }
            }
        }

        private void UpdatePlot(List<ProjectData> filteredProjects)
        {
            PlotModel.Series.Clear();

            foreach (var project in filteredProjects)
            {
                var barSeries = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName
                };

                // Первая часть (до паузы)
                var startDateDouble = DateTimeAxis.ToDouble(project.StartDate);
                var endDateDouble = DateTimeAxis.ToDouble(project.EndDate);
                int projectIndex = filteredProjects.IndexOf(project);
                barSeries.Items.Add(new RectangleBarItem(startDateDouble, projectIndex - 0.2, endDateDouble, projectIndex + 0.2));

                // Вторая часть (после возобновления)
                var resumeDateDouble = DateTimeAxis.ToDouble(project.ResumeDate);
                var resumeEndDateDouble = DateTimeAxis.ToDouble(project.ResumeEndDate);
                barSeries.Items.Add(new RectangleBarItem(resumeDateDouble, projectIndex - 0.2, resumeEndDateDouble, projectIndex + 0.2));

                PlotModel.Series.Add(barSeries);
            }

            // Добавляем обработчики событий
            AddMouseDownHandlersToSeries();

            ProjectTimeline.Model.InvalidatePlot(true);
        }


        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем фильтры
            SortByCompany.SelectedItem = null;
            SortByMonth.SelectedItem = null;
            SortByYear.SelectedItem = null;

            // Перезагружаем все проекты (без фильтрации)
            UpdatePlot(projects);
        }


        private void Go_Back(object sender, RoutedEventArgs e)
        {
            // Обработка кнопки "Назад"
            NavigationService.GoBack();
        }
    }

    public class ProjectData
    {
        public Guid ProjectId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime ResumeDate { get; set; }
        public DateTime ResumeEndDate { get; set; }
        public string Status { get; set; }
        public string OrganizationName { get; set; }
        public List<TeamMemberData> TeamMembers { get; set; }
    }

    public class TeamMemberData
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
    }
}
