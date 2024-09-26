using Npgsql;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Utilities;
using OxyPlot.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace GraphicsModule.Pages
{
    public partial class GeneralAdmCheckPeoplesProjsPage : Page
    {
        public PlotModel PlotModel { get; set; }

        private List<ProjectData> projects;
        public List<string> Companies { get; set; }
        public List<string> Months { get; set; }
        public List<int> Years { get; set; }

        public GeneralAdmCheckPeoplesProjsPage()
        {
            InitializeComponent();
            LoadProjectsData();
            LoadComboBoxData();
        }

        private void LoadProjectsData()
        {
            var connection = new Connection();

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
      u.name_user,
      pu.phase1_start, 
      pu.phase1_end,
      pu.phase2_start, 
      pu.phase2_end
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
      Users u ON pu.id_user = u.id_user
  WHERE 
      s.status_name != 'Архивирован'";

            using (var conn = connection.GetConnection())
            {
                using (var cmd = new NpgsqlCommand(query, conn))
                {
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
                                    ResumeDate = reader.GetDateTime(3),
                                    ResumeEndDate = reader.GetDateTime(4),
                                    Status = reader.GetString(5),
                                    OrganizationName = reader.GetString(6),
                                    TeamMembers = new List<TeamMemberData>()
                                };
                                projects.Add(projectData);
                            }

                            var teamMember = new TeamMemberData
                            {
                                UserId = reader.GetGuid(7),
                                UserName = reader.GetString(8),
                                Phase1Start = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                                Phase1End = reader.IsDBNull(10) ? DateTime.MinValue : reader.GetDateTime(10),
                                Phase2Start = reader.IsDBNull(11) ? DateTime.MinValue : reader.GetDateTime(11),
                                Phase2End = reader.IsDBNull(12) ? DateTime.MinValue : reader.GetDateTime(12)
                            };
                            Debug.WriteLine($"User: {teamMember.UserName}, Phase1: {teamMember.Phase1Start} - {teamMember.Phase1End}, Phase2: {teamMember.Phase2Start} - {teamMember.Phase2End}");
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
            PlotModel = new PlotModel { Title = "Общий график работы с проектами" };

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

            UpdatePlot(projects);
        }

        private void UpdatePlot(List<ProjectData> filteredProjects)
        {
            PlotModel.Series.Clear();

            foreach (var project in filteredProjects)
            {
                var projectIndex = filteredProjects.IndexOf(project);

                var barSeries = new RectangleBarSeries
                {
                    FillColor = OxyColors.CornflowerBlue,
                    StrokeColor = OxyColors.Black,
                    StrokeThickness = 1,
                    Title = project.OrganizationName
                };

                // Отображение первой фазы проекта
                if (project.StartDate != DateTime.MinValue && project.EndDate != DateTime.MinValue)
                {
                    var startDateDouble = DateTimeAxis.ToDouble(project.StartDate);
                    var endDateDouble = DateTimeAxis.ToDouble(project.EndDate);
                    barSeries.Items.Add(new RectangleBarItem(startDateDouble, projectIndex - 0.4, endDateDouble, projectIndex - 0.1));
                }

                // Отображение второй фазы проекта
                if (project.ResumeDate != DateTime.MinValue && project.ResumeEndDate != DateTime.MinValue)
                {
                    var resumeStartDouble = DateTimeAxis.ToDouble(project.ResumeDate);
                    var resumeEndDouble = DateTimeAxis.ToDouble(project.ResumeEndDate);
                    barSeries.Items.Add(new RectangleBarItem(resumeStartDouble, projectIndex + 0.1, resumeEndDouble, projectIndex + 0.4));
                }

                PlotModel.Series.Add(barSeries);

                // Внутри каждой фазы отображаем узкие прямоугольники для членов команды
                foreach (var teamMember in project.TeamMembers)
                {
                    // Фаза 1 работы участника
                    if (teamMember.Phase1Start != DateTime.MinValue && teamMember.Phase1End != DateTime.MinValue)
                    {
                        var phase1StartDouble = DateTimeAxis.ToDouble(teamMember.Phase1Start);
                        var phase1EndDouble = DateTimeAxis.ToDouble(teamMember.Phase1End);

                        barSeries.Items.Add(new RectangleBarItem(phase1StartDouble, projectIndex - 0.35, phase1EndDouble, projectIndex - 0.15)
                        {
                            // Цвет фазы 1 работы участника
                            Color = OxyColors.LightGreen
                        });
                    }

                    // Фаза 2 работы участника
                    if (teamMember.Phase2Start != DateTime.MinValue && teamMember.Phase2End != DateTime.MinValue)
                    {
                        var phase2StartDouble = DateTimeAxis.ToDouble(teamMember.Phase2Start);
                        var phase2EndDouble = DateTimeAxis.ToDouble(teamMember.Phase2End);

                        barSeries.Items.Add(new RectangleBarItem(phase2StartDouble, projectIndex + 0.15, phase2EndDouble, projectIndex + 0.35)
                        {
                            // Цвет фазы 2 работы участника
                            Color = OxyColors.LightBlue
                        });
                    }
                }

                // Обработчик клика для обновления информации о проекте
                barSeries.MouseDown += (s, e) =>
                {
                    UpdateCompanyName(project.OrganizationName);
                    UpdateTeamList(project.TeamMembers);
                };
            }

            ProjectTimeline.Model = PlotModel;
            ProjectTimeline.InvalidatePlot(true); // Обновляем отображение
        }


        private void UpdateTeamList(List<TeamMemberData> teamMembers)
        {
            TeamList.ItemsSource = teamMembers;
        }

        private void UpdateCompanyName(string organizationName)
        {
            CompanyName.Text = organizationName;
        }

        private void LoadComboBoxData()
        {
            Companies = projects.Select(p => p.OrganizationName).Distinct().ToList();
            Months = projects.Select(p => p.StartDate.ToString("MMMM")).Distinct().ToList();
            Years = projects.Select(p => p.StartDate.Year).Distinct().ToList();

            SortByCompany.ItemsSource = Companies;
            SortByMonth.ItemsSource = Months;
            SortByYear.ItemsSource = Years;
        }

        // Метод для применения комбинированной сортировки
        private void ApplyFilters()
        {
            var filteredProjects = projects.AsEnumerable();

            if (SortByCompany.SelectedItem is string selectedCompany)
            {
                filteredProjects = filteredProjects.Where(p => p.OrganizationName == selectedCompany);
            }

            if (SortByMonth.SelectedItem is string selectedMonth)
            {
                filteredProjects = filteredProjects.Where(p => p.StartDate.ToString("MMMM") == selectedMonth);
            }

            if (SortByYear.SelectedItem is int selectedYear)
            {
                filteredProjects = filteredProjects.Where(p => p.StartDate.Year == selectedYear);
            }

            UpdatePlot(filteredProjects.ToList());
        }

        // Комбинированная сортировка при изменении компании
        private void SortByCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Комбинированная сортировка при изменении месяца
        private void SortByMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Комбинированная сортировка при изменении года
        private void SortByYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        // Сброс фильтров
        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            SortByCompany.SelectedItem = null;
            SortByMonth.SelectedItem = null;
            SortByYear.SelectedItem = null;
            UpdatePlot(projects);
        }

        private void Go_Back(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }
    }
}
