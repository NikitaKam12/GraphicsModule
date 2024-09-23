using Npgsql;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private Guid userId;

        public MatesAndPlanUserPage()
        {
            InitializeComponent();
            Debug.WriteLine("Page initialized.");
            LoadProjectsData();
            LoadComboBoxData();
        }

        private void LoadProjectsData()
        {
            var connection = new Connection();
            userId = Session.UserID;
            Debug.WriteLine($"User ID: {userId}");

            string query = @"
            SELECT 
    p.id_project,
    c.date_start,
    c.date_end,
    s.status_name,
    cl.org_name,
    u.id_user,
    u.name_user,
    pu.phase1_start,
    pu.phase1_end,
    pu.phase2_start,
    pu.phase2_end
FROM 
    ProjectUsers pu  -- Все пользователи проекта
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
    p.id_project IN (
        SELECT id_project FROM ProjectUsers WHERE id_user = @userId
    )  -- Проекты, в которых участвует текущий пользователь
    AND s.status_name != 'Архивирован'";

            using (var conn = connection.GetConnection())
            {
                Debug.WriteLine("Opened connection.");
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
                                    Status = reader.GetString(3),
                                    OrganizationName = reader.GetString(4),
                                    TeamMembers = new List<TeamMemberData>()
                                };
                                projects.Add(projectData);
                                Debug.WriteLine($"New project added: {projectData.OrganizationName}");
                            }

                            var userIdFromDb = reader.GetGuid(5);
                            if (!projectData.TeamMembers.Any(m => m.UserId == userIdFromDb))
                            {
                                var teamMember = new TeamMemberData
                                {
                                    UserId = userIdFromDb,
                                    UserName = reader.GetString(6),
                                    Phase1Start = reader.IsDBNull(7) ? (DateTime?)null : reader.GetDateTime(7),
                                    Phase1End = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                    Phase2Start = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                                    Phase2End = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10)
                                };
                                projectData.TeamMembers.Add(teamMember);
                                Debug.WriteLine($"Added team member: {teamMember.UserName} to project {projectData.OrganizationName}");
                            }
                        }

                        foreach (var project in projects)
                        {
                            Debug.WriteLine($"Project {project.OrganizationName} has {project.TeamMembers.Count} team members.");
                        }

                        InitializePlotModel();
                    }
                }
            }
        }

        private void InitializePlotModel()
        {
            PlotModel = new PlotModel { Title = "График работы по проектам" };

            if (!projects.Any())
            {
                Debug.WriteLine("No projects loaded.");
                return;
            }

            var startDate = projects.Min(p => p.StartDate);
            var endDate = projects.Max(p => p.EndDate);
            Debug.WriteLine($"Plotting range: {startDate} to {endDate}");

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
                Debug.WriteLine($"Added project to category axis: {project.OrganizationName}");
            }
            PlotModel.Axes.Add(categoryAxis);

            UpdatePlot(projects);
        }

        private void UpdatePlot(List<ProjectData> filteredProjects)
        {
            PlotModel.Series.Clear();

            foreach (var project in filteredProjects)
            {
                int projectIndex = filteredProjects.IndexOf(project);

                foreach (var member in project.TeamMembers)
                {
                    if (member.Phase1Start.HasValue && member.Phase1End.HasValue)
                    {
                        var phase1Series = new RectangleBarSeries
                        {
                            FillColor = OxyColors.Green,
                            StrokeColor = OxyColors.Black,
                            StrokeThickness = 1
                        };

                        var phase1StartDouble = DateTimeAxis.ToDouble(member.Phase1Start.Value);
                        var phase1EndDouble = DateTimeAxis.ToDouble(member.Phase1End.Value);

                        phase1Series.Items.Add(new RectangleBarItem(phase1StartDouble, projectIndex - 0.2, phase1EndDouble, projectIndex + 0.2));

                        // Добавляем событие клика на фазу 1, но привязываем его к конкретному проекту
                        phase1Series.MouseDown += (s, e) =>
                        {
                            Debug.WriteLine($"Clicked on project: {project.OrganizationName}");
                            UpdateCompanyName(project.OrganizationName);
                            UpdateTeamList(project.TeamMembers);
                        };

                        PlotModel.Series.Add(phase1Series);
                    }

                    if (member.Phase2Start.HasValue && member.Phase2End.HasValue)
                    {
                        var phase2Series = new RectangleBarSeries
                        {
                            FillColor = OxyColors.Blue,
                            StrokeColor = OxyColors.Black,
                            StrokeThickness = 1
                        };

                        var phase2StartDouble = DateTimeAxis.ToDouble(member.Phase2Start.Value);
                        var phase2EndDouble = DateTimeAxis.ToDouble(member.Phase2End.Value);

                        phase2Series.Items.Add(new RectangleBarItem(phase2StartDouble, projectIndex - 0.2, phase2EndDouble, projectIndex + 0.2));

                        // Добавляем событие клика на фазу 2, привязываем его к проекту
                        phase2Series.MouseDown += (s, e) =>
                        {
                            Debug.WriteLine($"Clicked on project: {project.OrganizationName}");
                            UpdateCompanyName(project.OrganizationName);
                            UpdateTeamList(project.TeamMembers);
                        };

                        PlotModel.Series.Add(phase2Series);
                    }
                }
            }

            ProjectTimeline.Model = PlotModel;
            ProjectTimeline.InvalidatePlot(true);
        }


        private void UpdateTeamList(List<TeamMemberData> teamMembers)
        {
            Debug.WriteLine("Updating team list...");
            foreach (var member in teamMembers)
            {
                Debug.WriteLine($"Team Member: {member.UserName}");
            }
            TeamList.ItemsSource = teamMembers;
        }

        private void UpdateCompanyName(string organizationName)
        {
            Debug.WriteLine($"Updating company name to: {organizationName}");
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

        private void ApplyFilters()
        {
            var filteredProjects = projects.AsEnumerable();

            if (SortByCompany.SelectedItem is string selectedCompany)
            {
                Debug.WriteLine($"Filter applied by company: {selectedCompany}");
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

        private void SortByCompany_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortByMonth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SortByYear_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

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
        public DateTime? Phase1Start { get; set; }  // Дата начала фазы 1
        public DateTime? Phase1End { get; set; }    // Дата конца фазы 1
        public DateTime? Phase2Start { get; set; }  // Дата начала фазы 2
        public DateTime? Phase2End { get; set; }    // Дата конца фазы 2
        public bool Phase1 { get; set; }
        public bool Phase2 { get; set; }

    }
}

